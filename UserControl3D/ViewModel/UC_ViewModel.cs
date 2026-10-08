using System.Threading;
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
    [Reactive] public partial bool IsBusy { get; set; }
    [Reactive] public partial string Status { get; set; } = "Import an image to render it as a 3D gray level heatmap.";

    private readonly MultipleDisposable disposables = new();
    private readonly SynchronizationContext? ui = SynchronizationContext.Current;
    private bool rendering;
    public EffectsManager EffectsManager { get; } = new DefaultEffectsManager();

    public void SetImageFile(string? path) => File = path ?? string.Empty;

    [ReactiveCommand] public async Task LoadImage(CancellationToken token)
    {
        Image = await Task.Run(() => ImageLoader.Load(File), token);
        await Render();
    }

    [ReactiveCommand] public async Task Render()
    {
        GrayImage? image = Image;
        if (image is null || rendering) return;
        rendering = true;
        try
        {
            var geometry = await Task.Run(() => Image2Dto3D.CreateSurface(image));
            await PublishAsync(() =>
            {
                ModelMesh = new MeshGeometryModel3D
                {
                    Geometry = geometry,
                    Material = new VertColorMaterial(),
                    IsHitTestVisible = true,
                    IsTransparent = false
                };
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
