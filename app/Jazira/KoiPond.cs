using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Jazira;

// Пруд с рыбками-кои. Уровень воды — как давно вы пили: только что — пруд полный,
// через несколько часов вода мелеет, а рыбка вяло плавает у дна. Выпили — всплеск и пузырьки.
// Рыбка рисуется кодом: голова плывёт по «восьмёрке», тело тянется по её следу, хвост машет.
// Рыбок может быть две (питомец открыл вторую), у каждой своя окраска, размер и свой путь.
sealed class KoiPond : FrameworkElement
{
    public static bool Still; // снимки (--snapshot): один неподвижный кадр, без таймера

    public bool Compact { get; set; }   // маленькая рыбка в свёрнутом острове, без пруда
    public bool Outline { get; set; }   // тонкий контур — для светлого острова
    public bool Sad { get; set; }       // пропустили день: рыбка бледнее, медленнее и держится у дна

    double _level = 0.8, _levelShown = 0.8;
    public double Level
    {
        get => _level;
        set { _level = Math.Clamp(value, 0, 1); if (Still) _levelShown = _level; InvalidateVisual(); }
    }

    public double Energy { get; set; } = 0.6; // 0 — вялая, 1 — бодрая
    public double Corner { get; set; } = 14;  // скругление пруда
    public double Lines { get; set; } = 1;    // толщина линий воды и пузырьков: на крупной картинке тоньше

    // Одна рыбка: свой след, свой ритм и своя «восьмёрка»
    sealed class Fish
    {
        public KoiKind Kind;
        public double Growth;                       // 0 — малёк, 1 — взрослая
        public readonly List<Point> Trail = new();  // путь головы: тело тянется по нему
        public double Phase, Swim;
        public readonly int Index;
        public Fish(int index) { Index = index; Phase = index * 2.3; Swim = index * 1.7; }
    }

    readonly List<Fish> _fish = new() { new Fish(0) };

    // Кто плавает в пруду: главная рыбка и, если открыта, вторая
    public void SetFish(KoiKind kind, double growth, KoiKind? second = null, double secondGrowth = 0)
    {
        int want = second.HasValue ? 2 : 1;
        while (_fish.Count > want) _fish.RemoveAt(_fish.Count - 1);
        while (_fish.Count < want) _fish.Add(new Fish(_fish.Count));
        _fish[0].Kind = kind;
        _fish[0].Growth = Math.Clamp(growth, 0, 1);
        if (second is KoiKind k2) { _fish[1].Kind = k2; _fish[1].Growth = Math.Clamp(secondGrowth, 0, 1); }
        InvalidateVisual();
    }

    readonly DispatcherTimer _timer = new(DispatcherPriority.Render);
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly List<(Point P, double R, double Born)> _bubbles = new();
    double _last, _splashUntil = -1, _nextBubble;

    // Экран заблокирован — рыбку никто не видит, кадры не рисуем
    static bool _locked;

    static KoiPond()
    {
        Microsoft.Win32.SystemEvents.SessionSwitch += (_, e) =>
        {
            if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock) _locked = true;
            else if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock) _locked = false;
        };
    }

    public KoiPond()
    {
        _timer.Tick += (_, _) => Step();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible && !Still)
            {
                // Маленькой рыбке хватает 10 кадров в секунду — остров почти не тратит процессор
                _timer.Interval = TimeSpan.FromMilliseconds(Compact ? 100 : 33);
                _last = Now;
                _timer.Start();
            }
            else _timer.Stop();
        };
    }

    double Now => Still ? 2.0 : _clock.Elapsed.TotalSeconds;

    public void Splash()
    {
        _splashUntil = Now + 2.6;
        _nextBubble = 0;
    }

    void Step()
    {
        double now = Now, dt = Math.Min(0.1, now - _last);
        _last = now;
        if (_locked) return;
        Advance(now, dt);
        InvalidateVisual();
    }

    void Advance(double now, double dt)
    {
        bool splash = now < _splashUntil;
        double energy = Sad ? Math.Min(Energy, 0.35) : Energy;
        foreach (var f in _fish)
        {
            // Рыбка никогда не стоит: то разгоняется, то скользит — два медленных колебания дают «живой» ритм
            double t = now + f.Index * 4.1;
            double glide = 0.55 + 0.45 * (0.5 + 0.5 * Math.Sin(t * 0.37) * Math.Sin(t * 0.23 + 1));
            double pace = f.Index == 0 ? 1 : 0.86; // вторая чуть медленнее — плывут не в ногу
            f.Phase += dt * pace * glide * (0.35 + 0.75 * energy + (splash ? 1.3 : 0));
            f.Swim += dt * (0.6 + 0.4 * glide) * (4 + 6 * energy + (splash ? 6 : 0)); // взмахи хвоста

            var head = Head(f);
            if (f.Trail.Count == 0 || Dist(f.Trail[^1], head) > 0.4)
            {
                f.Trail.Add(head);
                if (f.Trail.Count > 240) f.Trail.RemoveAt(0);
            }
        }
        _levelShown += (_level - _levelShown) * Math.Min(1, dt * 2.5);

        if (!Compact && (splash || energy > 0.85) && now >= _nextBubble)
        {
            _nextBubble = now + (splash ? 0.22 : 1.8);
            var head = Head(_fish[(int)(now * 3) % _fish.Count]);
            _bubbles.Add((new Point(head.X, head.Y - 3), 0.9 + now * 7 % 1.1, now));
        }
        for (int i = _bubbles.Count - 1; i >= 0; i--)
        {
            var b = _bubbles[i];
            var p = new Point(b.P.X + Math.Sin((now - b.Born) * 6) * 0.15, b.P.Y - dt * 14);
            if (p.Y < SurfaceY(p.X, now) + 1.5) _bubbles.RemoveAt(i);
            else _bubbles[i] = (p, b.R, b.Born);
        }
    }

    // Где плавает рыбка: в пруду — под поверхностью (грустная — у дна), в острове — узкая полоска посередине
    Rect Area(Fish f)
    {
        double w = ActualWidth, h = ActualHeight;
        if (Compact)
        {
            // Две рыбки в тесном острове плывут каждая своей дорожкой — иначе сливаются в комок
            if (_fish.Count < 2) return new Rect(7, h / 2 - 2, Math.Max(1, w - 14), 4);
            return new Rect(7, f.Index == 0 ? h / 2 - 5 : h / 2 + 2, Math.Max(1, w - 14), 3);
        }
        double top = SurfaceBase() + 9, bottom = h - 9;
        if (Sad) top = Math.Max(top, bottom - (bottom - top) * 0.45);
        if (bottom - top < 6) top = bottom - 6;
        return new Rect(12, top, Math.Max(1, w - 24), bottom - top);
    }

    double SurfaceBase() => ActualHeight * (1 - (0.2 + 0.7 * _levelShown));
    double SurfaceY(double x, double now) => SurfaceBase() + Math.Sin(x * 0.18 + now * 2.2) * 1.1;

    Point Head(Fish f)
    {
        var a = Area(f);
        double p = f.Phase;
        // У второй рыбки своя, незамкнутая «восьмёрка» — рыбки то сближаются, то расходятся
        double x = f.Index == 0 ? Math.Sin(p) : Math.Sin(0.83 * p + 2.4);
        double y = f.Index == 0 ? Math.Sin(2 * p + 0.6) : Math.Sin(1.71 * p + 1.3);
        return new Point(a.X + a.Width / 2 * (1 + x), a.Y + a.Height / 2 * (1 + y));
    }

    // Первый кадр: рыбки «проплывают» пару секунд, чтобы тело успело вытянуться по следу
    void Prime(double now)
    {
        for (int i = 0; i < 90; i++) Advance(now, 1 / 30.0);
    }

    // Для картинки: «проплыть» до красивого момента — рыбки вытянуты, не у самого края,
    // а если их две, то подальше друг от друга. Пруд должен быть уже размечен (Measure/Arrange).
    public void PoseApart()
    {
        const int steps = 900;
        int best = 120;
        double top = double.MinValue;
        var center = new Point(ActualWidth / 2, ActualHeight * 0.58);
        double diag = Math.Max(1, Math.Sqrt(ActualWidth * ActualWidth + ActualHeight * ActualHeight));
        Reset();
        for (int i = 1; i <= steps; i++)
        {
            Advance(i / 30.0, 1 / 30.0);
            if (i < 120) continue;
            double score = 0, off = 0;
            foreach (var f in _fish)
            {
                var spine = Spine(f.Trail, Radii.Length, 3.0 * SizeOf(f));
                double length = 3.0 * SizeOf(f) * (Radii.Length - 1);
                score += Dist(spine[0], spine[^1]) / length;                       // 1 — рыбка ровная
                off = Math.Max(off, Dist(spine[spine.Count / 2], center) / diag);  // 0 — в середине пруда
            }
            score = score / _fish.Count - off * (_fish.Count == 1 ? 1.6 : 0.5);
            if (_fish.Count > 1) score += 1.2 * Dist(Head(_fish[0]), Head(_fish[1])) / diag;
            if (score > top) { top = score; best = i; }
        }
        Reset();
        for (int i = 1; i <= best; i++) Advance(i / 30.0, 1 / 30.0);
        InvalidateVisual();

        void Reset()
        {
            for (int i = 0; i < _fish.Count; i++) { var f = _fish[i]; f.Trail.Clear(); f.Phase = i * 2.3; f.Swim = i * 1.7; }
            _bubbles.Clear();
            _nextBubble = 0;
            _levelShown = _level;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double now = Now;
        if (_fish.Exists(f => f.Trail.Count < 2)) Prime(now);

        if (!Compact)
        {
            var r = new Rect(0, 0, w, h);
            dc.PushClip(new RectangleGeometry(r, Corner, Corner));
            dc.DrawRectangle(PondBg, null, r);
            var water = new StreamGeometry();
            using (var c = water.Open())
            {
                c.BeginFigure(new Point(0, SurfaceY(0, now)), true, true);
                for (double x = 4; x <= w + 4; x += 4) c.LineTo(new Point(x, SurfaceY(x, now)), true, true);
                c.LineTo(new Point(w, h), false, false);
                c.LineTo(new Point(0, h), false, false);
            }
            dc.DrawGeometry(WaterFill, null, water);
            var surface = new StreamGeometry();
            using (var c = surface.Open())
            {
                c.BeginFigure(new Point(0, SurfaceY(0, now)), false, false);
                for (double x = 4; x <= w + 4; x += 4) c.LineTo(new Point(x, SurfaceY(x, now)), true, true);
            }
            var surfacePen = Lines == 1 ? SurfacePen : new Pen(SurfacePen.Brush, SurfacePen.Thickness * Lines);
            var bubblePen = Lines == 1 ? BubblePen : new Pen(BubblePen.Brush, BubblePen.Thickness * Lines);
            dc.DrawGeometry(null, surfacePen, surface);
            foreach (var b in _bubbles) dc.DrawEllipse(null, bubblePen, b.P, b.R, b.R);
        }

        // Вторая рыбка — под первой
        for (int i = _fish.Count - 1; i >= 0; i--) DrawFish(dc, _fish[i]);
        if (!Compact) dc.Pop();
    }

    static readonly double[] Radii = { 2.5, 2.8, 2.75, 2.5, 2.15, 1.75, 1.35, 1.0, 0.75 };

    // Размер: малёк заметно меньше взрослой рыбки, в свёрнутом острове разница скромнее — там тесно
    double SizeOf(Fish f) => Compact ? 0.72 * (0.86 + 0.18 * f.Growth) : 0.8 + 0.34 * f.Growth;

    void DrawFish(DrawingContext dc, Fish fish)
    {
        double s = SizeOf(fish);
        var spine = Spine(fish.Trail, Radii.Length, 3.0 * s);
        if (spine.Count < 2) return;
        int n = spine.Count;

        // Волна по телу: у головы почти нет, к хвосту сильнее
        var pts = new Point[n];
        var dirs = new Vector[n];
        for (int i = 0; i < n; i++)
        {
            Vector d = i == 0 ? spine[0] - spine[1] : spine[i - 1] - spine[i];
            if (d.Length < 1e-6) d = i > 0 ? dirs[i - 1] : new Vector(1, 0);
            else d.Normalize();
            dirs[i] = d;
            double wave = Math.Sin(fish.Swim - i * 0.85) * 0.9 * s * i / (n - 1);
            pts[i] = spine[i] + new Vector(-d.Y, d.X) * wave;
        }

        var look = Palette.Of(fish.Kind, dull: Energy < 0.3 || Sad);

        // Хвост
        var tail = pts[n - 1];
        var td = dirs[n - 1];
        var tn = new Vector(-td.Y, td.X);
        double sway = Math.Sin(fish.Swim - n * 0.85) * 1.4 * s;
        var fin = new StreamGeometry();
        using (var c = fin.Open())
        {
            c.BeginFigure(tail + td * 0.8 * s, true, true);
            c.LineTo(tail - td * 5.2 * s + tn * (3.4 * s + sway), true, true);
            c.LineTo(tail - td * 3.4 * s + tn * sway * 0.5, true, true);
            c.LineTo(tail - td * 5.2 * s - tn * (3.4 * s - sway), true, true);
        }
        dc.DrawGeometry(look.Fin, Outline ? OutlinePen : null, fin);

        // Грудные плавники
        var p2 = pts[2];
        var d2 = dirs[2];
        var n2 = new Vector(-d2.Y, d2.X);
        double flap = 0.6 + 0.4 * Math.Sin(fish.Swim * 0.7);
        foreach (var side in new[] { 1.0, -1.0 })
        {
            var root = p2 + n2 * side * Radii[2] * s * 0.8;
            var f = new StreamGeometry();
            using (var c = f.Open())
            {
                c.BeginFigure(root + d2 * 1.2 * s, true, true);
                c.LineTo(root + n2 * side * 3.2 * s * flap - d2 * 1.8 * s, true, true);
                c.LineTo(root - d2 * 1.4 * s, true, true);
            }
            dc.DrawGeometry(look.Fin, null, f);
        }

        // Тело — один гладкий контур по точкам спины; узор рисуется внутри него и обрезается по краю
        var body = BodyOutline(pts, dirs, s);
        if (Outline) dc.DrawGeometry(null, OutlineRim, body);
        dc.DrawGeometry(look.Body, null, body);
        dc.PushClip(body);

        Vector Side(int i) => new(-dirs[i].Y, dirs[i].X);
        if (look.Shine != null)
        {
            // Огон: металлический блеск сплошной полосой вдоль спины, к хвосту тоньше
            var front = new StreamGeometry();
            using (var c = front.Open()) { c.BeginFigure(pts[0], false, false); for (int i = 1; i <= 3; i++) c.LineTo(pts[i], true, true); }
            var back = new StreamGeometry();
            using (var c = back.Open()) { c.BeginFigure(pts[3], false, false); for (int i = 4; i <= 6; i++) c.LineTo(pts[i], true, true); }
            dc.DrawGeometry(null, RoundPen(look.Shine, Radii[1] * s * 0.85), front);
            dc.DrawGeometry(null, RoundPen(look.Shine, Radii[5] * s * 0.7), back);
        }
        if (look.Spot != null)
        {
            // Красные пятна кохаку и санкэ
            dc.DrawEllipse(look.Spot, null, pts[1] + dirs[1] * 0.3 * s, Radii[1] * s * 0.72, Radii[1] * s * 0.72);
            dc.DrawEllipse(look.Spot, null, pts[4] + Side(4) * 0.5 * s, Radii[4] * s * 0.8, Radii[4] * s * 0.8);
            dc.DrawEllipse(look.Spot, null, pts[5] - Side(5) * 0.3 * s, Radii[5] * s * 0.6, Radii[5] * s * 0.6);
        }
        if (look.Sumi != null)
        {
            // Чёрные мазки санкэ — третий цвет
            dc.DrawEllipse(look.Sumi, null, pts[3] - Side(3) * 1.2 * s, Radii[3] * s * 0.38, Radii[3] * s * 0.38);
            dc.DrawEllipse(look.Sumi, null, pts[6] + Side(6) * 0.4 * s, Radii[6] * s * 0.5, Radii[6] * s * 0.5);
            dc.DrawEllipse(look.Sumi, null, pts[2] + Side(2) * 1.5 * s, Radii[2] * s * 0.26, Radii[2] * s * 0.26);
        }
        dc.Pop();

        // Глаза
        if (!Compact)
        {
            var hd = dirs[0];
            var hn = new Vector(-hd.Y, hd.X);
            foreach (var side in new[] { 1.0, -1.0 })
                dc.DrawEllipse(Eye, null, pts[0] + hd * 1.1 * s + hn * side * 1.6 * s, 0.55, 0.55);
        }
    }

    // Контур тела: нос, левый бок, кончик хвоста, правый бок — и гладкая кривая через эти точки
    static StreamGeometry BodyOutline(Point[] pts, Vector[] dirs, double s)
    {
        int n = pts.Length;
        var ring = new List<Point>(2 * n + 4);
        var hd = dirs[0];
        var hn = new Vector(-hd.Y, hd.X);
        double r0 = Radii[0] * s;
        ring.Add(pts[0] + hd * r0);                                  // нос
        ring.Add(pts[0] + (hd * 0.72 + hn * 0.72) * r0);             // скругление морды
        for (int i = 0; i < n; i++) ring.Add(pts[i] + new Vector(-dirs[i].Y, dirs[i].X) * Radii[i] * s);
        ring.Add(pts[n - 1] - dirs[n - 1] * Radii[n - 1] * s);       // кончик хвоста
        for (int i = n - 1; i >= 0; i--) ring.Add(pts[i] - new Vector(-dirs[i].Y, dirs[i].X) * Radii[i] * s);
        ring.Add(pts[0] + (hd * 0.72 - hn * 0.72) * r0);

        // Замкнутый сплайн Катмулла — Рома через все точки, переведённый в кривые Безье
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            int m = ring.Count;
            c.BeginFigure(ring[0], true, true);
            for (int i = 0; i < m; i++)
            {
                Point p0 = ring[(i - 1 + m) % m], p1 = ring[i], p2 = ring[(i + 1) % m], p3 = ring[(i + 2) % m];
                c.BezierTo(p1 + (p2 - p0) / 6, p2 - (p3 - p1) / 6, p2, true, true);
            }
        }
        g.Freeze();
        return g;
    }

    // Окраска: тело, красные пятна, чёрные мазки, золотой блеск, плавники; бледная — когда рыбке плохо
    sealed record Palette(Brush Body, Brush? Spot, Brush? Sumi, Brush? Shine, Brush Fin)
    {
        static readonly Palette Kohaku = new(B("#F7F2E8"), B("#F2582A"), null, null, B("#A6FFFFFF"));
        static readonly Palette KohakuDull = new(B("#CFC9BF"), B("#B0735A"), null, null, B("#A6FFFFFF"));
        static readonly Palette Sanke = new(B("#F7F2E8"), B("#F2582A"), B("#232328"), null, B("#A6FFFFFF"));
        static readonly Palette SankeDull = new(B("#CFC9BF"), B("#B0735A"), B("#55555A"), null, B("#A6FFFFFF"));
        static readonly Palette Ogon = new(B("#F2B23A"), null, null, B("#FFE6A8"), B("#B3FFE3A6"));
        static readonly Palette OgonDull = new(B("#C9A867"), null, null, B("#DDCDA4"), B("#99F0E6D2"));

        public static Palette Of(KoiKind kind, bool dull) => kind switch
        {
            KoiKind.Sanke => dull ? SankeDull : Sanke,
            KoiKind.Ogon => dull ? OgonDull : Ogon,
            _ => dull ? KohakuDull : Kohaku,
        };
    }

    // Точки тела: идём назад по следу головы через равные промежутки
    static List<Point> Spine(List<Point> trail, int count, double seg)
    {
        var pts = new List<Point>(count);
        if (trail.Count == 0) return pts;
        pts.Add(trail[^1]);
        double need = seg, acc = 0;
        for (int i = trail.Count - 1; i > 0 && pts.Count < count; i--)
        {
            Point a = trail[i], b = trail[i - 1];
            double d = Dist(a, b);
            if (d < 1e-6) continue;
            while (need <= acc + d && pts.Count < count)
            {
                double t = (need - acc) / d;
                pts.Add(new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
                need += seg;
            }
            acc += d;
        }
        while (pts.Count < count) pts.Add(pts[^1]); // след ещё короткий — хвост «сложен»
        return pts;
    }

    static double Dist(Point a, Point b) => (a - b).Length;

    static Pen RoundPen(Brush b, double width) =>
        new(b, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

    static SolidColorBrush B(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }

    static readonly Brush PondBg = Frozen(new LinearGradientBrush(Color.FromRgb(0x13, 0x33, 0x3E), Color.FromRgb(0x08, 0x16, 0x1C), 90));
    static readonly Brush WaterFill = Frozen(new LinearGradientBrush(Color.FromArgb(0x90, 0x3A, 0xA6, 0xE8), Color.FromArgb(0xD0, 0x12, 0x58, 0x96), 90));
    static readonly Pen SurfacePen = Frozen(new Pen(B("#B3C8EEFF"), 1.2));
    static readonly Pen BubblePen = Frozen(new Pen(B("#D9FFFFFF"), 0.8));
    static readonly Pen OutlinePen = Frozen(new Pen(B("#4D000000"), 0.6));
    static readonly Pen OutlineRim = Frozen(new Pen(B("#40000000"), 1.4) { LineJoin = PenLineJoin.Round });
    static readonly Brush Eye = B("#1B1B1B");
}
