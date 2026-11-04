using System;
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
    bool _typing;

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

        // Сначала несделанные: с временем — по времени, без времени — в порядке добавления; сделанные — в конце
        var list = st.Tasks.Where(t => !t.Done).OrderBy(t => t.Due ?? DateTime.MaxValue)
                     .Concat(st.Tasks.Where(t => t.Done).OrderBy(t => t.DoneAt)).ToList();
        TaskList.Children.Clear();
        if (list.Count == 0 && TaskInputRow.Visibility != Visibility.Visible)
            TaskList.Children.Add(new TextBlock { Text = Strings.Get("day.empty"), FontSize = 12, Foreground = Res("Dim"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 2) });
        foreach (var t in list.Take(TasksShown)) TaskList.Children.Add(TaskRow(t, now));
        if (list.Count > TasksShown)
            TaskList.Children.Add(new TextBlock { Text = Strings.Format("day.more", list.Count - TasksShown), FontSize = 11, Foreground = Res("Faint"), Margin = new Thickness(24, 2, 0, 0) });
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
