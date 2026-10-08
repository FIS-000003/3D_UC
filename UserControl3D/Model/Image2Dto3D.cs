using System.Numerics;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;

namespace UserControl3D.Model;

public static class Image2Dto3D
{
    public const double HeightSpan = 255.0;

    public static MeshGeometry3D CreateSurface(GrayImage image, double heightSpan = HeightSpan)
    {
        ArgumentNullException.ThrowIfNull(image);
        int width = image.Width, height = image.Height;
        if (width < 2 || height < 2) throw new InvalidOperationException("Image is too small to build a surface.");
        int lo = image.MinValue, hi = image.MaxValue;
        if (hi < lo) (lo, hi) = (hi, lo);
        var mesh = new MeshGeometry3D
        {
            Positions = new Vector3Collection(width * height),
            Normals = new Vector3Collection(width * height),
            Colors = new Color4Collection(width * height),
            TriangleIndices = new IntCollection((width - 1) * (height - 1) * 6)
        };
        double zScale = heightSpan / 255.0, range = hi - lo;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                byte value = image.Pixels[y * width + x];
                double t = range == 0 ? 0 : (value - lo) / range;
                mesh.Positions!.Add(new Vector3(x, y, (float)(value * zScale)));
                mesh.Colors!.Add(GetHeatColor(t));
            }
        for (int y = 0; y < height - 1; y++)
            for (int x = 0; x < width - 1; x++)
            {
                int topLeft = y * width + x, topRight = topLeft + 1, bottomLeft = topLeft + width, bottomRight = bottomLeft + 1;
                mesh.TriangleIndices!.Add(topLeft); mesh.TriangleIndices.Add(bottomLeft); mesh.TriangleIndices.Add(topRight);
                mesh.TriangleIndices.Add(topRight); mesh.TriangleIndices.Add(bottomLeft); mesh.TriangleIndices.Add(bottomRight);
            }
        mesh.UpdateNormals();
        return mesh;
    }

    public static Color4 GetHeatColor(double t)
    {
        t = Math.Clamp(t, 0, 1);
        if (t < .25) return Interpolate(new Color4(0, 0, 1, 1), new Color4(0, 1, 1, 1), t / .25);
        if (t < .5) return Interpolate(new Color4(0, 1, 1, 1), new Color4(0, 1, 0, 1), (t - .25) / .25);
        if (t < .75) return Interpolate(new Color4(0, 1, 0, 1), new Color4(1, 1, 0, 1), (t - .5) / .25);
        return Interpolate(new Color4(1, 1, 0, 1), new Color4(1, 0, 0, 1), (t - .75) / .25);
    }

    private static Color4 Interpolate(Color4 a, Color4 b, double t) => new(
        (float)(a.Red + (b.Red - a.Red) * Math.Clamp(t, 0, 1)),
        (float)(a.Green + (b.Green - a.Green) * Math.Clamp(t, 0, 1)),
        (float)(a.Blue + (b.Blue - a.Blue) * Math.Clamp(t, 0, 1)), 1f);
}
