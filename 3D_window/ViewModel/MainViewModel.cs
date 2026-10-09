using System.Windows.Media.Media3D;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace _3D_window.ViewModel;

public partial class MainViewModel : ReactiveObject
{
    // Five vertices of a square pyramid: four base corners and the apex.
    [Reactive] public partial List<Point3D> Coordinates { get; set; } =
    [
        new Point3D(-10, -10, 0),
        new Point3D(10, -10, 0),
        new Point3D(10, 10, 0),
        new Point3D(-10, 10, 0),
        new Point3D(0, 0, 20)
    ];
}
