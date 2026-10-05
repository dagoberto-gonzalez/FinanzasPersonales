namespace FinanzasPersonales.Models;

/// <summary>
/// Lo que dice el comprobante de pago de un período, con los mismos conceptos que trae la
/// planilla. Se guarda aparte de lo calculado: antes "Editar" un cierre sobrescribía las cifras
/// calculadas con las pagadas, y entonces no quedaba nada con qué comparar.
/// </summary>
public sealed class ComprobantePago
{
    public int     PeriodoId      { get; set; }
    public decimal SalarioFijo    { get; set; }
    /// <summary>"Horas Normales": las de un feriado trabajado, a 1×.</summary>
    public decimal HorasNormales  { get; set; }
    public decimal MontoNormales  { get; set; }
    public decimal HorasExtra     { get; set; }
    public decimal MontoExtra     { get; set; }
    public decimal HorasNocturnas { get; set; }
    public decimal MontoNocturnas { get; set; }
    public decimal HorasDobles    { get; set; }
    public decimal MontoDobles    { get; set; }

    /// <summary>Cualquier línea que no sea de las anteriores, con su signo (un rebajo, negativo).</summary>
    public decimal OtrosAjustes   { get; set; }

    public decimal Viaticos       { get; set; }
    public decimal Deducciones    { get; set; }
    public string  Notas          { get; set; } = "";

    /// <summary>"Monto Bruto" del comprobante: sin viáticos.</summary>
    public decimal Bruto => SalarioFijo + MontoNormales + MontoExtra + MontoNocturnas + MontoDobles + OtrosAjustes;

    /// <summary>"Monto Neto" del comprobante: lo que llega a la cuenta.</summary>
    public decimal Neto => Bruto - Deducciones + Viaticos;
}

/// <summary>Una fila de la comparación entre lo calculado y lo pagado.</summary>
public sealed class LineaComparacion
{
    /// <summary>Por debajo de un colón es redondeo de la planilla, no una diferencia.</summary>
    public const decimal Tolerancia = 1m;

    public string   Concepto       { get; init; } = "";
    public decimal? HorasCalculadas { get; init; }
    public decimal? HorasPagadas    { get; init; }
    public decimal  Calculado      { get; init; }
    public decimal  Pagado         { get; init; }
    public bool     EsTotal        { get; init; }

    /// <summary>
    /// Todavía no se copió nada del comprobante. Sin esto, al abrir el formulario todas las
    /// líneas salían en rojo con la cifra entera como diferencia, antes de escribir nada.
    /// </summary>
    public bool     Pendiente      { get; init; }

    private static string Moneda(decimal v) => v < 0 ? $"−₡{-v:N2}" : $"₡{v:N2}";
    public string CalculadoTexto => Moneda(Calculado);
    public string PagadoTexto    => Pendiente ? "—" : Moneda(Pagado);

    /// <summary>Positiva si pagaron de más; negativa si pagaron de menos.</summary>
    public decimal Diferencia      => Pagado - Calculado;
    public decimal? DiferenciaHoras => HorasCalculadas is { } c && HorasPagadas is { } p ? p - c : null;

    public bool Cuadra =>
        Math.Abs(Diferencia) < Tolerancia && (DiferenciaHoras is null || DiferenciaHoras == 0m);

    public string HorasTexto => HorasCalculadas is { } c && HorasPagadas is { } p
        ? Pendiente ? $"{c:0.##} h" : $"{c:0.##} h → {p:0.##} h"
        : "";

    public string DiferenciaTexto => Pendiente
        ? ""
        : Cuadra
        ? "✓"
        : Math.Abs(Diferencia) < Tolerancia
            ? $"{DiferenciaHoras:+0.##;-0.##} h"
            : $"{(Diferencia > 0 ? "+" : "−")}₡{Math.Abs(Diferencia):N2}";

    /// <summary>
    /// Arma la comparación en el orden del comprobante. Las ausencias no tienen línea propia
    /// conocida en la planilla, así que van en "Otros ajustes".
    /// </summary>
    public static List<LineaComparacion> Comparar(CalculoPeriodo c, ComprobantePago p, bool pendiente = false) =>
    [
        new() { Concepto = "Salario fijo",    Calculado = c.SalarioFijo,    Pagado = p.SalarioFijo, Pendiente = pendiente },
        new() { Concepto = "Horas normales",  Calculado = c.MontoNormales,  Pagado = p.MontoNormales, Pendiente = pendiente,
                HorasCalculadas = c.HorasNormales,  HorasPagadas = p.HorasNormales },
        new() { Concepto = "Horas extra",     Calculado = c.MontoExtra,     Pagado = p.MontoExtra, Pendiente = pendiente,
                HorasCalculadas = c.HorasExtra,     HorasPagadas = p.HorasExtra },
        new() { Concepto = "Horas nocturnas", Calculado = c.MontoNocturnas, Pagado = p.MontoNocturnas, Pendiente = pendiente,
                HorasCalculadas = c.HorasNocturnas, HorasPagadas = p.HorasNocturnas },
        new() { Concepto = "Horas dobles",    Calculado = c.MontoDobles,    Pagado = p.MontoDobles, Pendiente = pendiente,
                HorasCalculadas = c.HorasDobles,    HorasPagadas = p.HorasDobles },
        new() { Concepto = "Otros ajustes",   Calculado = c.OtrosAjustes,   Pagado = p.OtrosAjustes, Pendiente = pendiente },
        new() { Concepto = "Monto bruto",     Calculado = c.Bruto,          Pagado = p.Bruto, Pendiente = pendiente, EsTotal = true },
        new() { Concepto = "C.C.S.S.",        Calculado = -c.Deducciones,   Pagado = -p.Deducciones, Pendiente = pendiente },
        new() { Concepto = "Viáticos",        Calculado = c.Viaticos,       Pagado = p.Viaticos, Pendiente = pendiente },
        new() { Concepto = "Monto neto",      Calculado = c.Neto,           Pagado = p.Neto, Pendiente = pendiente, EsTotal = true },
    ];
}
