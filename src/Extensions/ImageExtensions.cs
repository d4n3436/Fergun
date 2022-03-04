using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Fergun.Extensions
{
    public static class ImageExtensions
    {
        public static Color GetAverageColor(this Image<Rgba32> image)
        {
            var average = new Rgba32();

            image.ProcessPixelRows(accessor =>
            {
                int r = 0;
                int g = 0;
                int b = 0;

                for (int y = 0; y < accessor.Height; y++)
                {
                    var pixelRow = accessor.GetRowSpan(y);
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        ref var pixel = ref pixelRow[x];
                        r += pixel.R;
                        g += pixel.G;
                        b += pixel.B;
                    }
                }

                int total = image.Width * image.Height;

                average.R = (byte)(r / total);
                average.G = (byte)(g / total);
                average.B = (byte)(b / total);
            });

            return Color.FromPixel(average);
        }
    }
}