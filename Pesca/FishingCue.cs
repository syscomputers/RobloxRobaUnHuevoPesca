namespace Pesca;

internal static class FishingCue
{
    // Reconoce el botón rojo con texto blanco que acompaña al pez en la barra inferior.
    public static bool IsPresent(ReadOnlySpan<byte> rgb, int width, int height)
    {
        if (width <= 0 || height <= 0 || rgb.Length != checked(width * height * 3))
        {
            throw new ArgumentException("La imagen RGB no tiene las dimensiones indicadas.");
        }

        var red = new bool[width * height];
        var visited = new bool[red.Length];
        var queue = new int[red.Length];
        for (var pixel = 0; pixel < red.Length; pixel++)
        {
            var offset = pixel * 3;
            var r = rgb[offset];
            var g = rgb[offset + 1];
            var b = rgb[offset + 2];
            red[pixel] = r >= 160 && g < 170 && b < 170 && r > g * 1.4 && r > b * 1.15;
        }

        for (var start = 0; start < red.Length; start++)
        {
            if (!red[start] || visited[start]) continue;
            var head = 0;
            var tail = 1;
            queue[0] = start;
            visited[start] = true;
            var left = start % width;
            var right = left;
            var top = start / width;
            var bottom = top;

            while (head < tail)
            {
                var pixel = queue[head++];
                var x = pixel % width;
                var y = pixel / width;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
                if (x > 0) Enqueue(pixel - 1);
                if (x + 1 < width) Enqueue(pixel + 1);
                if (y > 0) Enqueue(pixel - width);
                if (y + 1 < height) Enqueue(pixel + width);
            }

            var boxWidth = right - left + 1;
            var boxHeight = bottom - top + 1;
            var area = boxWidth * boxHeight;
            var ratio = (double)boxWidth / boxHeight;
            if (boxWidth < width * 0.20 || boxWidth > width * 0.95 ||
                boxHeight < height * 0.12 || boxHeight > height * 0.80 ||
                ratio < 1.6 || ratio > 4.2 || tail < area * 0.30)
            {
                continue;
            }

            var white = 0;
            for (var y = top; y <= bottom; y++)
            {
                for (var x = left; x <= right; x++)
                {
                    var offset = (y * width + x) * 3;
                    if (rgb[offset] > 200 && rgb[offset + 1] > 200 && rgb[offset + 2] > 200) white++;
                }
            }

            if (white >= area * 0.02) return true;

            void Enqueue(int pixel)
            {
                if (red[pixel] && !visited[pixel])
                {
                    visited[pixel] = true;
                    queue[tail++] = pixel;
                }
            }
        }

        return false;
    }
}
