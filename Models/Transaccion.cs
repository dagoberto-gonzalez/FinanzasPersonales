namespace FinanzasPersonales.Models;

public class Transaccion
{
    public int      Id               { get; set; }
    public string   Tipo             { get; set; } = "Gasto"; // "Ingreso" | "Gasto"
    public decimal  Monto            { get; set; }
    public string   Categoria        { get; set; } = string.Empty;
    public string   Descripcion      { get; set; } = string.Empty;
    public DateTime Fecha            { get; set; } = DateTime.Today;
    public string   Notas            { get; set; } = string.Empty;
    public string   CuentaNombre     { get; set; } = "Efectivo";

    /// <summary>Tarjeta con la que se compró. La deuda sube, pero todavía no sale efectivo.</summary>
    public int?     TarjetaCreditoId { get; set; }

    /// <summary>Tarjeta que salda este pago. Sale efectivo, pero no es un gasto nuevo.</summary>
    public int?     PagoDeTarjetaId  { get; set; }

    public int?     CuentaId         { get; set; }

    // ── Los dos ejes (decisión D2) ────────────────────────────────────────────
    // Un movimiento puede afectar a lo que gastaste, a lo que te queda, o a ambos.
    // Confundirlos en un único número era el origen de las incoherencias entre pantallas.

    /// <summary>
    /// Cuenta como gasto o ingreso del período: alimenta categorías, presupuesto y reportes.
    /// Pagar la tarjeta no cuenta — ese gasto ya se registró al comprar.
    /// </summary>
    public bool AfectaPresupuesto => PagoDeTarjetaId is null;

    /// <summary>
    /// Mueve dinero real: alimenta saldos de cuenta y el disponible.
    /// Comprar con tarjeta no cuenta — el dinero sale cuando pagás la tarjeta.
    /// </summary>
    public bool AfectaEfectivo => TarjetaCreditoId is null;

    public string EtiquetaDisplay =>
        !string.IsNullOrWhiteSpace(Notas) ? Notas :
        !string.IsNullOrWhiteSpace(Descripcion) ? Descripcion : Categoria;
}
