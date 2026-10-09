using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using ReactiveUI;
using ReactiveUI.Primitives;
using UserControl3D.ViewModel;

namespace UserControl3D;

public partial class UserControl1 : UserControl
{
    public static readonly DependencyProperty ImageFilePathProperty = DependencyProperty.Register(
        nameof(ImageFilePath), typeof(string), typeof(UserControl1),
        new PropertyMetadata(string.Empty, OnImageFilePathChanged));

    public string ImageFilePath
    {
        get => (string)GetValue(ImageFilePathProperty);
        set => SetValue(ImageFilePathProperty, value);
    }

    public static readonly DependencyProperty CoordinatesProperty = DependencyProperty.Register(
        nameof(Coordinates), typeof(IEnumerable<Point3D>), typeof(UserControl1),
        new PropertyMetadata(null, OnCoordinatesChanged));

    public IEnumerable<Point3D>? Coordinates
    {
        get => (IEnumerable<Point3D>?)GetValue(CoordinatesProperty);
        set => SetValue(CoordinatesProperty, value);
    }

    private static void OnImageFilePathChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is UserControl1 control) control.UpdateInput();
    }

    private static void OnCoordinatesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is UserControl1 control) control.UpdateInput();
    }

    private readonly Stopwatch mouseMoveStopwatch = Stopwatch.StartNew();
    private static readonly TimeSpan MouseMoveInterval = TimeSpan.FromSeconds(0.05);
    private readonly UC_ViewModel vm;
    private MeshGeometryModel3D? surfaceModel;
    private PointGeometryModel3D? pointCloudModel;
    private AmbientLight3D? ambientLight;
    private bool showPointCloud;
    public UserControl1()
    {
        InitializeComponent();
        ambientLight = viewport.Items.OfType<AmbientLight3D>().FirstOrDefault();
        vm = new UC_ViewModel();
        DataContext = vm;
        Loaded += (_, _) => UpdateInput();
        Unloaded += (_, _) => vm.Dispose();
        vm.WhenAnyValue(x => x.ModelMesh).Where(x => x is not null).Subscribe(mesh =>
        {
            surfaceModel = mesh!;
            var pointGeometry = new PointGeometry3D
            {
                Positions = mesh!.Geometry?.Positions,
                Colors = mesh.Geometry?.Colors,
                Indices = new IntCollection(mesh.Geometry?.Positions?.Count ?? 0)
            };
            if (pointGeometry.Positions is { } positions && pointGeometry.Indices is { } indices)
                for (int i = 0; i < positions.Count; i++)
                    indices.Add(i);
            pointCloudModel = new PointGeometryModel3D
            {
                Geometry = pointGeometry,
                Size = new Size(3, 3),
                Color = System.Windows.Media.Colors.White,
                EnableColorBlending = true,
                BlendingFactor = 1,
                IsHitTestVisible = true
            };
            ToggleRepresentationButton.Visibility = Visibility.Visible;
            ShowCurrentRepresentation();
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(CenterCamera));
        });
        vm.WhenAnyValue(x => x.ModelPoints).Subscribe(_ =>
        {
            if (vm.ModelPoints is null && vm.ModelMesh is null) showPointCloud = false;
            ToggleRepresentationButton.Visibility = vm.ModelMesh is null
                ? Visibility.Collapsed
                : Visibility.Visible;
            ToggleRepresentationButton.Content = showPointCloud ? "Show Surface Mesh" : "Show Point Cloud";
            ShowCurrentRepresentation();
            if (vm.ModelPoints is not null || vm.ModelMesh is not null)
                Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(CenterCamera));
        });
    }

    private void UpdateInput()
    {
        var coordinates = Coordinates?.ToArray();
        if (coordinates is { Length: > 0 }) vm.SetCoordinates(coordinates);
        else vm.SetImageFile(ImageFilePath);
    }

    private void ToggleRepresentationButton_Click(object sender, RoutedEventArgs e)
    {
        showPointCloud = !showPointCloud;
        ToggleRepresentationButton.Content = showPointCloud ? "Show Surface Mesh" : "Show Point Cloud";
        ShowCurrentRepresentation();
    }

    private void ShowCurrentRepresentation()
    {
        if (vm.ModelPoints is null && vm.ModelMesh is null)
        {
            viewport.Items.Clear();
            viewport.InvalidateRender();
            HidePointInfo();
            return;
        }

        Element3D? model = showPointCloud
            ? vm.ModelPoints ?? pointCloudModel
            : surfaceModel;
        if (model is null)
        {
            viewport.Items.Clear();
            viewport.InvalidateRender();
            HidePointInfo();
            return;
        }

        viewport.Items.Clear();
        if (ambientLight is not null) viewport.Items.Add(ambientLight);
        viewport.Items.Add(model);
        viewport.InvalidateRender();
    }

    private void CenterCamera()
    {
        HelixToolkit.SharpDX.Geometry3D? geometry = showPointCloud
            ? vm.ModelPoints?.Geometry ?? pointCloudModel?.Geometry
            : vm.ModelMesh?.Geometry;
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
        if (size < 1) size = 1;
        const double fov = 45;
        double distance = size / 2 / Math.Tan(fov * Math.PI / 360) * 1.25;
        viewport.Camera = new HelixToolkit.Wpf.SharpDX.PerspectiveCamera
        {
            Position = new System.Windows.Media.Media3D.Point3D(cx, cy, cz - distance),
            LookDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, distance),
            UpDirection = new System.Windows.Media.Media3D.Vector3D(0, -1, 0),
            NearPlaneDistance = 0.1,
            FarPlaneDistance = 100000,
            FieldOfView = fov
        };
    }

    public void ResetView()
    {
        CenterCamera();
        viewport.InvalidateRender();
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space) return;

        ResetView();
        e.Handled = true;
    }

    private void viewport_Loaded(object sender, RoutedEventArgs e) => Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
    {
        if (viewport.RenderHost is not null) viewport.RenderHost.ClearColor = new Color4(0.22f, 0.25f, 0.29f, 1);
    }));

    private void viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (mouseMoveStopwatch.Elapsed < MouseMoveInterval) return;
        mouseMoveStopwatch.Restart();
        bool isPointCloud = showPointCloud;
        var geometry = showPointCloud
            ? vm.ModelPoints?.Geometry ?? pointCloudModel?.Geometry
            : vm.ModelMesh?.Geometry;
        if (geometry?.Positions is null || geometry.Positions.Count == 0) { HidePointInfo(); return; }
        Point mouse = e.GetPosition(viewport);
        var hit = viewport.FindHits(mouse)?.FirstOrDefault(x => ReferenceEquals(x.Geometry, geometry));
        if (hit is null) { HidePointInfo(); return; }

        Vector3 hitPosition = isPointCloud ? Closest(hit.PointHit, geometry.Positions) : hit.PointHit;
        string text = $"X: {hitPosition.X:0.0}\nY: {hitPosition.Y:0.0}\nZ: {hitPosition.Z:0.0}";
        if (vm.ModelPoints is not null && isPointCloud)
        {
            ShowPointInfo(mouse, text);
            return;
        }
        if (!isPointCloud && hit.TriangleIndices is { } tri)
        {
            Vector3 vertex = Closest(hitPosition, geometry.Positions[tri.Item1], geometry.Positions[tri.Item2], geometry.Positions[tri.Item3]);
            int x = (int)Math.Round(vertex.X), y = (int)Math.Round(vertex.Y);
            string gray = vm.Image is { } image && x >= 0 && x < image.Width && y >= 0 && y < image.Height ? image[x, y].ToString() : "-";
            text += $"\nPixel: {x}, {y}\nGray: {gray}";
        }
        else if (isPointCloud)
        {
            int x = (int)Math.Round(hitPosition.X), y = (int)Math.Round(hitPosition.Y);
            string gray = vm.Image is { } cloudImage && x >= 0 && x < cloudImage.Width && y >= 0 && y < cloudImage.Height ? cloudImage[x, y].ToString() : "-";
            text += $"\nPixel: {x}, {y}\nGray: {gray}";
        }
        ShowPointInfo(mouse, text);
    }

    private void ShowPointInfo(Point mouse, string text)
    {
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

    private static Vector3 Closest(Vector3 hit, Vector3Collection positions)
    {
        Vector3 closest = positions[0];
        float closestDistance = Vector3.DistanceSquared(hit, closest);
        for (int i = 1; i < positions.Count; i++)
        {
            float distance = Vector3.DistanceSquared(hit, positions[i]);
            if (distance >= closestDistance) continue;
            closest = positions[i];
            closestDistance = distance;
        }
        return closest;
    }

    private void HidePointInfo() => PointInfo.Visibility = Visibility.Collapsed;
    private void viewport_MouseLeave(object sender, MouseEventArgs e) => HidePointInfo();

}
