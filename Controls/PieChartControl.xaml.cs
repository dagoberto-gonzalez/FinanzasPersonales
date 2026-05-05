using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FinanzasPersonales.Models;

namespace FinanzasPersonales.Controls;

public partial class PieChartControl : UserControl
{
    public static readonly DependencyProperty SlicesProperty =
        DependencyProperty.Register(
            nameof(Slices),
            typeof(IEnumerable<PieSlice>),
            typeof(PieChartControl),
            new PropertyMetadata(null, OnSlicesChanged));

    public IEnumerable<PieSlice>? Slices
    {
        get => (IEnumerable<PieSlice>?)GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    public PieChartControl() => InitializeComponent();

    private static void OnSlicesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (PieChartControl)d;

        if (e.OldValue is INotifyCollectionChanged old)
            old.CollectionChanged -= ctrl.OnCollectionChanged;

        if (e.NewValue is INotifyCollectionChanged next)
            next.CollectionChanged += ctrl.OnCollectionChanged;

        ctrl.Redraw();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Dispatcher.Invoke(Redraw);

    private void Redraw()
    {
        PieCanvas.Children.Clear();

        var slices = Slices?.ToList();
        if (slices is null || slices.Count == 0)
        {
            DrawEmptyCircle();
            return;
        }

        double total = slices.Sum(s => s.Value);
        if (total <= 0) { DrawEmptyCircle(); return; }

        const double cx     = 110;
        const double cy     = 110;
        const double radius = 100;
        const double hole   = 58;

        double startDeg = -90;

        foreach (var slice in slices)
        {
            if (slice.Value <= 0) continue;

            double sweep = slice.Value / total * 360;
            // Evitar arco exactamente de 360° (el arco colapsaría)
            if (sweep >= 359.999) sweep = 359.999;

            PieCanvas.Children.Add(
                BuildSegment(cx, cy, radius, hole, startDeg, sweep, slice.ColorBrush));

            startDeg += sweep;
        }

        // Agujero central
        var holeEllipse = new Ellipse
        {
            Width  = hole * 2,
            Height = hole * 2,
            Fill   = new SolidColorBrush(Color.FromRgb(0x24, 0x27, 0x3A))
        };
        Canvas.SetLeft(holeEllipse, cx - hole);
        Canvas.SetTop(holeEllipse,  cy - hole);
        PieCanvas.Children.Add(holeEllipse);
    }

    private void DrawEmptyCircle()
    {
        const double cx = 110, cy = 110;
        var circle = new Ellipse
        {
            Width  = 200,
            Height = 200,
            Fill   = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44))
        };
        Canvas.SetLeft(circle, cx - 100);
        Canvas.SetTop(circle,  cy - 100);
        PieCanvas.Children.Add(circle);

        // Texto centrado
        var tb = new TextBlock
        {
            Text       = "Sin datos",
            Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86)),
            FontSize   = 13
        };
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(tb, cx - tb.DesiredSize.Width / 2);
        Canvas.SetTop(tb,  cy - tb.DesiredSize.Height / 2);
        PieCanvas.Children.Add(tb);
    }

    private static Path BuildSegment(
        double cx, double cy, double radius, double innerRadius,
        double startDeg, double sweepDeg, Brush fill)
    {
        var startOuter = DegToPoint(cx, cy, radius,      startDeg);
        var endOuter   = DegToPoint(cx, cy, radius,      startDeg + sweepDeg);
        var startInner = DegToPoint(cx, cy, innerRadius, startDeg + sweepDeg);
        var endInner   = DegToPoint(cx, cy, innerRadius, startDeg);

        bool large = sweepDeg > 180;

        var figure = new PathFigure { StartPoint = startOuter, IsClosed = true, IsFilled = true };
        figure.Segments.Add(new ArcSegment(endOuter, new Size(radius, radius), 0, large,
                                            SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(startInner, true));
        figure.Segments.Add(new ArcSegment(endInner, new Size(innerRadius, innerRadius), 0, large,
                                            SweepDirection.Counterclockwise, true));

        var geo = new PathGeometry();
        geo.Figures.Add(figure);

        return new Path { Data = geo, Fill = fill };
    }

    private static Point DegToPoint(double cx, double cy, double r, double deg)
    {
        double rad = deg * Math.PI / 180.0;
        return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }
}
