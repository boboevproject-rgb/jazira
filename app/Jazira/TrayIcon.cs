using System;
using System.Diagnostics;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Jazira;

// Значок в трее: показать или спрятать остров, автозапуск, цель по воде, язык, выход.
sealed class TrayIcon : IDisposable
{
    readonly Forms.NotifyIcon _icon;
    readonly IslandWindow _island;

    public TrayIcon(IslandWindow island)
    {
        _island = island;
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

    // Меню собирается заново после каждого изменения — так галочки и язык всегда актуальны
    public void Rebuild()
    {
        var st = AppState.Current;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem($"Jazira {App.Version}") { Enabled = false });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Item(Strings.Get("tray.show"), ToggleIsland, st.ShowIsland));
        menu.Items.Add(Item(Strings.Get("tray.autostart"), () => { Autostart.Set(!Autostart.Enabled); Rebuild(); }, Autostart.Enabled));

        var water = new Forms.ToolStripMenuItem(Strings.Get("tray.water"));
        foreach (var ml in new[] { 1500, 2000, 2500, 3000 })
            water.DropDownItems.Add(Item(Liters(ml), () => { st.WaterGoalMl = ml; AppState.Save(); _island.RefreshAll(); Rebuild(); }, st.WaterGoalMl == ml));
        menu.Items.Add(water);

        var lang = new Forms.ToolStripMenuItem(Strings.Get("tray.lang"));
        foreach (var (code, name) in new[] { ("ru", "Русский"), ("en", "English"), ("tg", "Тоҷикӣ") })
            lang.DropDownItems.Add(Item(name, () => { Strings.Lang = code; st.Lang = code; AppState.Save(); _island.ApplyTexts(); Rebuild(); }, Strings.Lang == code));
        menu.Items.Add(lang);

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Item(Strings.Get("tray.site"), () => Process.Start(new ProcessStartInfo(App.Site) { UseShellExecute = true })));
        menu.Items.Add(Item(Strings.Get("tray.exit"), () => Application.Current.Shutdown()));

        var old = _icon.ContextMenuStrip;
        _icon.ContextMenuStrip = menu;
        old?.Dispose();
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
