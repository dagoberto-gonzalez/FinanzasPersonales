namespace FinanzasPersonales.Models;

public class TarjetaCredito
{
    public int      Id                { get; set; }
    public string   Nombre            { get; set; } = string.Empty;
    public decimal  LimiteCredito     { get; set; }
    /// <summary>
    /// Deuda real pendiente: compras menos pagos. Calculado por
    /// <c>AppDatabase.ObtenerTarjetas</c> — no se almacena, asignarlo no persiste nada.
    /// </summary>
    public decimal  SaldoUsado        { get; set; }

    /// <summary>
    /// Compras desde el último corte. Es informativo: lo que se lleva consumido del período
    /// en curso, que no es lo mismo que lo que se debe (el corte anterior puede estar sin pagar).
    /// </summary>
    public decimal  ConsumoPeriodo    { get; set; }

    public int      DiaCierre         { get; set; }
    public int      DiaPago           { get; set; }

    public decimal SaldoDisponible => LimiteCredito - SaldoUsado;

    public string SaldoUsadoTexto     => $"₡{SaldoUsado:N0}";
    public string ConsumoPeriodoTexto => $"₡{ConsumoPeriodo:N0}";
    public double  PorcentajeUso   => LimiteCredito > 0
        ? Math.Min(100.0, (double)(SaldoUsado / LimiteCredito) * 100.0) : 0;

    // ── Período actual ────────────────────────────────────────────────────────

    /// <summary>Fecha en que inició el período actual (último cierre que ya pasó).</summary>
    public DateTime InicioPeriodo
    {
        get
        {
            var hoy = DateTime.Today;
            var cierreEsteMes = new DateTime(hoy.Year, hoy.Month,
                Math.Min(DiaCierre, DateTime.DaysInMonth(hoy.Year, hoy.Month)));
            return hoy.Date >= cierreEsteMes.Date
                ? cierreEsteMes
                : cierreEsteMes.AddMonths(-1);
        }
    }

    /// <summary>Fecha en que cierra el período actual (próximo DiaCierre).</summary>
    public DateTime FinPeriodo
    {
        get
        {
            var hoy = DateTime.Today;
            var cierreEsteMes = new DateTime(hoy.Year, hoy.Month,
                Math.Min(DiaCierre, DateTime.DaysInMonth(hoy.Year, hoy.Month)));
            return hoy.Date >= cierreEsteMes.Date
                ? cierreEsteMes.AddMonths(1)
                : cierreEsteMes;
        }
    }

    /// <summary>Fecha límite de pago (próximo DiaPago después del cierre).</summary>
    public DateTime FechaLimitePago
    {
        get
        {
            var fin = FinPeriodo;
            var pago = new DateTime(fin.Year, fin.Month,
                Math.Min(DiaPago, DateTime.DaysInMonth(fin.Year, fin.Month)));
            // El pago es en el mismo mes del cierre o en el siguiente
            if (pago <= fin)
                pago = pago.AddMonths(1);
            return pago;
        }
    }

    public string PeriodoTexto         => $"{InicioPeriodo:dd/MM/yyyy} — {FinPeriodo:dd/MM/yyyy}";
    public string FechaLimitePagoTexto => FechaLimitePago.ToString("dd/MM/yyyy");

    public int DiasHastaCierre
    {
        get
        {
            var hoy    = DateTime.Today;
            var cierre = new DateTime(hoy.Year, hoy.Month,
                Math.Min(DiaCierre, DateTime.DaysInMonth(hoy.Year, hoy.Month)));
            if (cierre < hoy)
                cierre = cierre.AddMonths(1);
            return (int)(cierre - hoy).TotalDays;
        }
    }

    public int DiasHastaPago
    {
        get
        {
            var hoy  = DateTime.Today;
            var pago = new DateTime(hoy.Year, hoy.Month,
                Math.Min(DiaPago, DateTime.DaysInMonth(hoy.Year, hoy.Month)));
            if (pago < hoy)
                pago = pago.AddMonths(1);
            return (int)(pago - hoy).TotalDays;
        }
    }
}
