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

    /// <summary>
    /// Las dos mitades de una transferencia entre cuentas (o con el efectivo) comparten este
    /// número: la salida del origen y la entrada al destino. El dinero sólo cambia de lugar.
    /// </summary>
    public int?     TransferenciaId  { get; set; }

    public bool EsTransferencia => TransferenciaId.HasValue;

    // ── Los dos ejes (decisión D2) ────────────────────────────────────────────
    // Un movimiento puede afectar a lo que gastaste, a lo que te queda, o a ambos.
    // Confundirlos en un único número era el origen de las incoherencias entre pantallas.

    /// <summary>
    /// Cuenta como gasto o ingreso del período: alimenta categorías, presupuesto y reportes.
    /// Pagar la tarjeta no cuenta — ese gasto ya se registró al comprar. Una transferencia
    /// tampoco: pasar dinero de una cuenta a otra no es ni gastarlo ni ganarlo.
    /// </summary>
    public bool AfectaPresupuesto => PagoDeTarjetaId is null && TransferenciaId is null;

    /// <summary>
    /// Mueve dinero real: alimenta saldos de cuenta y el disponible.
    /// Comprar con tarjeta no cuenta — el dinero sale cuando pagás la tarjeta.
    /// </summary>
    public bool AfectaEfectivo => TarjetaCreditoId is null;

    /// <summary>
    /// Lo que identifica la transacción en las listas: la descripción, si no las notas, si no
    /// la categoría.
    /// <para>
    /// Antes las notas iban primero. Las transacciones automáticas guardan el nombre en la
    /// descripción y un texto fijo en las notas ("Ingreso fijo recibido", "Pago automático de
    /// gasto fijo", "Abono registrado manualmente"), así que ese texto tapaba el nombre: no se
    /// sabía qué ingreso fijo era ni qué tarjeta se había pagado.
    /// </para>
    /// </summary>
    public string EtiquetaDisplay =>
        !string.IsNullOrWhiteSpace(Descripcion) ? Descripcion :
        !string.IsNullOrWhiteSpace(Notas)       ? Notas       : Categoria;

    /// <summary>Las notas, para el tooltip; null si no hay (así no aparece un tooltip vacío).</summary>
    public string? NotasTooltip => string.IsNullOrWhiteSpace(Notas) ? null : Notas;
}
