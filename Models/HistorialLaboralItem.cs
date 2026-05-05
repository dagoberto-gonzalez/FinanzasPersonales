namespace FinanzasPersonales.Models;

public class HistorialLaboralItem
{
    public bool     EsCierre         { get; set; }
    public DateTime Fecha            { get; set; }
    public string   Label            { get; set; } = "";
    public decimal  Bruto            { get; set; }
    public decimal  Deducciones      { get; set; }
    public decimal  Monto            { get; set; }
    public decimal  BalanceAcumulado { get; set; }
    public string   Notas            { get; set; } = "";
    public int      SourceId         { get; set; }
    public int?     TransaccionId    { get; set; }

    public string BrutoDisplay       => EsCierre ? $"₡{Bruto:N0}"        : "—";
    public string DeduccionesDisplay => EsCierre ? $"-₡{Deducciones:N0}" : "—";
    public string TipoDisplay        => EsCierre ? "Cierre período"       : "Ingreso directo";
    public string MontoDisplay       => $"₡{Monto:N0}";
    public string BalanceDisplay     => $"₡{BalanceAcumulado:N0}";
}
