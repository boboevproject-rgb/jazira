using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Jazira;

// Пружина: быстрый разгон, небольшой перелёт и затухание — остров раскрывается, как капля,
// которая растекается и встаёт на место.
sealed class SpringEase : EasingFunctionBase
{
    const double Damping = 0.55;
    const double Oscillations = 1.4;

    public SpringEase() { EasingMode = EasingMode.EaseIn; } // кривая целиком задана в EaseInCore

    protected override double EaseInCore(double t)
    {
        if (t >= 1) return 1;
        double w = 2 * Math.PI * Oscillations;
        double wd = w * Math.Sqrt(1 - Damping * Damping);
        return 1 - Math.Exp(-Damping * w * t) * (Math.Cos(wd * t) + Damping * w / wd * Math.Sin(wd * t));
    }

    protected override Freezable CreateInstanceCore() => new SpringEase();
}

// Самый «живой» цвет обложки: пиксели раскладываются по оттенкам, побеждает насыщенная яркая группа.
// Серые и тёмные обложки цвета не дают — тогда остров остаётся в цветах своего вида.
static class CoverColor
{
    public static Color? From(BitmapSource? src)
    {
        if (src == null) return null;
        try
        {
            var bmp = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            int w = bmp.PixelWidth, h = bmp.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            bmp.CopyPixels(px, stride, 0);

            var weight = new double[12];
            var sum = new double[12, 3];
            for (int y = 0; y < h; y += 2)
                for (int x = 0; x < w; x += 2)
                {
                    int i = y * stride + x * 4;
                    double b = px[i] / 255.0, g = px[i + 1] / 255.0, r = px[i + 2] / 255.0;
                    var (hue, s, v) = ToHsv(r, g, b);
                    if (s < 0.28 || v < 0.28) continue;
                    int bin = (int)(hue / 30) % 12;
                    double k = s * v * v;
                    weight[bin] += k;
                    sum[bin, 0] += r * k; sum[bin, 1] += g * k; sum[bin, 2] += b * k;
                }

            int best = 0;
            for (int i = 1; i < 12; i++) if (weight[i] > weight[best]) best = i;
            // Слишком мало цветных точек — обложка по сути серая
            if (weight[best] < (w / 2.0) * (h / 2.0) * 0.015) return null;

            var (hh, ss, vv) = ToHsv(sum[best, 0] / weight[best], sum[best, 1] / weight[best], sum[best, 2] / weight[best]);
            // Подсветка должна читаться на тёмном острове: поднимаем насыщенность и яркость
            return FromHsv(hh, Math.Max(ss, 0.55), Math.Max(vv, 0.88));
        }
        catch (Exception) { return null; }
    }

    // Светлее — для строки текста песни поверх тёмного фона
    public static Color Lighten(Color c, double k)
    {
        byte L(byte v) => (byte)(v + (255 - v) * k);
        return Color.FromRgb(L(c.R), L(c.G), L(c.B));
    }

    static (double H, double S, double V) ToHsv(double r, double g, double b)
    {
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double hue = 0;
        if (d > 0)
        {
            if (max == r) hue = 60 * (((g - b) / d) % 6);
            else if (max == g) hue = 60 * ((b - r) / d + 2);
            else hue = 60 * ((r - g) / d + 4);
        }
        if (hue < 0) hue += 360;
        return (hue, max <= 0 ? 0 : d / max, max);
    }

    static Color FromHsv(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
