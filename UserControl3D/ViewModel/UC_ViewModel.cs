using System.Threading;
using System.Numerics;
using System.Windows.Media.Media3D;
using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;
using UserControl3D.Model;

namespace UserControl3D.ViewModel;

public partial class UC_ViewModel : ReactiveObject, IDisposable
{
    [Reactive] public partial string File { get; set; } = string.Empty;
    [Reactive] public partial GrayImage? Image { get; set; }
    [Reactive] public partial MeshGeometryModel3D? ModelMesh { get; set; }
    [Reactive] public partial PointGeometryModel3D? ModelPoints { get; set; }
    [Reactive] public partial bool IsBusy { get; set; }
    [Reactive] public partial string Status { get; set; } = "Import an image to render it as a 3D gray level heatmap.";

    private readonly MultipleDisposable disposables = new();
    private readonly SynchronizationContext? ui = SynchronizationContext.Current;
    private bool rendering;
    private int inputVersion;
    public EffectsManager EffectsManager { get; } = new DefaultEffectsManager();

    public void SetImageFile(string? path)
    {
        string selectedPath = path ?? string.Empty;
        if (string.Equals(File, selectedPath, StringComparison.Ordinal) && ModelPoints is null)
            return;

        inputVersion++;
        ModelPoints = null;
        File = selectedPath;
        if (string.IsNullOrWhiteSpace(File))
        {
            Image = null;
            ModelMesh = null;
            Status = "Provide an image file or 3D coordinates.";
        }
    }

    public void SetCoordinates(IReadOnlyList<Point3D>? coordinates)
    {
        inputVersion++;
        File = string.Empty;
        Image = null;
        ModelMesh = null;
        if (coordinates is null || coordinates.Count == 0)
        {
            ModelPoints = null;
            Status = "Provide an image file or 3D coordinates.";
            return;
        }

        var positions = new Vector3Collection(coordinates.Count);
        var colors = new Color4Collection(coordinates.Count);
        double minZ = coordinates.Min(point => point.Z);
        double maxZ = coordinates.Max(point => point.Z);
        double range = maxZ - minZ;
        foreach (var point in coordinates)
        {
            positions.Add(new Vector3((float)point.X, (float)point.Y, (float)point.Z));
            colors.Add(Image2Dto3D.GetHeatColor(range == 0 ? 0 : (point.Z - minZ) / range));
        }
        var indices = new IntCollection(coordinates.Count);
        for (int i = 0; i < coordinates.Count; i++) indices.Add(i);
        var pointGeometry = new PointGeometry3D
        {
            Positions = positions,
            Colors = colors,
            Indices = indices
        };
        var meshIndices = new IntCollection();
        if (coordinates.Count == 5)
        {
            // Five points are treated as a square pyramid: base corners 0-3, apex 4.
            int[] pyramidTriangles = [
                0, 1, 2, 0, 2, 3, // base
                0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4 // four sides
            ];
            foreach (int index in pyramidTriangles) meshIndices.Add(index);
        }
        else
        {
            meshIndices = new IntCollection(Math.Max(0, coordinates.Count - 2) * 3);
            for (int i = 1; i < coordinates.Count - 1; i++)
            {
                // Coordinate order is expected to follow the surface perimeter.
                meshIndices.Add(0);
                meshIndices.Add(i);
                meshIndices.Add(i + 1);
            }
        }
        var meshGeometry = new HelixToolkit.SharpDX.MeshGeometry3D
        {
            Positions = positions,
            Colors = colors,
            TriangleIndices = meshIndices
        };
        if (coordinates.Count >= 3) meshGeometry.UpdateNormals();
        ModelMesh = new MeshGeometryModel3D
        {
            Geometry = meshGeometry,
            Material = new VertColorMaterial(),
            IsHitTestVisible = true,
            IsTransparent = false
        };
        ModelPoints = new PointGeometryModel3D
        {
            Geometry = pointGeometry,
            Size = new System.Windows.Size(3, 3),
            Color = System.Windows.Media.Colors.White,
            EnableColorBlending = true,
            BlendingFactor = 1,
            IsHitTestVisible = true
        };
        Status = $"Coordinate point cloud | {coordinates.Count} points";
    }

    [ReactiveCommand] public async Task LoadImage(CancellationToken token)
    {
        string path = File;
        int version = inputVersion;
        var image = await Task.Run(() => ImageLoader.Load(path), token);
        if (version != inputVersion || string.IsNullOrWhiteSpace(File)) return;
        Image = image;
        await Render();
    }

    [ReactiveCommand] public async Task Render()
    {
        GrayImage? image = Image;
        if (image is null || rendering) return;
        int version = inputVersion;
        rendering = true;
        try
        {
            var geometry = await Task.Run(() => Image2Dto3D.CreateSurface(image));
            await PublishAsync(() =>
            {
                if (version != inputVersion) return;
                ModelMesh = new MeshGeometryModel3D
                {
                    Geometry = geometry,
                    Material = new VertColorMaterial(),
                    IsHitTestVisible = true,
                    IsTransparent = false
                };
                ModelPoints = null;
                Status = $"Surface {image.Width} x {image.Height} px  |  image gray {image.MinValue}-{image.MaxValue}";
            });
        }
        finally { rendering = false; }
    }

    public UC_ViewModel()
    {
        this.WhenAnyValue(x => x.File)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .DistinctUntilChanged()
            .Subscribe(_ => LoadImageCommand.Execute().Subscribe())
            .DisposeWith(disposables);
        this.WhenAnyObservable(x => x.LoadImageCommand.IsExecuting, x => x.RenderCommand.IsExecuting)
            .Subscribe(busy => IsBusy = busy)
            .DisposeWith(disposables);
        LoadImageCommand.ThrownExceptions.Subscribe(ReportError).DisposeWith(disposables);
        RenderCommand.ThrownExceptions.Subscribe(ReportError).DisposeWith(disposables);
    }

    private void ReportError(Exception ex) => Status = $"Failed: {ex.Message}";

    private Task PublishAsync(Action action)
    {
        if (ui is null) { action(); return Task.CompletedTask; }
        var tcs = new TaskCompletionSource();
        ui.Post(_ =>
        {
            try { action(); tcs.TrySetResult(); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }, null);
        return tcs.Task;
    }

    public void Dispose()
    {
        disposables.Dispose();
        EffectsManager.Dispose();
    }
}
