namespace UserControl3D.Model;

public sealed record GrayImage(int Width, int Height, byte[] Pixels)
{
    public byte this[int x, int y] => Pixels[y * Width + x];
    public byte MinValue { get; init; }
    public byte MaxValue { get; init; }
}
