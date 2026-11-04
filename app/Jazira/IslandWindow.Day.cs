using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfPath = System.Windows.Shapes.Path;

namespace Jazira;

// «Мой день»: задачи с напоминаниями (время понимается прямо из текста) и кухонные таймеры рядом с помодоро.
// Поле ввода — единственное место, где острову нужна клавиатура: на время набора он становится обычным
// активным окном, а после Enter, Esc или клика мимо снова не отнимает фокус у ваших окон.
public partial class IslandWindow
{
    const int TasksShown = 5, TimersMax = 4;
    static readonly int[] PresetMinutes = { 5, 10, 15, 30 };
    static readonly Geometry CheckGeo = G("M3 8.5l3.2 3.2L13 5");
    static readonly Geometry TaskGeo = G("M5 4h14a1.5 1.5 0 0 1 1.5 1.5v13A1.5 1.5 0 0 1 19 20H5a1.5 1.5 0 0 1-1.5-1.5v-13A1.5 1.5 0 0 1 5 4z M7 9.2l1.6 1.6L11.8 7.6l1.2 1.2-4.4 4.4L5.8 10.4z M14 9h4v1.8h-4z M7 14.5h11v1.8H7z");
    static readonly Geometry CalendarGeo = G("M7 2h2v2h6V2h2v2h2.5A1.5 1.5 0 0 1 21 5.5v14a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 19.5v-14A1.5 1.5 0 0 1 4.5 4H7z M5 9v10h14V9z M7 11h3v3H7z");
    bool _typing;
    readonly CalendarService _cal = new();
    DateTime _calNext;                                  // следующее обновление календаря
    readonly HashSet<string> _alerted = new();          // какие напоминания о встречах и Рамазане уже показаны
    bool? _fakeRamadan;                                 // снимки: «сейчас Рамазан»

    // ---- Задачи --------------------------------------------------------------------------------

    void RefreshDay()
    {
        var st = AppState.Current;
        var now = DateTime.Now;
        st.Tasks.RemoveAll(t => t.Done && t.DoneAt is DateTime d && d.Date < now.Date); // сделанное вчера уходит само
        DayTitle.Text = $"{Strings.Get("day.title")} · {now.ToString("ddd, d MMMM", Strings.DateCulture)}";
        TaskAddText.Text = Strings.Get("day.add");
        TaskHint.Text = Strings.Get("day.hint");
        TaskHint.Visibility = TaskInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        RefreshDayTop(now);

        // Сначала несделанные: с временем — по времени, без времени — в порядке добавления; сделанные — в конце
        var list = st.Tasks.Where(t => !t.Done).OrderBy(t => t.Due ?? DateTime.MaxValue)
                     .Concat(st.Tasks.Where(t => t.Done).OrderBy(t => t.DoneAt)).ToList();
        TaskList.Children.Clear();
        if (list.Count == 0 && TaskInputRow.Visibility != Visibility.Visible)
            TaskList.Children.Add(new TextBlock { Text = Strings.Get("day.empty"), FontSize = 12, Foreground = Res("Dim"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 2) });
        // Карточка встречи или Рамазана занимает место двух задач — остров не должен вылезти за окно
        int shown = Math.Max(2, TasksShown - 2 * DayTop.Children.Count - (TaskInputRow.Visibility == Visibility.Visible ? 1 : 0));
        if (list.Count == shown + 1) shown++; // «и ещё 1» не короче самой задачи
        foreach (var t in list.Take(shown)) TaskList.Children.Add(TaskRow(t, now));
        if (list.Count > shown)
            TaskList.Children.Add(new TextBlock { Text = Strings.Format("day.more", list.Count - shown), FontSize = 11, Foreground = Res("Faint"), Margin = new Thickness(24, 2, 0, 0) });
    }

    FrameworkElement TaskRow(TaskItem t, DateTime now)
    {
        var row = new Grid { Height = 26, Background = Brushes.Transparent, Cursor = Cursors.Hand };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var box = new Border
        {
            Width = 16, Height = 16, CornerRadius = new CornerRadius(8), VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(t.Done ? 0 : 1.5), BorderBrush = Res("Dim"),
            Background = t.Done ? Res("Lagoon") : Brushes.Transparent,
        };
        if (t.Done)
            box.Child = new WpfPath { Data = CheckGeo, Width = 9, Height = 8, Stretch = Stretch.Uniform, Stroke = Brushes.White, StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        row.Children.Add(box);

        var text = new TextBlock
        {
            Text = t.Text, FontSize = 12.5, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis, Foreground = t.Done ? Res("Faint") : Res("Fg"),
            TextDecorations = t.Done ? TextDecorations.Strikethrough : null,
        };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        if (t.Due is DateTime due && !t.Done)
        {
            bool late = due <= now;
            var when = new TextBlock
            {
                Text = TaskParser.Short(due, now), FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
                Foreground = late ? Res("Coral") : Res("Dim"), Margin = new Thickness(0, 0, 4, 0),
            };
            Grid.SetColumn(when, 2);
            row.Children.Add(when);
        }

        var remove = new Button { Style = (Style)FindResource("IconBtn"), Width = 20, Height = 20, Opacity = 0.55, VerticalAlignment = VerticalAlignment.Center };
        remove.Content = new WpfPath { Data = CrossGeo, Width = 7, Height = 7, Stretch = Stretch.Uniform, Stroke = Res("Fg"), StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        remove.Click += (_, e) => { e.Handled = true; RemoveTask(t.Id); };
        Grid.SetColumn(remove, 3);
        row.Children.Add(remove);

        row.MouseLeftButtonUp += (_, e) => { if (e.OriginalSource is not DependencyObject d || !IsInside(d, remove)) ToggleTask(t.Id); };
        return row;
    }

    static bool IsInside(DependencyObject d, DependencyObject parent)
    {
        for (var x = d; x != null; x = VisualTreeHelper.GetParent(x)) if (x == parent) return true;
        return false;
    }

    void ToggleTask(string id)
    {
        var t = AppState.Current.Tasks.FirstOrDefault(x => x.Id == id);
        if (t == null) return;
        t.Done = !t.Done;
        t.DoneAt = t.Done ? DateTime.Now : null;
        AppState.Save();
        RefreshDay();
    }

    void RemoveTask(string id)
    {
        AppState.Current.Tasks.RemoveAll(x => x.Id == id);
        AppState.Save();
        RefreshDay();
        ResizeDay();
    }

    void AddTask(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        var (text, due) = TaskParser.Parse(raw, DateTime.Now);
        var list = AppState.Current.Tasks;
        list.Add(new TaskItem { Text = text, Due = due });
        while (list.Count > 40) list.RemoveAt(list.FindIndex(x => x.Done) is int i and >= 0 ? i : 0); // не копим бесконечно
        AppState.Save();
    }

    // Напоминания: раз в секунду; каждая задача звенит один раз
    void CheckTasks(DateTime now)
    {
        var st = AppState.Current;
        var due = st.Tasks.Where(t => !t.Done && !t.Reminded && t.Due is DateTime d && d <= now).ToList();
        if (due.Count == 0) return;
        foreach (var t in due) t.Reminded = true;
        AppState.Save();
        var last = due.OrderBy(t => t.Due).Last();
        Chime.Play();
        Alert("task", Res("Violet"), TaskGeo, last.Text, Strings.Format("task.remind", TaskParser.Short(last.Due!.Value, now)), 15,
              Strings.Get("task.done"), () => { last.Done = true; last.DoneAt = DateTime.Now; AppState.Save(); RefreshDay(); });
        if (_mode == Mode.Expanded && PageName == "day") RefreshDay();
    }

    // Важное уведомление (напоминание, таймер): открытый остров ради него сворачивается, если вы не печатаете
    void Alert(string kind, Brush bg, Geometry icon, string title, string sub, double seconds, string? action = null, Action? onAction = null)
    {
        if (_mode == Mode.Expanded)
        {
            if (_typing) return;
            Collapse();
        }
        ShowPeek(kind, bg, icon, title, sub, seconds: seconds, action: action, onAction: onAction);
    }

    // ---- Встреча из календаря и Рамазан — карточки над задачами ------------------------------

    void RefreshDayTop(DateTime now)
    {
        DayTop.Children.Clear();
        if (RamadanNow(now) is var (isIftar, at, day))
            DayTop.Children.Add(DayCard(MoonGeo, Res("Violet"),
                Strings.Format(isIftar ? "ramadan.iftar" : "ramadan.suhoor", at.ToString("H:mm", CultureInfo.InvariantCulture)),
                Strings.Format("in", Duration(at - now)) + " · " + Strings.Format("ramadan.day", day), null, null));

        if (AppState.Current.CalendarUrl == null) return;
        if (_cal.Next(now) is CalEvent e)
        {
            var range = $"{e.Start:H:mm}–{e.End:H:mm}";
            var sub = e.Start <= now ? Strings.Format("cal.now", e.End.ToString("H:mm", CultureInfo.InvariantCulture))
                                     : $"{range} · {Strings.Format("in", Duration(e.Start - now))}";
            DayTop.Children.Add(DayCard(CalendarGeo, Res("Sky"), e.Title, sub, e.Link != null ? Strings.Get("cal.join") : null, e.Link));
        }
        else
            DayTop.Children.Add(DayCard(CalendarGeo, Res("Muted"), Strings.Get(_cal.Failed ? "cal.failed" : _cal.LoadedAt == null ? "cal.waiting" : "cal.empty"), "", null, null));
    }

    FrameworkElement DayCard(Geometry icon, Brush accent, string title, string sub, string? action, string? link)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var badge = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = accent, VerticalAlignment = VerticalAlignment.Center };
        badge.Child = new WpfPath { Data = icon, Width = 12, Height = 12, Stretch = Stretch.Uniform, Fill = Brushes.White };
        grid.Children.Add(badge);
        var texts = new StackPanel { Margin = new Thickness(10, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = title, FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        if (sub.Length > 0) texts.Children.Add(new TextBlock { Text = sub, FontSize = 11, Foreground = Res("Dim"), TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);
        if (action != null && link != null)
        {
            var join = new Button { Style = (Style)FindResource("MiniPill"), Content = action, Background = Res("Sky"), Foreground = Brushes.Black, Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
            join.Click += (_, _) => OpenLink(link);
            Grid.SetColumn(join, 2);
            grid.Children.Add(join);
        }
        return new Border { Child = grid, Background = Res("Chip"), CornerRadius = new CornerRadius(12), Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 6, 0, 2) };
    }

    static void OpenLink(string link)
    {
        if (!link.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        try { Process.Start(new ProcessStartInfo(link) { UseShellExecute = true }); }
        catch (Exception ex) { App.Log(ex); }
    }

    // ---- Календарь ----------------------------------------------------------------------------

    void CheckCalendar(DateTime now)
    {
        var url = AppState.Current.CalendarUrl;
        if (url == null || _instant) return;
        if (now >= _calNext)
        {
            _calNext = now.AddMinutes(10);
            _ = _cal.RefreshAsync(url);
        }
        foreach (var e in _cal.Events.Where(e => !e.AllDay && e.Start > now.AddMinutes(-2) && e.Start <= now.AddMinutes(10)))
        {
            var left = e.Start - now;
            var key = $"{e.Uid}|{e.Start:O}";
            if (left > TimeSpan.Zero && _alerted.Add(key + "|soon"))
            {
                Chime.Play();
                Alert("event", Res("Sky"), CalendarGeo, e.Title, $"{Strings.Format("in", Duration(left))} · {e.Start:H:mm}", 20,
                      e.Link != null ? Strings.Get("cal.join") : null, e.Link != null ? () => OpenLink(e.Link) : null);
            }
            else if (left <= TimeSpan.Zero && e.Link != null && _alerted.Add(key + "|start"))
                Alert("event", Res("Sky"), CalendarGeo, e.Title, Strings.Get("cal.starts"), 20, Strings.Get("cal.join"), () => OpenLink(e.Link));
        }
    }

    public string CalendarStatus() =>
        _cal.Failed ? Strings.Get("cal.failed")
        : _cal.LoadedAt is DateTime t ? Strings.Format("cal.connected", t.ToString("H:mm", CultureInfo.InvariantCulture))
        : Strings.Get("cal.waiting");

    // Ссылку берём из буфера обмена: так не нужно окно для ввода
    public void ConnectCalendar()
    {
        string? text = null;
        try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); }
        catch (Exception ex) { App.Log(ex); }
        var url = CalendarService.Normalize(text);
        if (url == null) { ShowInfo(Strings.Get("cal.nolink")); return; }
        AppState.Current.CalendarUrl = url;
        AppState.Save();
        _calNext = DateTime.MinValue;
        ShowInfo(Strings.Get("cal.ok"));
        CheckCalendar(DateTime.Now);
    }

    public void RefreshCalendar()
    {
        _calNext = DateTime.MinValue;
        CheckCalendar(DateTime.Now);
    }

    public void DisconnectCalendar()
    {
        AppState.Current.CalendarUrl = null;
        AppState.Save();
        _cal.Clear();
    }

    void OnCalendarChanged()
    {
        if (_mode == Mode.Expanded && PageName == "day" && !_typing) { RefreshDay(); ResizeDay(); }
        SettingsChanged?.Invoke(); // в меню — новое «обновлён в …»
    }

    // ---- Рамазан ------------------------------------------------------------------------------

    static readonly UmAlQuraCalendar Hijri = new();

    bool IsRamadan(DateTime d)
    {
        if (_fakeRamadan is bool f) return f;
        try { return Hijri.GetMonth(d) == 9; }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    int RamadanDay(DateTime d)
    {
        if (_fakeRamadan != null) return 12;
        try { return Hijri.GetDayOfMonth(d); }
        catch (ArgumentOutOfRangeException) { return 0; }
    }

    // До фаджра — сухур (последняя еда), днём — ифтар (разговение на закате), после заката — завтрашний сухур
    (bool Iftar, DateTime At, int Day)? RamadanNow(DateTime now)
    {
        if (!AppState.Current.Ramadan) return null;
        var city = LocalInfo.CityOf(AppState.Current.City);
        var today = TodayPrayers();
        DateTime fajr = today[0], maghrib = today[4];
        if (IsRamadan(now.Date))
        {
            if (now < fajr) return (false, fajr, RamadanDay(now.Date));
            if (now < maghrib) return (true, maghrib, RamadanDay(now.Date));
        }
        var tomorrow = now.Date.AddDays(1);
        if (IsRamadan(tomorrow))
            return (false, PrayerTimes.ForDate(tomorrow, city.Lat, city.Lon)[0].ToLocalTime(), RamadanDay(tomorrow));
        return null;
    }

    void CheckRamadan(DateTime now)
    {
        if (RamadanNow(now) is not var (iftar, at, _)) return;
        var left = at - now;
        var day = at.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var time = at.ToString("H:mm", CultureInfo.InvariantCulture);
        if (iftar && left <= TimeSpan.FromMinutes(15) && left > TimeSpan.FromMinutes(1) && _alerted.Add("iftar15|" + day))
            Alert("ramadan", Res("Violet"), MoonGeo, Strings.Get("ramadan.iftarSoon"), Strings.Format("ramadan.iftar", time), 15);
        else if (iftar && left <= TimeSpan.FromMinutes(1) && _alerted.Add("iftar|" + day))
        {
            Chime.Play();
            Alert("ramadan", Res("Violet"), MoonGeo, Strings.Get("ramadan.iftarNow"), Strings.Format("ramadan.iftar", time), 30);
        }
        else if (!iftar && left <= TimeSpan.FromMinutes(30) && left > TimeSpan.Zero && _alerted.Add("suhoor30|" + day))
            Alert("ramadan", Res("Violet"), MoonGeo, Strings.Get("ramadan.suhoorSoon"), Strings.Format("ramadan.suhoor", time), 20);
    }

    // ---- Ввод с клавиатуры --------------------------------------------------------------------

    void TaskAdd_Click(object sender, RoutedEventArgs e)
    {
        TaskInputRow.Visibility = Visibility.Visible;
        RefreshDay();
        ResizeDay();
        BeginTyping();
    }

    void TaskInput_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_typing) BeginTyping();
    }

    void TaskInput_TextChanged(object sender, TextChangedEventArgs e) =>
        TaskHint.Visibility = TaskInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    void TaskInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            AddTask(TaskInput.Text);
            TaskInput.Clear();
            RefreshDay();
            ResizeDay(); // поле остаётся открытым — можно сразу ввести следующую
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            EndTyping(hide: true);
        }
    }

    // Острову нужна клавиатура: снимаем «не активировать» и становимся обычным активным окном
    void BeginTyping()
    {
        if (_hwnd == IntPtr.Zero) return;
        _typing = true;
        _hoverOut.Stop();
        Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE) & ~Native.WS_EX_NOACTIVATE);
        Activate();
        TaskInput.Focus();
        Keyboard.Focus(TaskInput);
    }

    void EndTyping(bool hide)
    {
        if (_typing && _hwnd != IntPtr.Zero)
            Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE) | Native.WS_EX_NOACTIVATE);
        _typing = false;
        Keyboard.ClearFocus();
        if (hide || TaskInput.Text.Length == 0)
        {
            TaskInput.Clear();
            TaskInputRow.Visibility = Visibility.Collapsed;
        }
        RefreshDay();
        ResizeDay();
        if (_mode == Mode.Expanded && !CursorOverIsland()) _hoverOut.Start();
    }

    // Высота «Моего дня» зависит от числа задач и поля ввода
    void ResizeDay()
    {
        if (_mode == Mode.Expanded && PageName == "day") AnimateTo(PageSize(), gentle: true);
    }

    Size DaySize()
    {
        DayPanel.Measure(new Size(420, double.PositiveInfinity));
        return new Size(420, Math.Max(76, Math.Ceiling(DayPanel.DesiredSize.Height)));
    }

    // ---- Таймеры ------------------------------------------------------------------------------

    void RefreshTimers()
    {
        var st = AppState.Current;
        var now = DateTime.Now;
        st.Timers.RemoveAll(t => t.Done && now - t.Ends > TimeSpan.FromMinutes(2)); // «готово» висит пару минут
        TimerLabel.Text = Strings.Get("timer.label");
        TimerLabel.Visibility = st.Timers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        TimerChips.Children.Clear();
        foreach (var t in st.Timers.OrderBy(t => t.Ends))
        {
            var left = t.Ends - now;
            var label = new StackPanel { Orientation = Orientation.Horizontal };
            label.Children.Add(new TextBlock
            {
                Text = t.Done ? "✓ " + Strings.Format("timer.min", t.Minutes) : Fmt(left > TimeSpan.Zero ? left : TimeSpan.Zero),
                VerticalAlignment = VerticalAlignment.Center,
            });
            label.Children.Add(new WpfPath { Data = CrossGeo, Width = 6, Height = 6, Stretch = Stretch.Uniform, Stroke = t.Done ? Brushes.Black : Res("Dim"), StrokeThickness = 1.5, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            var chip = new Button { Style = (Style)FindResource("MiniPill"), Content = label, Tag = t.Id };
            if (t.Done) { chip.Background = Res("Amber"); chip.Foreground = Brushes.Black; }
            else chip.Foreground = Res("Amber");
            chip.Click += (_, _) => { AppState.Current.Timers.RemoveAll(x => x.Id == (string)chip.Tag); AppState.Save(); RefreshTimers(); RefreshIdleRight(); };
            TimerChips.Children.Add(chip);
        }

        // Пресеты справа; чем больше идущих таймеров, тем меньше пресетов помещается в строку
        TimerPresets.Children.Clear();
        int chips = st.Timers.Count, running = st.Timers.Count(t => !t.Done);
        int shown = running >= TimersMax ? 0 : chips <= 2 ? 4 : chips == 3 ? 2 : 1;
        foreach (var m in PresetMinutes.Take(shown))
        {
            var b = new Button { Style = (Style)FindResource("MiniPill"), Content = "+" + (m == PresetMinutes[0] ? Strings.Format("timer.min", m) : m.ToString(Strings.Culture)) };
            int minutes = m;
            b.Click += (_, _) => StartTimer(minutes);
            TimerPresets.Children.Add(b);
        }
        if (TimerPresets.Children.Count > 0) ((Button)TimerPresets.Children[^1]).Margin = new Thickness(0);
    }

    void StartTimer(int minutes)
    {
        var list = AppState.Current.Timers;
        if (list.Count(t => !t.Done) >= TimersMax) return;
        list.Add(new TimerItem { Minutes = minutes, Ends = DateTime.Now.AddMinutes(minutes) });
        AppState.Save();
        RefreshTimers();
        RefreshIdleRight();
    }

    void CheckTimers(DateTime now)
    {
        var st = AppState.Current;
        var done = st.Timers.Where(t => !t.Done && t.Ends <= now).ToList();
        if (done.Count > 0)
        {
            foreach (var t in done) t.Done = true;
            AppState.Save();
            Chime.Play();
            var t0 = done[0];
            Alert("timer", Res("Amber"), WatchGeo, Strings.Format("timer.done", Strings.Format("timer.min", t0.Minutes)), "", 10);
        }
        if (_mode == Mode.Expanded && PageName == "focus") RefreshTimers();
    }

    // Ближайший идущий таймер — для правого края свёрнутого острова
    TimeSpan? SoonestTimer()
    {
        var now = DateTime.Now;
        var running = AppState.Current.Timers.Where(t => !t.Done).Select(t => t.Ends - now).ToList();
        return running.Count == 0 ? null : running.Min() is var m && m > TimeSpan.Zero ? m : TimeSpan.Zero;
    }
}
