using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Jazira;

// Jazira.exe --snapshot <папка>: рисует остров во всех состояниях в PNG (масштаб 200 %),
// не показывая его на экране. Так вёрстку можно проверить без мыши и без живого плеера.
static class Snapshot
{
    public static void Render(string dir)
    {
        Directory.CreateDirectory(dir);
        var w = new IslandWindow(snapshot: true) { Left = -4000, Top = -4000 };
        w.Root.Background = new SolidColorBrush(Color.FromRgb(0x1D, 0x2B, 0x33)); // «рабочий стол» под островом
        w.Show();
        foreach (var (name, setup) in w.SnapshotStates(FakeCover()))
        {
            setup();
            w.RefreshAll();
            w.UpdateLayout();
            Save(w, Path.Combine(dir, name + ".png"));
        }
        w.Close();
    }

    static void Save(IslandWindow w, string path)
    {
        const double dpi = 192, k = dpi / 96;
        var root = w.Root;
        var rtb = new RenderTargetBitmap((int)(root.ActualWidth * k), (int)(root.ActualHeight * k), dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(root);
        // Вырезаем остров с полями под тень
        double x = Math.Max(0, (root.ActualWidth - w.Island.ActualWidth) / 2 - 18);
        double width = Math.Min(root.ActualWidth - x, w.Island.ActualWidth + 36);
        double height = Math.Min(root.ActualHeight, w.Island.ActualHeight + 30);
        var crop = new CroppedBitmap(rtb, new Int32Rect((int)(x * k), 0, (int)(width * k), (int)(height * k)));
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(crop));
        using var f = File.Create(path);
        enc.Save(f);
    }

    // Обложка-заглушка: градиент «море и закат»
    static BitmapSource FakeCover()
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var brush = new LinearGradientBrush(Color.FromRgb(0x12, 0x30, 0x3F), Color.FromRgb(0xF3, 0xB5, 0x7A), 90);
            dc.DrawRectangle(brush, null, new Rect(0, 0, 128, 128));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xFF, 0xB1, 0x5C)), null, new Point(64, 84), 26, 26);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x10, 0x46, 0x50)), null, new Rect(0, 84, 128, 44));
        }
        var rtb = new RenderTargetBitmap(128, 128, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }
}
