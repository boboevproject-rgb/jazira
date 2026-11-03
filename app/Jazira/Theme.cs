using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Jazira;

// Виды острова: фон, рамка и цвет текста. Разметка берёт их через DynamicResource,
// поэтому смена вида применяется сразу, без перезапуска.
sealed record Theme(string Id, Brush Bg, Color Border, Color Fg, bool Light, double ShadowOpacity)
{
    public static readonly Theme[] All =
    {
        new("night", Solid("#020507"), C("#1CFFFFFF"), C("#F2F7F6"), false, 0.45),
        // Полупрозрачный: сквозь остров видно рабочий стол
        new("glass", Vertical("#C2283840", "#D60A1418"), C("#4DFFFFFF"), C("#F7FBFA"), false, 0.32),
        new("lagoon", Vertical("#0F4C55", "#06232A"), C("#592FD4B8"), C("#EFFFFB"), false, 0.45),
        new("sunset", Diagonal("#3B1640", "#5C2614"), C("#59FF8A4C"), C("#FFF4EC"), false, 0.45),
        new("light", Vertical("#FFFFFF", "#ECF1F2"), C("#26000000"), C("#0E1A1E"), true, 0.22),
    };

    public string NameKey => "theme." + Id;

    public static Theme Get(string? id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];

    public void Apply(FrameworkElement host)
    {
        var r = host.Resources;
        var ink = Light ? Colors.Black : Colors.White;
        r["IslandBg"] = Bg;
        r["IslandBorder"] = Brush(Border);
        r["Fg"] = Brush(Fg);
        r["Dim"] = Brush(Fg, 0xA6);
        r["Faint"] = Brush(Fg, 0x6B);
        r["Hover"] = Brush(ink, Light ? (byte)0x14 : (byte)0x26);
        r["Press"] = Brush(ink, Light ? (byte)0x24 : (byte)0x3D);
        r["Chip"] = Brush(ink, Light ? (byte)0x10 : (byte)0x1F);
        r["Track"] = Brush(ink, Light ? (byte)0x1A : (byte)0x26);
    }

    public static SolidColorBrush Brush(Color c, byte? alpha = null)
    {
        var b = new SolidColorBrush(alpha is byte a ? Color.FromArgb(a, c.R, c.G, c.B) : c);
        b.Freeze();
        return b;
    }

    static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    static Brush Solid(string hex) => Brush(C(hex));

    static Brush Vertical(string top, string bottom)
    {
        var g = new LinearGradientBrush(C(top), C(bottom), 90);
        g.Freeze();
        return g;
    }

    static Brush Diagonal(string from, string to)
    {
        var g = new LinearGradientBrush(C(from), C(to), new Point(0, 0), new Point(1, 1));
        g.Freeze();
        return g;
    }
}
