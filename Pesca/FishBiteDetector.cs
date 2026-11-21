using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Pesca;

internal sealed class FishBiteDetector(nint window)
{
    public bool IsVisible()
    {
        var client = RobloxWindowActivator.GetClientBounds(window);
        var region = new Rectangle(client.Left + (int)(client.Width * 0.70),
            client.Top + (int)(client.Height * 0.80),
            Math.Max(1, (int)(client.Width * 0.10)), Math.Max(1, (int)(client.Height * 0.17)));
        using var capture = new Bitmap(region.Width, region.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(capture))
        {
            graphics.CopyFromScreen(region.Location, Point.Empty, region.Size);
        }

        using var sample = new Bitmap(160, 96, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(sample))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(capture, new Rectangle(0, 0, sample.Width, sample.Height));
        }

        var rgb = new byte[sample.Width * sample.Height * 3];
        for (var y = 0; y < sample.Height; y++)
        {
            for (var x = 0; x < sample.Width; x++)
            {
                var color = sample.GetPixel(x, y);
                var offset = (y * sample.Width + x) * 3;
                rgb[offset] = color.R;
                rgb[offset + 1] = color.G;
                rgb[offset + 2] = color.B;
            }
        }

        return FishingCue.IsPresent(rgb, sample.Width, sample.Height);
    }
}
