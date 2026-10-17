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

    /// <summary>Estado del comprobante de pago frente a lo calculado. Sólo en cierres.</summary>
    public string   Comprobante      { get; set; } = "";

    /// <summary>Si la quincena ya se cobró: "✓ 16/10", o "Pendiente". Sólo en cierres.</summary>
    public string   Ingreso          { get; set; } = "";

    public string BrutoDisplay       => EsCierre ? $"₡{Bruto:N2}"        : "—";
    public string DeduccionesDisplay => EsCierre ? $"-₡{Deducciones:N2}" : "—";
    public string TipoDisplay        => EsCierre ? "Cierre período"       : "Ingreso directo";
    public string MontoDisplay       => $"₡{Monto:N2}";
    public string BalanceDisplay     => $"₡{BalanceAcumulado:N2}";
}
