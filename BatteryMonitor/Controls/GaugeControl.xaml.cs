using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BatteryMonitor.Controls;

public partial class GaugeControl : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(GaugeControl),
            new PropertyMetadata(0.0, OnValueChanged));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(GaugeControl),
            new PropertyMetadata("", OnLabelChanged));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(GaugeControl),
            new PropertyMetadata("%", OnUnitChanged));

    public static readonly DependencyProperty GaugeColorProperty =
        DependencyProperty.Register(nameof(GaugeColor), typeof(Brush), typeof(GaugeControl),
            new PropertyMetadata(Brushes.LimeGreen, OnValueChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public Brush GaugeColor
    {
        get => (Brush)GetValue(GaugeColorProperty);
        set => SetValue(GaugeColorProperty, value);
    }

    private Brush _bgArcBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x23, 0x30));

    public GaugeControl()
    {
        InitializeComponent();
        Loaded += (_, _) => DrawArc();
    }

    public void UpdateTheme(Brush textPrimary, Brush textMuted, Brush accent, Brush bgArc)
    {
        ValueText.Foreground = textPrimary;
        UnitText.Foreground = textMuted;
        LabelText.Foreground = accent;
        _bgArcBrush = bgArc;
        DrawArc();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GaugeControl g) g.DrawArc();
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GaugeControl g) g.LabelText.Text = e.NewValue?.ToString() ?? "";
    }

    private static void OnUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GaugeControl g) g.UnitText.Text = e.NewValue?.ToString() ?? "";
    }

    private void DrawArc()
    {
        ArcCanvas.Children.Clear();

        const double cx = 60, cy = 60, r = 52;
        const double startAngle = 230;
        const double totalSweep = 280;
        const double thickness = 9;

        var bgPath = CreateArcPath(cx, cy, r, startAngle, totalSweep, _bgArcBrush, thickness);
        ArcCanvas.Children.Add(bgPath);

        var pct = Math.Max(0, Math.Min(100, Value));
        if (pct > 0.5)
        {
            var sweep = pct / 100.0 * totalSweep;
            var valuePath = CreateArcPath(cx, cy, r, startAngle, sweep, GaugeColor, thickness);
            ArcCanvas.Children.Add(valuePath);
        }

        ValueText.Text = $"{Value:F0}";
        LabelText.Text = Label;
        UnitText.Text = Unit;
    }

    private static Path CreateArcPath(double cx, double cy, double r, double startAngleDeg, double sweepDeg, Brush stroke, double thickness)
    {
        var startRad = (startAngleDeg - 90) * Math.PI / 180;
        var endRad = (startAngleDeg - 90 + sweepDeg) * Math.PI / 180;

        var x1 = cx + r * Math.Cos(startRad);
        var y1 = cy + r * Math.Sin(startRad);
        var x2 = cx + r * Math.Cos(endRad);
        var y2 = cy + r * Math.Sin(endRad);

        var figure = new PathFigure
        {
            StartPoint = new Point(x1, y1),
            IsClosed = false
        };

        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(x2, y2),
            Size = new Size(r, r),
            IsLargeArc = sweepDeg > 180,
            SweepDirection = SweepDirection.Clockwise
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Path
        {
            Data = geometry,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
    }
}
