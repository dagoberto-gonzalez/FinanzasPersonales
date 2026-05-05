using System.Windows.Media;

namespace FinanzasPersonales.Models;

public class PieSlice
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Percentage { get; set; }
    public Brush ColorBrush { get; set; } = Brushes.Gray;
}
