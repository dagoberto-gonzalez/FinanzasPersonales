namespace FinanzasPersonales.Models;

public class MetaAhorro
{
    public int      Id            { get; set; }
    public string   Nombre        { get; set; } = string.Empty;
    public decimal  MontoObjetivo { get; set; }
    public decimal  MontoActual   { get; set; }
    public DateTime FechaLimite   { get; set; }

    public double ProgresoPercent =>
        MontoObjetivo > 0 ? Math.Min(100.0, (double)(MontoActual / MontoObjetivo) * 100.0) : 0;

    public string ProgresoTexto => $"₡{MontoActual:N0} / ₡{MontoObjetivo:N0}";
}
