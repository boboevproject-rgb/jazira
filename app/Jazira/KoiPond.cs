using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Jazira;

// Пруд с рыбкой-кои. Уровень воды — как давно вы пили: только что — пруд полный,
// через несколько часов вода мелеет, а рыбка вяло плавает у дна. Выпили — всплеск и пузырьки.
// Рыбка рисуется кодом: голова плывёт по «восьмёрке», тело тянется по её следу, хвост машет.
sealed class KoiPond : FrameworkElement
{
    public static bool Still; // снимки (--snapshot): один неподвижный кадр, без таймера

    public bool Compact { get; set; }   // маленькая рыбка в свёрнутом острове, без пруда
    public bool Outline { get; set; }   // тонкий контур — для светлого острова

    double _level = 0.8, _levelShown = 0.8;
    public double Level
    {
        get => _level;
        set { _level = Math.Clamp(value, 0, 1); if (Still) _levelShown = _level; InvalidateVisual(); }
    }

    public double Energy { get; set; } = 0.6; // 0 — вялая, 1 — бодрая

    readonly DispatcherTimer _timer = new(DispatcherPriority.Render);
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly List<Point> _trail = new(); // путь головы: тело тянется по нему
    readonly List<(Point P, double R, double Born)> _bubbles = new();
    double _phase, _last, _swim, _splashUntil = -1, _nextBubble;

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
        // Маленькая рыбка то плывёт, то замирает — как настоящая; пока замерла, остров не перерисовывается.
        // Если за компьютером никого нет больше минуты, рыбка спит.
        if (Compact && now >= _splashUntil && (now % 20 > 5 || Native.IdleSeconds() > 60)) return;
        Advance(now, dt);
        InvalidateVisual();
    }

    void Advance(double now, double dt)
    {
        bool splash = now < _splashUntil;
        _phase += dt * (0.35 + 0.75 * Energy + (splash ? 1.3 : 0));
        _swim += dt * (4 + 6 * Energy + (splash ? 6 : 0)); // взмахи хвоста
        _levelShown += (_level - _levelShown) * Math.Min(1, dt * 2.5);

        var head = Head();
        if (_trail.Count == 0 || Dist(_trail[^1], head) > 0.4)
        {
            _trail.Add(head);
            if (_trail.Count > 240) _trail.RemoveAt(0);
        }

        if (!Compact && (splash || Energy > 0.85) && now >= _nextBubble)
        {
            _nextBubble = now + (splash ? 0.22 : 1.8);
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

    // Где плавает рыбка: в пруду — под поверхностью, в острове — узкая полоска посередине
    Rect Area()
    {
        double w = ActualWidth, h = ActualHeight;
        if (Compact) return new Rect(7, h / 2 - 2, Math.Max(1, w - 14), 4);
        double top = SurfaceBase() + 9, bottom = h - 9;
        if (bottom - top < 6) top = bottom - 6;
        return new Rect(12, top, Math.Max(1, w - 24), bottom - top);
    }

    double SurfaceBase() => ActualHeight * (1 - (0.2 + 0.7 * _levelShown));
    double SurfaceY(double x, double now) => SurfaceBase() + Math.Sin(x * 0.18 + now * 2.2) * 1.1;

    Point Head()
    {
        var a = Area();
        return new Point(a.X + a.Width / 2 * (1 + Math.Sin(_phase)), a.Y + a.Height / 2 * (1 + Math.Sin(2 * _phase + 0.6)));
    }

    // Первый кадр: рыбка «проплывает» пару секунд, чтобы тело успело вытянуться по следу
    void Prime(double now)
    {
        for (int i = 0; i < 90; i++) Advance(now, 1 / 30.0);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double now = Now;
        if (_trail.Count < 2) Prime(now);

        if (!Compact)
        {
            var r = new Rect(0, 0, w, h);
            dc.PushClip(new RectangleGeometry(r, 14, 14));
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
            dc.DrawGeometry(null, SurfacePen, surface);
            foreach (var b in _bubbles) dc.DrawEllipse(null, BubblePen, b.P, b.R, b.R);
        }

        DrawFish(dc);
        if (!Compact) dc.Pop();
    }

    static readonly double[] Radii = { 2.5, 2.8, 2.75, 2.5, 2.15, 1.75, 1.35, 1.0, 0.75 };

    void DrawFish(DrawingContext dc)
    {
        double s = Compact ? 0.72 : 1.0;
        var spine = Spine(Radii.Length, 3.0 * s);
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
            double wave = Math.Sin(_swim - i * 0.85) * 0.9 * s * i / (n - 1);
            pts[i] = spine[i] + new Vector(-d.Y, d.X) * wave;
        }

        bool thirsty = Energy < 0.3;
        var body = thirsty ? BodyDull : Body;
        var spot = thirsty ? SpotDull : Spot;

        // Хвост
        var tail = pts[n - 1];
        var td = dirs[n - 1];
        var tn = new Vector(-td.Y, td.X);
        double sway = Math.Sin(_swim - n * 0.85) * 1.4 * s;
        var fin = new StreamGeometry();
        using (var c = fin.Open())
        {
            c.BeginFigure(tail + td * 0.8 * s, true, true);
            c.LineTo(tail - td * 5.2 * s + tn * (3.4 * s + sway), true, true);
            c.LineTo(tail - td * 3.4 * s + tn * sway * 0.5, true, true);
            c.LineTo(tail - td * 5.2 * s - tn * (3.4 * s - sway), true, true);
        }
        dc.DrawGeometry(Fin, Outline ? OutlinePen : null, fin);

        // Грудные плавники
        var p2 = pts[2];
        var d2 = dirs[2];
        var n2 = new Vector(-d2.Y, d2.X);
        double flap = 0.6 + 0.4 * Math.Sin(_swim * 0.7);
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
            dc.DrawGeometry(Fin, null, f);
        }

        // Тело: от хвоста к голове, кругами по следу
        if (Outline)
            for (int i = n - 1; i >= 0; i--) dc.DrawEllipse(OutlineFill, null, pts[i], Radii[i] * s + 0.7, Radii[i] * s + 0.7);
        for (int i = n - 1; i >= 0; i--) dc.DrawEllipse(body, null, pts[i], Radii[i] * s, Radii[i] * s);

        // Красные пятна кохаку
        dc.DrawEllipse(spot, null, pts[1] + dirs[1] * 0.3 * s, Radii[1] * s * 0.72, Radii[1] * s * 0.72);
        dc.DrawEllipse(spot, null, pts[4] + new Vector(-dirs[4].Y, dirs[4].X) * 0.5 * s, Radii[4] * s * 0.8, Radii[4] * s * 0.8);
        dc.DrawEllipse(spot, null, pts[5] - new Vector(-dirs[5].Y, dirs[5].X) * 0.3 * s, Radii[5] * s * 0.6, Radii[5] * s * 0.6);

        // Глаза
        if (!Compact)
        {
            var hd = dirs[0];
            var hn = new Vector(-hd.Y, hd.X);
            foreach (var side in new[] { 1.0, -1.0 })
                dc.DrawEllipse(Eye, null, pts[0] + hd * 1.1 * s + hn * side * 1.6 * s, 0.55, 0.55);
        }
    }

    // Точки тела: идём назад по следу головы через равные промежутки
    List<Point> Spine(int count, double seg)
    {
        var pts = new List<Point>(count);
        if (_trail.Count == 0) return pts;
        pts.Add(_trail[^1]);
        double need = seg, acc = 0;
        for (int i = _trail.Count - 1; i > 0 && pts.Count < count; i--)
        {
            Point a = _trail[i], b = _trail[i - 1];
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
    static readonly Brush OutlineFill = B("#40000000");
    static readonly Brush Body = B("#F7F2E8");
    static readonly Brush BodyDull = B("#CFC9BF");
    static readonly Brush Spot = B("#F2582A");
    static readonly Brush SpotDull = B("#B0735A");
    static readonly Brush Fin = B("#A6FFFFFF");
    static readonly Brush Eye = B("#1B1B1B");
}
