using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace Jazira;

// Снимок всего экрана, на котором сейчас курсор, — в настоящих пикселях монитора.
// Сохраняется туда же, куда Windows кладёт свои снимки (Win+PrtScn): «Изображения\Снимки экрана».
static class ScreenShot
{
    public static BitmapSource Capture()
    {
        var b = Forms.Screen.FromPoint(Forms.Cursor.Position).Bounds;
        using var bmp = new System.Drawing.Bitmap(b.Width, b.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
            g.CopyFromScreen(b.Left, b.Top, 0, 0, b.Size, System.Drawing.CopyPixelOperation.SourceCopy);
        var h = bmp.GetHbitmap();
        try
        {
            var src = Imaging.CreateBitmapSourceFromHBitmap(h, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally { DeleteObject(h); }
    }

    // «Снимок экрана 2026-11-04 153012.png»; если такой уже есть — «… (2).png»
    public static string NewPath()
    {
        var dir = KnownFolder.Screenshots;
        Directory.CreateDirectory(dir);
        var name = $"{Strings.Get("shot.file")} {DateTime.Now:yyyy-MM-dd HHmmss}";
        var path = Path.Combine(dir, name + ".png");
        for (int i = 2; File.Exists(path); i++) path = Path.Combine(dir, $"{name} ({i}).png");
        return path;
    }

    public static void Save(BitmapSource image, string path)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(image));
        var tmp = path + ".tmp";
        using (var f = File.Create(tmp)) enc.Save(f);
        File.Move(tmp, path, true);
    }

    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
}
