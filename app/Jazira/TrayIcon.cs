using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Jazira;

// Значок в трее и то же меню по правому клику на острове: вид, место, размер, функции,
// город, вода, язык, автозапуск, обновления, выход.
sealed class TrayIcon : IDisposable
{
    static readonly double[] Scales = { 0.8, 0.9, 1.0, 1.15, 1.3, 1.5 };

    readonly Forms.NotifyIcon _icon;
    readonly IslandWindow _island;
    readonly App _app;

    public TrayIcon(IslandWindow island, App app)
    {
        _island = island;
        _app = app;
        using var s = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/jazira.ico")).Stream;
        _icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(s, Forms.SystemInformation.SmallIconSize),
            Text = "Jazira",
            Visible = true
        };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ToggleIsland(); };
        Rebuild();
    }

    void ToggleIsland()
    {
        AppState.Current.ShowIsland = !AppState.Current.ShowIsland;
        AppState.Save();
        _island.ApplyVisibility();
        Rebuild();
    }

    // Меню у курсора — для правого клика по острову. NotifyIcon сам делает окно активным,
    // иначе меню не закрывалось бы по щелчку мимо.
    public void ShowMenu()
    {
        var show = typeof(Forms.NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        if (show != null) show.Invoke(_icon, null);
        else _icon.ContextMenuStrip?.Show(Forms.Cursor.Position);
    }

    // Меню собирается заново после каждого изменения — так галочки и язык всегда актуальны
    public void Rebuild()
    {
        var st = AppState.Current;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem($"Jazira {App.Version}") { Enabled = false });
        if (_app.ReadyVersion is Version ready)
            menu.Items.Add(Item(Strings.Format("tray.install", ready), _app.ApplyUpdate));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Item(Strings.Get("tray.show"), ToggleIsland, st.ShowIsland));

        var look = new Forms.ToolStripMenuItem(Strings.Get("tray.look"));
        foreach (var t in Theme.All)
            look.DropDownItems.Add(Item(Strings.Get(t.NameKey), () => Set(() => st.Theme = t.Id), st.Theme == t.Id));
        look.DropDownItems.Add(new Forms.ToolStripSeparator());
        look.DropDownItems.Add(Item(Strings.Get("shape.pill"), () => Set(() => st.Shape = "pill"), st.Shape != "notch"));
        var notch = Item(Strings.Get("shape.notch"), () => Set(() => st.Shape = "notch"), st.Shape == "notch");
        notch.Enabled = st.Docked; // вырез бывает только у прикреплённого к краю острова
        look.DropDownItems.Add(notch);
        look.DropDownItems.Add(new Forms.ToolStripSeparator());
        look.DropDownItems.Add(Item(Strings.Get("tray.tint"), () => Set(() => st.CoverTint = !st.CoverTint), st.CoverTint));
        menu.Items.Add(look);

        var place = new Forms.ToolStripMenuItem(Strings.Get("tray.place"));
        place.DropDownItems.Add(Item(Strings.Get("place.dock"), () => Set(() => st.Docked = true), st.Docked));
        place.DropDownItems.Add(Item(Strings.Get("place.float"), () => Set(() => st.Docked = false), !st.Docked));
        place.DropDownItems.Add(new Forms.ToolStripSeparator());
        var screens = Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var scr = screens[i];
            var label = Strings.Format("place.monitor", i + 1) + $" — {scr.Bounds.Width}×{scr.Bounds.Height}"
                      + (scr.Primary ? $" ({Strings.Get("place.primary")})" : "");
            bool current = scr.Primary ? st.Monitor == null || Array.TrueForAll(screens, s => s.DeviceName != st.Monitor) : st.Monitor == scr.DeviceName;
            place.DropDownItems.Add(Item(label, () => Set(() => st.Monitor = scr.Primary ? null : scr.DeviceName), current));
        }
        menu.Items.Add(place);

        var size = new Forms.ToolStripMenuItem(Strings.Get("tray.scale"));
        foreach (var k in Scales)
            size.DropDownItems.Add(Item($"{Math.Round(k * 100)}%", () => Set(() => st.Scale = k), Math.Abs(st.Scale - k) < 0.01));
        menu.Items.Add(size);

        var features = new Forms.ToolStripMenuItem(Strings.Get("tray.features"));
        features.DropDownItems.Add(Item(Strings.Get("f.lyrics"), () => Set(() => st.Lyrics = !st.Lyrics), st.Lyrics));
        features.DropDownItems.Add(Item(Strings.Get("f.koi"), () => Set(() => st.Koi = !st.Koi), st.Koi));
        features.DropDownItems.Add(Item(Strings.Get("f.indicators"), () => Set(() => st.Indicators = !st.Indicators), st.Indicators));
        features.DropDownItems.Add(Item(Strings.Get("f.local"), () => Set(() => st.LocalPage = !st.LocalPage), st.LocalPage));
        features.DropDownItems.Add(Item(Strings.Get("f.prayer"), () => Set(() => st.Prayer = !st.Prayer), st.Prayer));
        menu.Items.Add(features);

        var city = new Forms.ToolStripMenuItem(Strings.Get("tray.city"));
        foreach (var c in LocalInfo.Cities)
            city.DropDownItems.Add(Item(c.Name, () => Set(() => st.City = c.Id), st.City == c.Id));
        menu.Items.Add(city);

        var water = new Forms.ToolStripMenuItem(Strings.Get("tray.water"));
        foreach (var ml in new[] { 1500, 2000, 2500, 3000 })
            water.DropDownItems.Add(Item(Liters(ml), () => Set(() => st.WaterGoalMl = ml), st.WaterGoalMl == ml));
        if (st.Koi)
        {
            water.DropDownItems.Add(new Forms.ToolStripSeparator());
            water.DropDownItems.Add(Item(Strings.Get("koi.share"), _island.ShareKoi));
        }
        menu.Items.Add(water);

        var lang = new Forms.ToolStripMenuItem(Strings.Get("tray.lang"));
        foreach (var (code, name) in new[] { ("ru", "Русский"), ("en", "English"), ("tg", "Тоҷикӣ") })
            lang.DropDownItems.Add(Item(name, () => Set(() => { Strings.Lang = code; st.Lang = code; }), Strings.Lang == code));
        menu.Items.Add(lang);

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Item(Strings.Get("tray.autostart"), () => { Autostart.Set(!Autostart.Enabled); Rebuild(); }, Autostart.Enabled));
        menu.Items.Add(Item(Strings.Get("tray.autoupdate"), () => Set(() => st.AutoUpdate = !st.AutoUpdate), st.AutoUpdate));
        menu.Items.Add(Item(Strings.Get("tray.check"), () => _app.CheckUpdates(manual: true)));
        menu.Items.Add(Item(Strings.Get("tray.site"), () => Process.Start(new ProcessStartInfo(App.Site) { UseShellExecute = true })));
        menu.Items.Add(Item(Strings.Get("tray.exit"), () => Application.Current.Shutdown()));

        var old = _icon.ContextMenuStrip;
        _icon.ContextMenuStrip = menu;
        old?.Dispose();
    }

    // Поменять настройку, применить к острову и обновить галочки
    void Set(Action change)
    {
        change();
        _island.ApplySettings();
        Rebuild();
    }

    // Действие выполняется после закрытия меню: так можно спокойно пересобрать само меню
    static Forms.ToolStripMenuItem Item(string text, Action action, bool? check = null)
    {
        var item = new Forms.ToolStripMenuItem(text) { Checked = check == true };
        item.Click += (_, _) => Application.Current.Dispatcher.BeginInvoke(action);
        return item;
    }

    static string Liters(int ml) => (ml / 1000.0).ToString("0.#", Strings.Culture) + " " + Strings.Get("l");

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
