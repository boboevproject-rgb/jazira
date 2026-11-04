using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfPath = System.Windows.Shapes.Path;

namespace Jazira;

// Полка: бросили файл на остров — он лёг на полку; потом его можно вытащить мышью в любую папку,
// на рабочий стол, в Telegram или письмо. Полка хранит только пути: сами файлы остаются на месте.
public partial class IslandWindow
{
    const int ShelfMax = 6;
    bool _dropHover, _dragOut;
    string? _tilePressed;
    Point _tilePressAt;

    void Island_DragEnter(object sender, DragEventArgs e)
    {
        if (!AcceptFiles(e)) return;
        _hoverIn.Stop();
        _hoverOut.Stop();
        if (!_dropHover) { _dropHover = true; RefreshShelf(); }
        if (_mode != Mode.Expanded || PageName != "shelf") Expand("shelf");
    }

    void Island_DragOver(object sender, DragEventArgs e) => AcceptFiles(e);

    // DragLeave приходит и при переходе между частями острова — сворачиваемся с задержкой,
    // а таймер сам проверит, где курсор на самом деле
    void Island_DragLeave(object sender, DragEventArgs e)
    {
        if (_dropHover) { _dropHover = false; RefreshShelf(); }
        if (_mode == Mode.Expanded) _hoverOut.Start();
    }

    void Island_Drop(object sender, DragEventArgs e)
    {
        _dropHover = false;
        if (AcceptFiles(e) && e.Data.GetData(DataFormats.FileDrop) is string[] files) AddToShelf(files);
        else RefreshShelf();
        _hoverOut.Start();
    }

    bool AcceptFiles(DragEventArgs e)
    {
        bool ok = !_dragOut && e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        return ok;
    }

    void AddToShelf(IEnumerable<string> paths)
    {
        var list = AppState.Current.Shelf;
        foreach (var p in paths)
        {
            list.Remove(p);
            list.Add(p);
        }
        while (list.Count > ShelfMax) list.RemoveAt(0); // старые уходят с полки, сами файлы не трогаем
        AppState.Save();
        RefreshShelf();
    }

    void RemoveFromShelf(string path)
    {
        AppState.Current.Shelf.Remove(path);
        AppState.Save();
        RefreshShelf();
    }

    void ShelfClear_Click(object sender, RoutedEventArgs e)
    {
        AppState.Current.Shelf.Clear();
        AppState.Save();
        RefreshShelf();
    }

    void RefreshShelf()
    {
        var items = AppState.Current.Shelf;
        ShelfTitle.Text = items.Count > 0 ? $"{Strings.Get("shelf.title")} · {items.Count}" : Strings.Get("shelf.title");
        ShelfClearText.Text = Strings.Get("shelf.clear");
        ShelfShotText.Text = Strings.Get("shelf.shot");
        ShelfShot.Visibility = AppState.Current.ShotsToShelf ? Visibility.Visible : Visibility.Collapsed;
        ShelfShot.ToolTip = ShotKeyOk ? ShotKey : null;
        ShelfClear.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ShelfZone.Visibility = items.Count == 0 || _dropHover ? Visibility.Visible : Visibility.Collapsed;
        ShelfZone.Stroke = _dropHover ? Res("Lagoon") : Res("Faint");
        ShelfZone.Fill = _dropHover ? Theme.Brush(((SolidColorBrush)Res("Lagoon")).Color, 0x1F) : Brushes.Transparent;
        var st = AppState.Current;
        ShelfHint.Text = Strings.Get(_dropHover ? "shelf.drop" : st.DownloadsToShelf && st.ShotsToShelf ? "shelf.hintAuto" : "shelf.hint");
        ShelfHint.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShelfItems.Children.Clear();
        foreach (var p in items) ShelfItems.Children.Add(MakeTile(p));
    }

    FrameworkElement MakeTile(string path)
    {
        bool exists = File.Exists(path) || Directory.Exists(path);
        var name = System.IO.Path.GetFileName(path.TrimEnd('\\'));
        var label = new TextBlock
        {
            Text = name.Length > 0 ? name : path,
            FontSize = 10.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(3, 4, 3, 0),
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Dim");
        var icon = new Image { Width = 30, Height = 30, Source = ShellIcon.For(path), Opacity = exists ? 1 : 0.35 };
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(icon);
        stack.Children.Add(label);

        var cross = new WpfPath { Data = CrossGeo, Width = 7, Height = 7, Stretch = Stretch.Uniform, StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        cross.SetResourceReference(WpfPath.StrokeProperty, "Fg");
        var close = new Button
        {
            Style = (Style)FindResource("IconBtn"),
            Width = 18, Height = 18,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
            Content = cross,
        };
        close.SetResourceReference(BackgroundProperty, "Chip");
        close.Click += (_, _) => RemoveFromShelf(path);

        var grid = new Grid();
        grid.Children.Add(stack);
        grid.Children.Add(close);
        var tile = new Border
        {
            Width = 56, Height = 64,
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 0, 2, 0),
            Child = grid,
            ToolTip = path,
            Cursor = Cursors.Hand,
        };
        tile.MouseEnter += (_, _) => { tile.SetResourceReference(Border.BackgroundProperty, "Hover"); close.Visibility = Visibility.Visible; };
        tile.MouseLeave += (_, _) => { tile.Background = Brushes.Transparent; close.Visibility = Visibility.Collapsed; };
        tile.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true; // не начинать перетаскивание острова
            if (e.ClickCount == 2) { Open(path); return; }
            _tilePressed = path;
            _tilePressAt = e.GetPosition(this);
        };
        tile.MouseLeftButtonUp += (_, _) => _tilePressed = null;
        tile.MouseMove += (_, e) =>
        {
            if (_tilePressed != path || e.LeftButton != MouseButtonState.Pressed) return;
            if ((e.GetPosition(this) - _tilePressAt).Length < 5) return;
            _tilePressed = null;
            DragOut(tile, path);
        };
        return tile;
    }

    void DragOut(DependencyObject source, string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return;
        _dragOut = true;
        try
        {
            DragDrop.DoDragDrop(source, new DataObject(DataFormats.FileDrop, new[] { path }),
                                DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
        }
        catch (Exception ex) { App.Log(ex); }
        finally { _dragOut = false; }
        // Файл перенесли (а не скопировали) — на старом месте его больше нет, убираем с полки
        if (!File.Exists(path) && !Directory.Exists(path)) RemoveFromShelf(path);
        if (_mode == Mode.Expanded) _hoverOut.Start();
    }

    static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { App.Log(ex); }
    }

    // Проводник с выделенным файлом
    static void Reveal(string path)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); }
        catch (Exception ex) { App.Log(ex); }
    }

    // ---- Скачанное и снимки экрана сами ложатся на полку -------------------------------------

    const string ShotKey = "Ctrl+PrtScn";
    const uint VK_SNAPSHOT = 0x2C;
    // Программы и сценарии из уведомления не запускаем — только показываем в папке
    static readonly string[] RiskyExt =
    {
        ".exe", ".msi", ".msix", ".msixbundle", ".appx", ".appxbundle", ".bat", ".cmd", ".com", ".scr", ".ps1",
        ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta", ".lnk", ".url", ".reg", ".jar", ".cpl",
    };

    FolderWatch? _downloads, _shots;
    HotKeys? _keys;
    bool _shotBusy;
    DateTime _dlLast;
    int _dlCount;

    public bool ShotKeyOk { get; private set; } = true;

    // Пункт меню: «Снимки экрана — на полку · Ctrl+PrtScn» (и «занято», если клавишу держит другая программа)
    public string ShotsMenuText() =>
        $"{Strings.Get("f.shots")} · {ShotKey}" + (ShotKeyOk ? "" : $" — {Strings.Get("hotkey.busy")}");

    void WireFiles()
    {
        _downloads = new FolderWatch(Dispatcher, KnownFolder.Downloads);
        _shots = new FolderWatch(Dispatcher, KnownFolder.Screenshots);
        _downloads.Arrived += OnDownloaded;
        _shots.Arrived += OnShotsSaved;
    }

    void StartHotKeys()
    {
        _keys = new HotKeys(_hwnd);
        Closed += (_, _) => _keys.Dispose();
        ApplyHotKeys();
    }

    void ApplyFileFeatures()
    {
        var st = AppState.Current;
        if (_downloads != null) _downloads.Enabled = st.DownloadsToShelf;
        if (_shots != null) _shots.Enabled = st.ShotsToShelf;
        ApplyHotKeys();
    }

    void ApplyHotKeys()
    {
        if (_keys == null) return;
        ApplyIslandKey();
        if (AppState.Current.ShotsToShelf) ShotKeyOk = _keys.Set("shot", HotKeys.Ctrl, VK_SNAPSHOT, TakeScreenshot);
        else { _keys.Remove("shot"); ShotKeyOk = true; }
    }

    void OnDownloaded(List<string> files)
    {
        AddToShelf(files);
        var now = DateTime.Now;
        // Несколько файлов подряд (архив распаковали в «Загрузки») — одно уведомление со счётчиком
        bool more = _mode == Mode.Peek && _peekKind == "download" && now - _dlLast < TimeSpan.FromSeconds(8);
        _dlCount = more ? _dlCount + files.Count : files.Count;
        _dlLast = now;
        var last = files[^1];
        long size = 0;
        try { size = new FileInfo(last).Length; } catch (Exception) { }
        DownloadPeek(System.IO.Path.GetFileName(last), size, _dlCount, last);
    }

    void DownloadPeek(string name, long size, int count, string? path)
    {
        if (count > 1)
        {
            ShowPeek("download", Res("Lagoon"), UpdateGeo, Strings.Format("dl.many", count), Strings.Get("dl.shelf"), seconds: 6,
                     action: Strings.Get("shelf.title"), onAction: () => Expand("shelf"));
            return;
        }
        bool risky = RiskyExt.Contains(System.IO.Path.GetExtension(name).ToLowerInvariant());
        ShowPeek("download", Res("Lagoon"), UpdateGeo, ShortName(name),
                 $"{Strings.Get("dl.done")} · {Bytes(size)} · {Strings.Get("dl.shelf")}", seconds: 6,
                 action: Strings.Get(risky ? "shelf.reveal" : "shelf.open"),
                 onAction: () => { if (path == null) return; if (risky) Reveal(path); else Open(path); });
    }

    void OnShotsSaved(List<string> files)
    {
        AddToShelf(files);
        var last = files[^1];
        ShotPeek(last, ShortName(System.IO.Path.GetFileName(last)));
    }

    void ShotPeek(string path, string sub) =>
        ShowPeek("shot", Res("Lagoon"), ShotGeo, Strings.Get("shot.saved"), sub, seconds: 6,
                 action: Strings.Get("shelf.open"), onAction: () => Open(path));

    void ShelfShot_Click(object sender, RoutedEventArgs e) => TakeScreenshot();

    // Снимок экрана под курсором: в «Снимки экрана», в буфер обмена и на полку.
    // Сам остров в снимок не попадает: на миг просим Windows не показывать его при съёмке.
    async void TakeScreenshot()
    {
        if (_shotBusy || _instant) return;
        _shotBusy = true;
        try
        {
            bool visible = IsVisible && _hwnd != IntPtr.Zero;
            bool excluded = visible && Native.SetWindowDisplayAffinity(_hwnd, Native.WDA_EXCLUDEFROMCAPTURE);
            bool faded = visible && !excluded;
            if (faded) Opacity = 0;
            if (visible) await Task.Delay(excluded ? 80 : 220); // дать Windows перерисовать экран без острова
            BitmapSource image;
            try { image = ScreenShot.Capture(); }
            finally
            {
                if (excluded) Native.SetWindowDisplayAffinity(_hwnd, Native.WDA_NONE);
                if (faded) Opacity = 1;
            }
            var path = ScreenShot.NewPath();
            _shots?.Ignore(path); // папку снимков мы сами слушаем — не сообщать второй раз
            ScreenShot.Save(image, path);
            try { Clipboard.SetImage(image); }
            catch (Exception ex) { App.Log(ex); } // буфер занят другой программой — снимок всё равно на полке
            AddToShelf(new[] { path });
            ShotPeek(path, Strings.Format("shot.copied", $"{image.PixelWidth}×{image.PixelHeight}"));
        }
        catch (Exception ex)
        {
            App.Log(ex);
            ShowInfo(Strings.Get("shot.fail"));
        }
        finally { _shotBusy = false; }
    }

    // Длинное имя — с многоточием в середине, чтобы расширение осталось видно
    static string ShortName(string name, int max = 22)
    {
        if (name.Length <= max) return name;
        var ext = System.IO.Path.GetExtension(name);
        if (ext.Length > 8) ext = "";
        int head = max - ext.Length - 4;
        return name[..head].TrimEnd() + "…" + name[(name.Length - ext.Length - 3)..];
    }

    static string Bytes(long n)
    {
        var c = Strings.Culture;
        if (n < 1024) return Strings.Format("size.b", n);
        if (n < 1024 * 1024) return Strings.Format("size.kb", (n / 1024.0).ToString(n < 10 * 1024 ? "0.#" : "0", c));
        if (n < 1024L * 1024 * 1024) return Strings.Format("size.mb", (n / 1048576.0).ToString(n < 10 * 1048576L ? "0.#" : "0", c));
        return Strings.Format("size.gb", (n / 1073741824.0).ToString("0.#", c));
    }
}

// Значок файла, как в Проводнике; у картинок — уменьшенная копия самой картинки
static class ShellIcon
{
    static readonly Dictionary<string, ImageSource?> Cache = new();
    static readonly string[] Images = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff" };

    public static ImageSource? For(string path)
    {
        bool dir = Directory.Exists(path);
        var ext = dir ? "<dir>" : Path.GetExtension(path).ToLowerInvariant();
        if (!dir && Images.Contains(ext) && File.Exists(path))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; // файл не держим открытым
                bmp.DecodePixelWidth = 64;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch (Exception) { } // не картинка внутри — возьмём обычный значок
        }
        // У программ и ярлыков свой значок, у остальных — общий для расширения
        bool own = ext is ".exe" or ".lnk" or ".ico" or ".url";
        var key = own ? path : ext;
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var info = new SHFILEINFO();
        uint flags = SHGFI_ICON | SHGFI_LARGEICON, attrs = 0;
        string query = path;
        if (!own || !File.Exists(path))
        {
            flags |= SHGFI_USEFILEATTRIBUTES;
            attrs = dir ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
            query = dir ? "folder" : "file" + ext;
        }
        ImageSource? src = null;
        try
        {
            if (SHGetFileInfo(query, attrs, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags) != IntPtr.Zero && info.hIcon != IntPtr.Zero)
            {
                src = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
            }
        }
        catch (Exception) { }
        finally { if (info.hIcon != IntPtr.Zero) DestroyIcon(info.hIcon); }
        Cache[key] = src;
        return src;
    }

    const uint SHGFI_ICON = 0x100, SHGFI_LARGEICON = 0x0, SHGFI_USEFILEATTRIBUTES = 0x10;
    const uint FILE_ATTRIBUTE_DIRECTORY = 0x10, FILE_ATTRIBUTE_NORMAL = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SHGetFileInfo(string path, uint attrs, ref SHFILEINFO info, uint size, uint flags);

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr icon);
}
