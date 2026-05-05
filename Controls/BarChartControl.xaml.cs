using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FinanzasPersonales.Models;

namespace FinanzasPersonales.Controls;

public partial class BarChartControl : UserControl
{
    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IEnumerable), typeof(BarChartControl),
            new PropertyMetadata(null, (d, _) => ((BarChartControl)d).Redibujar()));

    public IEnumerable? Items
    {
        get => (IEnumerable?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private static readonly Brush BrushIngreso = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
    private static readonly Brush BrushGasto   = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
    private static readonly Brush BrushEje     = new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x52));
    private static readonly Brush BrushLabel   = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86));

    public BarChartControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redibujar();
    }

    private void Redibujar()
    {
        ChartCanvas.Children.Clear();
        EmptyText.Visibility = Visibility.Collapsed;

        var datos = Items?.Cast<BarMonth>().ToList() ?? [];
        if (datos.Count == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            return;
        }

        double w      = ActualWidth;
        double h      = ActualHeight;
        if (w < 10 || h < 10) return;

        double padLeft   = 60;
        double padRight  = 16;
        double padTop    = 16;
        double padBottom = 50;

        double chartW = w - padLeft - padRight;
        double chartH = h - padTop - padBottom;

        double maxVal  = datos.Max(d => Math.Max(d.Ingresos, d.Gastos));
        if (maxVal == 0) maxVal = 1;

        // Líneas horizontales de referencia
        int gridLines = 4;
        for (int i = 0; i <= gridLines; i++)
        {
            double y    = padTop + chartH - (chartH * i / gridLines);
            double mval = maxVal * i / gridLines;

            var line = new Line
            {
                X1              = padLeft,
                Y1              = y,
                X2              = padLeft + chartW,
                Y2              = y,
                Stroke          = BrushEje,
                StrokeThickness = 0.5
            };
            ChartCanvas.Children.Add(line);

            var lbl = new TextBlock
            {
                Text       = $"₡{mval / 1000:N0}k",
                Foreground = BrushLabel,
                FontSize   = 9
            };
            Canvas.SetLeft(lbl, 0);
            Canvas.SetTop(lbl, y - 7);
            ChartCanvas.Children.Add(lbl);
        }

        // Barras
        int    n         = datos.Count;
        double groupW    = chartW / n;
        double barW      = Math.Max(4, groupW * 0.3);
        double gap       = Math.Max(2, groupW * 0.05);

        for (int i = 0; i < n; i++)
        {
            var d    = datos[i];
            double x = padLeft + i * groupW + groupW * 0.1;

            // Ingreso
            double hI = chartH * (d.Ingresos / maxVal);
            var barI  = new Rectangle
            {
                Width  = barW,
                Height = Math.Max(1, hI),
                Fill   = BrushIngreso,
                RadiusX = 3,
                RadiusY = 3
            };
            Canvas.SetLeft(barI, x);
            Canvas.SetTop(barI, padTop + chartH - hI);
            ChartCanvas.Children.Add(barI);

            // Gasto
            double hG = chartH * (d.Gastos / maxVal);
            var barG  = new Rectangle
            {
                Width  = barW,
                Height = Math.Max(1, hG),
                Fill   = BrushGasto,
                RadiusX = 3,
                RadiusY = 3
            };
            Canvas.SetLeft(barG, x + barW + gap);
            Canvas.SetTop(barG, padTop + chartH - hG);
            ChartCanvas.Children.Add(barG);

            // Etiqueta mes
            var lblMes = new TextBlock
            {
                Text       = d.MesLabel.Replace("\n", " "),
                Foreground = BrushLabel,
                FontSize   = 9,
                TextAlignment = TextAlignment.Center,
                Width      = groupW * 0.8
            };
            Canvas.SetLeft(lblMes, x - groupW * 0.1 + groupW * 0.1);
            Canvas.SetTop(lblMes, padTop + chartH + 6);
            ChartCanvas.Children.Add(lblMes);
        }

        // Eje vertical
        var ejeY = new Line
        {
            X1 = padLeft, Y1 = padTop,
            X2 = padLeft, Y2 = padTop + chartH,
            Stroke = BrushEje, StrokeThickness = 1
        };
        ChartCanvas.Children.Add(ejeY);

        // Leyenda
        AgregarLeyenda(w - 140, h - 20, BrushIngreso, "Ingresos");
        AgregarLeyenda(w -  70, h - 20, BrushGasto,   "Gastos");
    }

    private void AgregarLeyenda(double x, double y, Brush color, string texto)
    {
        var rect = new Rectangle { Width = 10, Height = 10, Fill = color, RadiusX = 2, RadiusY = 2 };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        ChartCanvas.Children.Add(rect);

        var lbl = new TextBlock { Text = texto, Foreground = BrushLabel, FontSize = 9 };
        Canvas.SetLeft(lbl, x + 13);
        Canvas.SetTop(lbl, y - 1);
        ChartCanvas.Children.Add(lbl);
    }
}
