using System.Threading;
using System.IO;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Win32;
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
    [Reactive] public partial string To2D3D { get; set; } = "2D View";
    [Reactive] public partial bool Availability { get; set; }

    private bool is2D;

    public bool Is2D
    {
        get => is2D;
        set
        {
            this.RaiseAndSetIfChanged(ref is2D, value);
            this.RaisePropertyChanged(nameof(Is3D));
        }
    }

    public bool Is3D => !Is2D;

    private readonly MultipleDisposable disposables = new();
    private readonly SynchronizationContext? ui = SynchronizationContext.Current;
    private bool rendering;
    public EffectsManager EffectsManager { get; } = new DefaultEffectsManager();

    [ReactiveCommand] public void SelectImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select an image file",
            Filter = "Image Files (*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.tiff;*.ico)|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.tiff;*.ico|All Files (*.*)|*.*",
            FilterIndex = 1,
            Multiselect = false
        };
        if (dialog.ShowDialog() == true) File = Path.GetFullPath(dialog.FileName);
    }

    [ReactiveCommand]
    public void Swap2D3D()
    {
        if (Image is null) return;
        Is2D = !Is2D;
        To2D3D = Is2D ? "3D View" : "2D View";
    }

    [ReactiveCommand] public async Task LoadImage(CancellationToken token)
    {
        Availability = false;
        Image = null;
        ModelMesh = null;
        Image = await Task.Run(() => ImageLoader.Load(File), token);
        await Render();
        Availability = Image is not null && ModelMesh is not null;
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
        SelectImageCommand.ThrownExceptions.Subscribe(ReportError).DisposeWith(disposables);
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
