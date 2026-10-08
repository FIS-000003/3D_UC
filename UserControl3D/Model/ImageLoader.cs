using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UserControl3D.Model;

public static class ImageLoader
{
    public const int MaxDimension = 512;

    public static GrayImage Load(string filePath, int maxDimension = MaxDimension)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("No image file selected.", nameof(filePath));
        if (!File.Exists(filePath)) throw new FileNotFoundException("Image file not found.", filePath);

        BitmapSource bitmap = BitmapFrame.Create(new Uri(filePath, UriKind.Absolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        bitmap.Freeze();
        double scale = Math.Min(1.0, (double)maxDimension / Math.Max(bitmap.PixelWidth, bitmap.PixelHeight));
        if (scale < 1.0)
        {
            var scaled = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
            scaled.Freeze();
            bitmap = scaled;
        }

        var bgr = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr32, null, 0);
        bgr.Freeze();
        int width = bgr.PixelWidth;
        int height = bgr.PixelHeight;
        byte[] buffer = new byte[width * height * 4];
        bgr.CopyPixels(buffer, width * 4, 0);
        byte[] pixels = new byte[width * height];
        byte lo = byte.MaxValue;
        byte hi = byte.MinValue;

        for (int i = 0, p = 0; i < pixels.Length; i++, p += 4)
        {
            byte gray = (byte)Math.Clamp((int)Math.Round(0.114 * buffer[p] + 0.587 * buffer[p + 1] + 0.299 * buffer[p + 2]), 0, 255);
            pixels[i] = gray;
            if (gray < lo) lo = gray;
            if (gray > hi) hi = gray;
        }
        return new GrayImage(width, height, pixels) { MinValue = lo, MaxValue = hi };
    }
}
