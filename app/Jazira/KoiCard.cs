using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Shapes = System.Windows.Shapes;

namespace Jazira;

// Картинка «Моя кои — 14 дней» для сторис: 1080×1920, рисуется кодом без окна.
// Сохраняется в «Изображения\Jazira», копируется в буфер и ложится на полку — остаётся вставить в Telegram или Instagram.
static class KoiCard
{
    public const int W = 1080, H = 1920;
    static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");

    public static BitmapSource Render(AppState st)
    {
        int age = KoiPet.Age(st), streak = KoiPet.Streak(st);
        var kind = KoiPet.Kind(st);
        bool two = KoiPet.HasSecond(st);

        var canvas = new Canvas { Width = W, Height = H, Background = new LinearGradientBrush(C("#0F2C36"), C("#04090C"), 90) };

        // Мягкое свечение за прудом
        var glow = new Shapes.Ellipse { Width = 1100, Height = 1000, Fill = new RadialGradientBrush(C("#4D2FB7D4"), C("#002FB7D4")) };
        Canvas.SetLeft(glow, -10);
        Canvas.SetTop(glow, 110);
        canvas.Children.Add(glow);

        var brand = Text("Jazira", 44, "#A6F2F7F6", FontWeights.SemiBold);
        brand.Width = W;
        Canvas.SetTop(brand, 120);
        canvas.Children.Add(brand);

        // Пруд: тот же, что в острове, только крупно. Рыбки сытые и бодрые.
        // Пруд маленький и растянут — тогда рыбки на картинке крупные
        var pond = new KoiPond { Width = 100, Height = 91, Level = 1, Energy = 1, Corner = 6, Lines = 0.42 };
        pond.SetFish(kind, KoiPet.Growth(st), two ? KoiPet.SecondKind(st) : null, KoiPet.Growth(st) * 0.6);
        var frame = new Border
        {
            Width = 840, Height = 764,
            Child = new Viewbox { Child = pond, Stretch = Stretch.Uniform },
            Effect = new DropShadowEffect { BlurRadius = 70, ShadowDepth = 18, Direction = 270, Opacity = 0.55, Color = Colors.Black },
        };
        Canvas.SetLeft(frame, 120);
        Canvas.SetTop(frame, 230);
        canvas.Children.Add(frame);

        // Подписи под прудом
        var info = new StackPanel { Width = W };
        var title = Text(age > 0 ? Strings.Format("card.title", Strings.Days(age)) : Strings.Get("card.first"), 80, "#FFF7FAF9", FontWeights.SemiBold);
        title.Margin = new Thickness(70, 0, 70, 0);
        info.Children.Add(title);
        var kindText = Strings.Get("kind." + kind.ToString().ToLowerInvariant());
        var sub = streak > 0 ? Strings.Format("card.streak", Strings.Days(streak)) + " · " + kindText : kindText;
        var subtitle = Text(sub, 44, "#B3F2F7F6", FontWeights.Normal);
        subtitle.Margin = new Thickness(70, 22, 70, 0);
        info.Children.Add(subtitle);

        // Что открыто и что впереди
        var chips = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(60, 56, 60, 0) };
        int best = KoiPet.Best(st);
        foreach (var (at, key, dot) in new[] { (0, "chip.kohaku", "#F2582A"), (KoiPet.SankeAt, "chip.sanke", "#232328"), (KoiPet.SecondAt, "chip.second", "#5CC8FF"), (KoiPet.OgonAt, "chip.ogon", "#F2B23A") })
            chips.Children.Add(Chip(Strings.Get(key), dot, best >= at, at));
        info.Children.Add(chips);
        Canvas.SetTop(info, 1070);
        canvas.Children.Add(info);

        var foot = new StackPanel { Width = W };
        var line1 = Text(Strings.Get("card.grow"), 36, "#A6F2F7F6", FontWeights.Normal);
        line1.Margin = new Thickness(80, 0, 80, 0);
        foot.Children.Add(line1);
        var line2 = Text(Strings.Get("card.app"), 30, "#6BF2F7F6", FontWeights.Normal);
        line2.Margin = new Thickness(80, 14, 80, 0);
        foot.Children.Add(line2);
        Canvas.SetTop(foot, 1700);
        canvas.Children.Add(foot);

        canvas.Measure(new Size(W, H));
        canvas.Arrange(new Rect(0, 0, W, H));
        canvas.UpdateLayout();
        pond.PoseApart();
        canvas.UpdateLayout(); // поза новая — пруд перерисуется до снимка
        var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(canvas);
        rtb.Freeze();
        return rtb;
    }

    // Картинка в «Изображения\Jazira\koi-ГГГГ-ММ-ДД.png»; за день — один файл, повторный клик его обновит
    public static string Save(BitmapSource image)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Jazira");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"koi-{DateTime.Now:yyyy-MM-dd}.png");
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(image));
        using (var f = File.Create(path)) enc.Save(f);
        return path;
    }

    static FrameworkElement Chip(string text, string dot, bool open, int at)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new Shapes.Ellipse
        {
            Width = 22, Height = 22, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center,
            Fill = Brush(open ? dot : "#33FFFFFF"), Stroke = Brush(open ? "#F7F2E8" : "#00000000"), StrokeThickness = 3,
        });
        var label = Text(open ? text : $"{text} · {at}", 34, open ? "#F2F7F6" : "#80F2F7F6", FontWeights.SemiBold);
        label.TextAlignment = TextAlignment.Left;
        row.Children.Add(label);
        return new Border
        {
            Child = row,
            CornerRadius = new CornerRadius(40),
            Padding = new Thickness(28, 14, 30, 14),
            Margin = new Thickness(10),
            Background = Brush(open ? "#24FFFFFF" : "#00000000"),
            BorderBrush = Brush(open ? "#00000000" : "#33FFFFFF"),
            BorderThickness = new Thickness(2),
        };
    }

    static TextBlock Text(string text, double size, string color, FontWeight weight) => new()
    {
        Text = text,
        FontSize = size,
        FontFamily = Display,
        FontWeight = weight,
        Foreground = Brush(color),
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };

    static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    static SolidColorBrush Brush(string hex) => new(C(hex));
}
