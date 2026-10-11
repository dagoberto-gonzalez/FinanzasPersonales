namespace FinanzasPersonales.Models;

/// <summary>
/// Lo que debería pagarse en un período según lo registrado. Reproduce la planilla línea por
/// línea para poder compararla con el comprobante:
/// <list type="bullet">
/// <item>Salario fijo: fracción fija del mensual, no prorrateada por días.</item>
/// <item>Horas: tarifa = hora × factor redondeada al céntimo, y total = cantidad × tarifa.</item>
/// <item>Bruto: sin viáticos. La C.C.S.S. se aplica sobre él; los viáticos se suman al neto.</item>
/// </list>
/// Antes los viáticos entraban en el bruto y pagaban deducción: ₡1.625 de más por quincena con
/// ₡15.000 de viáticos.
/// </summary>
public sealed class CalculoPeriodo
{
    public const decimal FactorNormal    = 1m;
    public const decimal FactorExtra     = 1.5m;
    public const decimal FactorNocturna  = 1.75m;
    public const decimal FactorDoble     = 2m;

    public decimal ValorHora      { get; private init; }
    public decimal SalarioFijo    { get; private init; }

    public decimal HorasExtra     { get; private init; }
    public decimal MontoExtra     { get; private init; }
    public decimal HorasNocturnas { get; private init; }
    public decimal MontoNocturnas { get; private init; }
    public decimal HorasDobles    { get; private init; }
    public decimal MontoDobles    { get; private init; }

    /// <summary>Horas trabajadas en feriado. La planilla las paga a 1× como "Horas Normales", además
    /// del salario fijo que ya cubre el día: en total, el feriado sale doble.</summary>
    public decimal HorasNormales        { get; private init; }
    public decimal MontoNormales        { get; private init; }
    public decimal HorasAusenciaSinGoce { get; private init; }
    public decimal DescuentoAusencias   { get; private init; }
    public decimal HorasAusenciaConGoce { get; private init; }

    public decimal Viaticos            { get; private init; }
    public decimal PorcentajeDeduccion { get; private init; }
    public decimal Bruto               { get; private init; }
    public decimal Deducciones         { get; private init; }
    public decimal Neto                { get; private init; }

    /// <summary>Las ausencias: la planilla no les dedica una línea propia que se haya visto.</summary>
    public decimal OtrosAjustes => -DescuentoAusencias;

    /// <summary>Tarifa de una hora con recargo, al céntimo, como el "Valor Base" del comprobante.</summary>
    public static decimal Tarifa(decimal valorHora, decimal factor) => Centimos(valorHora * factor);

    private static decimal Centimos(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static CalculoPeriodo De(ConfiguracionLaboral cfg, IEnumerable<RegistroDiaLaboral> registros)
    {
        var regs = registros.ToList();
        var hora = cfg.SalarioPorHora;

        decimal horasExtra = (decimal)regs.Sum(r => r.HorasExtraDiurnas);
        decimal horasNoct  = (decimal)regs.Sum(r => r.HorasExtraNocturnas);
        decimal horasDob   = (decimal)regs.Sum(r => r.HorasDobles);
        decimal horasNorm  = (decimal)regs.Sum(r => r.HorasNormales);
        decimal sinGoce    = (decimal)regs.Where(r => !r.TieneGoceSalario).Sum(r => r.HorasAusencia);
        decimal conGoce    = (decimal)regs.Where(r =>  r.TieneGoceSalario).Sum(r => r.HorasAusencia);

        decimal fijo       = cfg.SalarioFijoPorPeriodo;
        decimal montoExtra = Centimos(horasExtra * Tarifa(hora, FactorExtra));
        decimal montoNoct  = Centimos(horasNoct  * Tarifa(hora, FactorNocturna));
        decimal montoDob   = Centimos(horasDob   * Tarifa(hora, FactorDoble));
        decimal montoNorm  = Centimos(horasNorm  * Tarifa(hora, FactorNormal));
        decimal descAus    = Centimos(sinGoce * hora);

        decimal bruto = Math.Max(0m, fijo + montoNorm + montoExtra + montoNoct + montoDob - descAus);
        decimal ded   = Centimos(bruto * cfg.PorcentajeDeduccion / 100m);

        return new CalculoPeriodo
        {
            ValorHora            = hora,
            SalarioFijo          = fijo,
            HorasExtra           = horasExtra,
            MontoExtra           = montoExtra,
            HorasNocturnas       = horasNoct,
            MontoNocturnas       = montoNoct,
            HorasDobles          = horasDob,
            MontoDobles          = montoDob,
            HorasNormales        = horasNorm,
            MontoNormales        = montoNorm,
            HorasAusenciaSinGoce = sinGoce,
            DescuentoAusencias   = descAus,
            HorasAusenciaConGoce = conGoce,
            Viaticos             = cfg.Viaticos,
            PorcentajeDeduccion  = cfg.PorcentajeDeduccion,
            Bruto                = bruto,
            Deducciones          = ded,
            Neto                 = bruto - ded + cfg.Viaticos
        };
    }
}
