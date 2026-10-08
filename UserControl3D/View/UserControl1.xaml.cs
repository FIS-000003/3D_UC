using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using HelixToolkit.Maths;
using HelixToolkit.Wpf.SharpDX;
using HalconDotNet;
using ReactiveUI;
using ReactiveUI.Primitives;
using UserControl3D.ViewModel;

namespace UserControl3D;

public partial class UserControl1 : System.Windows.Controls.UserControl
{
    private readonly Stopwatch mouseMoveStopwatch = Stopwatch.StartNew();
    private static readonly TimeSpan MouseMoveInterval = TimeSpan.FromSeconds(0.05);
    private readonly UC_ViewModel vm;
    private HImage? displayedImage;
    private bool halconWindowInitialized;

    public UserControl1()
    {
        InitializeComponent();
        halconWindow.HInitWindow += (_, _) =>
        {
            halconWindowInitialized = true;
            DrawDisplayedImage();
        };
        vm = new UC_ViewModel();
        DataContext = vm;
        Unloaded += (_, _) =>
        {
            displayedImage?.Dispose();
            vm.Dispose();
        };
        vm.WhenAnyValue(x => x.Image).Subscribe(image =>
        {
            if (image is not null)
                Dispatcher.BeginInvoke(new Action(() => DisplayGrayImage(image)));
        });
        vm.WhenAnyValue(x => x.Is2D).Where(is2D => is2D).Subscribe(_ => HidePointInfo());
        vm.WhenAnyValue(x => x.ModelMesh).Where(x => x is not null).Subscribe(mesh =>
        {
            viewport.Items.Clear();
            viewport.Items.Add(mesh!);
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(CenterCamera));
        });
    }

    private void DisplayGrayImage(UserControl3D.Model.GrayImage image)
    {
        displayedImage?.Dispose();
        displayedImage = new HImage();
        var pin = GCHandle.Alloc(image.Pixels, GCHandleType.Pinned);
        try
        {
            displayedImage.GenImage1("byte", image.Width, image.Height, pin.AddrOfPinnedObject());
        }
        finally
        {
            pin.Free();
        }

        DrawDisplayedImage();
    }

    private void DrawDisplayedImage()
    {
        if (!halconWindowInitialized || displayedImage is null) return;
        halconWindow.HalconWindow.ClearWindow();
        halconWindow.HalconWindow.DispObj(displayedImage);
        halconWindow.SetFullImagePart(displayedImage);
    }

    private void CenterCamera()
    {
        var geometry = vm.ModelMesh?.Geometry;
        if (geometry?.Positions is null || geometry.Positions.Count == 0) return;
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
        foreach (var p in geometry.Positions)
        {
            minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); minZ = Math.Min(minZ, p.Z);
            maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); maxZ = Math.Max(maxZ, p.Z);
        }
        float cx = (minX + maxX) / 2f, cy = (minY + maxY) / 2f, cz = (minZ + maxZ) / 2f;
        viewport.FixedRotationPoint = new System.Windows.Media.Media3D.Point3D(cx, cy, cz);
        double size = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
        const double fov = 45;
        double distance = size / 2 / Math.Tan(fov * Math.PI / 360) * 1.25;
        viewport.Camera = new PerspectiveCamera
        {
            Position = new System.Windows.Media.Media3D.Point3D(cx, cy, cz - distance),
            LookDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, distance),
            UpDirection = new System.Windows.Media.Media3D.Vector3D(0, -1, 0),
            NearPlaneDistance = 0.1,
            FarPlaneDistance = 100000,
            FieldOfView = fov
        };
    }

    private void viewport_Loaded(object sender, RoutedEventArgs e) => Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
    {
        if (viewport.RenderHost is not null) viewport.RenderHost.ClearColor = new Color4(0, 0, 0, 1);
    }));

    private void viewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (mouseMoveStopwatch.Elapsed < MouseMoveInterval) return;
        mouseMoveStopwatch.Restart();
        var geometry = vm.ModelMesh?.Geometry;
        if (geometry?.Positions is null || geometry.Positions.Count == 0) { HidePointInfo(); return; }
        System.Windows.Point mouse = e.GetPosition(viewport);
        var hit = viewport.FindHits(mouse)?.FirstOrDefault(x => ReferenceEquals(x.Geometry, geometry));
        if (hit is null) { HidePointInfo(); return; }

        string text = $"X: {hit.PointHit.X:0.0}\nY: {hit.PointHit.Y:0.0}\nZ: {hit.PointHit.Z:0.0}";
        if (hit.TriangleIndices is { } tri)
        {
            Vector3 vertex = Closest(hit.PointHit, geometry.Positions[tri.Item1], geometry.Positions[tri.Item2], geometry.Positions[tri.Item3]);
            int x = (int)Math.Round(vertex.X), y = (int)Math.Round(vertex.Y);
            string gray = vm.Image is { } image && x >= 0 && x < image.Width && y >= 0 && y < image.Height ? image[x, y].ToString() : "-";
            text += $"\nPixel: {x}, {y}\nGray: {gray}";
        }
        PointInfoText.Text = text;
        double left = mouse.X + 15, top = mouse.Y + 15;
        if (viewport.ActualWidth > PointInfo.ActualWidth + 6) left = Math.Min(left, viewport.ActualWidth - PointInfo.ActualWidth - 6);
        if (viewport.ActualHeight > PointInfo.ActualHeight + 6) top = Math.Min(top, viewport.ActualHeight - PointInfo.ActualHeight - 6);
        PointInfo.Margin = new Thickness(left, top, 0, 0);
        PointInfo.Visibility = Visibility.Visible;
    }

    private static Vector3 Closest(Vector3 hit, Vector3 a, Vector3 b, Vector3 c)
    {
        float da = Vector3.DistanceSquared(hit, a), db = Vector3.DistanceSquared(hit, b), dc = Vector3.DistanceSquared(hit, c);
        return da <= db && da <= dc ? a : db <= da && db <= dc ? b : c;
    }

    private void HidePointInfo() => PointInfo.Visibility = Visibility.Collapsed;
    private void viewport_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => HidePointInfo();

}
