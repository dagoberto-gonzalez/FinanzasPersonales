namespace FinanzasPersonales.Models;

public class ConfiguracionLaboral
{
    public int     UsuarioId      { get; set; }

    /// <summary>
    /// Salario mensual: el dato que figura en la planilla. Todo lo demás sale de él.
    /// <para>
    /// Antes se configuraba el salario por hora y la base se calculaba como hora × jornada × días.
    /// La planilla no funciona así: paga medio mensual por quincena sin importar cuántos días
    /// tenga, y valora la hora como mensual ÷ 240. Con una jornada de 42,5 h la hora "lógica"
    /// salía un 30 % más alta que la que se paga, y con ella cada hora extra calculada.
    /// </para>
    /// </summary>
    public decimal SalarioMensual { get; set; }

    /// <summary>
    /// Divisor legal de la hora: 30 días × 8 horas. Es el que aplica la planilla (comprobantes de
    /// marzo y setiembre de 2026: ₡430.230,76 ÷ 240 = ₡1.792,63, el "Valor Base" de las horas
    /// dobles es exactamente el doble), no la jornada que se trabaje de verdad.
    /// </summary>
    public const decimal HorasPorMes = 240m;

    /// <summary>Valor de la hora ordinaria, redondeado al céntimo como en la planilla.</summary>
    public decimal SalarioPorHora => Math.Round(SalarioMensual / HorasPorMes, 2, MidpointRounding.AwayFromZero);

    public decimal JornadaSemanal { get; set; } = 48m;
    public string  ModoPago       { get; set; } = "Mensual"; // "Mensual", "Quincenal", "Semanal"
    public int     DiaPago        { get; set; } = 15;
    public int     DiaPago2       { get; set; }              // quincenal: segundo día del mes
    public int     DiaSemana      { get; set; } = 5;         // semanal: 1=Lunes … 7=Domingo
    public decimal Viaticos       { get; set; }              // monto fijo por período

    /// <summary>
    /// Días trabajados por semana. Antes estaba clavado en 6 dentro del cálculo, así que con una
    /// semana de 5 días el valor de la hora diaria salía un 20 % por debajo y con él todos los
    /// feriados y todas las ausencias.
    /// </summary>
    public int DiasPorSemana { get; set; } = 5;

    /// <summary>
    /// Porcentaje que se descuenta del bruto. Configurable porque cambia por ley: estaba escrito
    /// a mano como 10,67 % en cuatro sitios distintos.
    /// </summary>
    public decimal PorcentajeDeduccion { get; set; } = 10.83m;

    /// <summary>Horas de una jornada diaria. Es lo que vale un feriado o un día de ausencia.</summary>
    public decimal HorasDiarias => DiasPorSemana > 0 ? JornadaSemanal / DiasPorSemana : 0m;

    // ── Períodos ──────────────────────────────────────────────────────────────
    // Un período va desde el día siguiente a un corte hasta el corte siguiente. Expresarlo con
    // fechas y no con números de día hace que febrero deje de ser un caso especial: el día
    // siguiente al 28 de febrero es el 1 de marzo, sin necesidad de reglas aparte.

    /// <summary>Días del mes en los que cierra un período, ordenados.</summary>
    private int[] DiasDeCorte => ModoPago switch
    {
        "Quincenal" => DiaPago2 > 0 && DiaPago2 != DiaPago
            ? new[] { Math.Min(DiaPago, DiaPago2), Math.Max(DiaPago, DiaPago2) }
            : new[] { DiaPago },
        "Mensual"   => new[] { DiaPago },
        _           => []   // semanal no usa días del mes
    };

    /// <summary>Fechas de corte de un mes concreto, recortadas si el día no existe (febrero).</summary>
    private IEnumerable<DateTime> CortesDelMes(int anio, int mes)
    {
        var ultimo = DateTime.DaysInMonth(anio, mes);
        return DiasDeCorte
            .Select(d => new DateTime(anio, mes, Math.Min(Math.Max(d, 1), ultimo)))
            .Distinct()          // si dos cortes se recortan al mismo día, es uno solo
            .OrderBy(f => f);
    }

    /// <summary>Todos los cortes desde el mes anterior al siguiente, que basta para situar una fecha.</summary>
    private List<DateTime> CortesAlrededor(DateTime fecha)
    {
        if (ModoPago == "Semanal")
        {
            // El corte cae cada semana en DiaSemana; se recorren cinco semanas alrededor.
            var objetivo = (DayOfWeek)(DiaSemana % 7);
            var primero  = fecha.Date.AddDays(-21);
            while (primero.DayOfWeek != objetivo) primero = primero.AddDays(1);
            return Enumerable.Range(0, 8).Select(i => primero.AddDays(i * 7)).ToList();
        }

        var centro = new DateTime(fecha.Year, fecha.Month, 1);
        return new[] { centro.AddMonths(-1), centro, centro.AddMonths(1) }
            .SelectMany(m => CortesDelMes(m.Year, m.Month))
            .OrderBy(f => f)
            .ToList();
    }

    /// <summary>
    /// Período que contiene la fecha dada: termina en el primer corte que no es anterior a ella,
    /// y empieza el día siguiente al corte previo.
    /// </summary>
    public (DateTime Inicio, DateTime Fin) PeriodoQueContiene(DateTime fecha)
    {
        var cortes = CortesAlrededor(fecha);
        var fin    = cortes.FirstOrDefault(c => c >= fecha.Date);
        if (fin == default) fin = cortes[^1];

        var anterior = cortes.LastOrDefault(c => c < fin);
        var inicio   = anterior == default ? fin.AddDays(-14) : anterior.AddDays(1);
        return (inicio, fin);
    }

    public (DateTime Inicio, DateTime Fin) PeriodoAnterior(DateTime inicioActual) =>
        PeriodoQueContiene(inicioActual.AddDays(-1));

    public (DateTime Inicio, DateTime Fin) PeriodoSiguiente(DateTime finActual) =>
        PeriodoQueContiene(finActual.AddDays(1));

    /// <summary>
    /// Salario fijo de un período. Es una fracción fija del mensual —la mitad en quincenal— y no
    /// depende de cuántos días tenga el período: la planilla paga ₡215.115,38 tanto del 16 al 30
    /// de setiembre como del 16 al 31 de marzo. Prorratearlo por días daba una base distinta
    /// para cada quincena y ninguna coincidía con lo pagado.
    /// </summary>
    public decimal SalarioFijoPorPeriodo => ModoPago switch
    {
        "Quincenal" => Math.Round(SalarioMensual / 2m, 2, MidpointRounding.AwayFromZero),
        // Semana de 7 días a salario diario (mensual ÷ 30), la misma base de 30 días que la hora.
        "Semanal"   => Math.Round(SalarioMensual / 30m * 7m, 2, MidpointRounding.AwayFromZero),
        _           => SalarioMensual
    };
}
