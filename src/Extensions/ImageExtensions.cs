using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Fergun.Extensions
{
    public static class ImageExtensions
    {
        public static Color GetAverageColor(this Image<Rgba32> image)
        {
            int r = 0;
            int g = 0;
            int b = 0;

            for (int y = 0; y < image.Height; y++)
            {
                var rowSpan = image.GetPixelRowSpan(y);
                for (int x = 0; x < rowSpan.Length; x++)
                {
                    var pixel = rowSpan[x];
                    r += pixel.R;
                    g += pixel.G;
                    b += pixel.B;
                }
            }

            int total = image.Width * image.Height;

            r /= total;
            g /= total;
            b /= total;

            return Color.FromRgb((byte)r, (byte)g, (byte)b);
        }
    }
}