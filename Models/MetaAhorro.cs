namespace FinanzasPersonales.Models;

/// <summary>
/// Una meta es un apartado ("bolsillo") dentro de una cuenta: dinero que sigue en la cuenta pero
/// está reservado para algo. Decisión D5.
/// <para>
/// Antes <c>MontoActual</c> era un contador decorativo: "Agregar dinero" subía un número sin
/// relación con ninguna cuenta. Ahora lo apartado se calcula de sus movimientos
/// (<see cref="MovimientoMeta"/>), igual que los saldos de las cuentas se calculan de las
/// transacciones (D1): no hay contador que pueda descuadrarse.
/// </para>
/// </summary>
public class MetaAhorro
{
    public int      Id            { get; set; }
    public string   Nombre        { get; set; } = string.Empty;
    public decimal  MontoObjetivo { get; set; }
    public DateTime FechaLimite   { get; set; }

    /// <summary>Cuenta donde está el dinero. Null sólo en metas creadas antes de D5.</summary>
    public int?   CuentaId     { get; set; }
    public string CuentaNombre { get; set; } = string.Empty;

    /// <summary>Lo reservado ahora: suma de sus movimientos.</summary>
    public decimal Apartado { get; set; }

    /// <summary>
    /// El contador de antes, en metas sin cuenta. Al elegir la cuenta se convierte en un
    /// apartado inicial y queda en 0.
    /// </summary>
    public decimal MontoAnterior { get; set; }

    public bool TieneCuenta => CuentaId.HasValue;

    public double ProgresoPercent =>
        MontoObjetivo > 0 ? Math.Min(100.0, (double)(Apartado / MontoObjetivo) * 100.0) : 0;

    public string ProgresoTexto => $"₡{Apartado:N2} / ₡{MontoObjetivo:N2}";
}

/// <summary>Un movimiento de una meta. Positivo aparta; negativo usa o libera.</summary>
public class MovimientoMeta
{
    public const string TipoApartar = "Apartar";
    public const string TipoUsar    = "Usar";
    public const string TipoLiberar = "Liberar";

    public int      Id            { get; set; }
    public int      MetaId        { get; set; }
    public DateTime Fecha         { get; set; }
    public decimal  Monto         { get; set; }
    public string   Tipo          { get; set; } = TipoApartar;
    public int?     TransaccionId { get; set; }
}
