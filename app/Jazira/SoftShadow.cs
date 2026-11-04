using System;
using System.Windows;
using System.Windows.Media;

namespace Jazira;

// Мягкая тень острова без эффекта размытия: несколько вложенных скруглённых прямоугольников,
// каждый чуть больше и прозрачнее предыдущего. Настоящее размытие (DropShadowEffect) пересчитывалось
// на каждый кадр рыбки и эквалайзера и стоило дороже всей остальной работы острова.
sealed class SoftShadow : FrameworkElement
{
    const int Layers = 14; // меньше — на цветной подсветке видны ступеньки

    public static readonly DependencyProperty ColorProperty = Prop(nameof(Color), Colors.Black);
    public static readonly DependencyProperty StrengthProperty = Prop(nameof(Strength), 0.45);
    public static readonly DependencyProperty SpreadProperty = Prop(nameof(Spread), 14.0);
    public static readonly DependencyProperty DepthProperty = Prop(nameof(Depth), 3.0);

    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public double Strength { get => (double)GetValue(StrengthProperty); set => SetValue(StrengthProperty, value); } // 0…1 у самого края
    public double Spread { get => (double)GetValue(SpreadProperty); set => SetValue(SpreadProperty, value); }       // на сколько тень расходится
    public double Depth { get => (double)GetValue(DepthProperty); set => SetValue(DepthProperty, value); }         // сдвиг вниз

    Rect _shape;
    double _radius;
    bool _notch;

    // Где остров и какой он формы — в координатах этого элемента
    public void SetShape(Rect shape, double radius, bool notch)
    {
        if (shape == _shape && radius == _radius && notch == _notch) return;
        _shape = shape;
        _radius = radius;
        _notch = notch;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_shape.Width <= 0 || Strength <= 0) return;
        // Слои складываются: у края острова суммарная плотность равна Strength и к внешнему краю сходит на нет
        double a = 1 - Math.Pow(1 - Math.Clamp(Strength, 0, 0.95), 1.0 / Layers);
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(a * 255), Color.R, Color.G, Color.B));
        brush.Freeze();
        for (int i = 1; i <= Layers; i++)
        {
            // Слои гуще у края острова и реже снаружи — тень плавно тает, как при размытии
            double g = Spread * Math.Pow(i / (double)Layers, 1.7);
            var r = new Rect(_shape.X - g, _shape.Y - g + Depth, _shape.Width + 2 * g, _shape.Height + 2 * g);
            double rad = _radius + g;
            if (_notch)
            {
                // У выреза тень только снизу и по бокам: сверху край экрана
                r = new Rect(r.X, _shape.Y - rad, r.Width, r.Bottom - (_shape.Y - rad));
            }
            dc.DrawRoundedRectangle(brush, null, r, rad, rad);
        }
    }

    static DependencyProperty Prop<T>(string name, T value) =>
        DependencyProperty.Register(name, typeof(T), typeof(SoftShadow),
            new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender));
}
