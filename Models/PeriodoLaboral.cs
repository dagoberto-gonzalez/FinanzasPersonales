using System.Globalization;

namespace FinanzasPersonales.Models;

public class PeriodoLaboral
{
    private static readonly CultureInfo Cr = CultureInfo.GetCultureInfo("es-CR");

    public int      Id        { get; set; }
    public int      UsuarioId { get; set; }

    /// <summary>
    /// El período se identifica por su rango de fechas. Antes era <c>(Anio, Mes)</c> con una
    /// clave única, lo que hacía imposible guardar dos quincenas del mismo mes — el combo
    /// ofrecía "Quincenal" y la tabla no podía representarlo.
    /// </summary>
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin    { get; set; }

    public decimal SalarioBruto  { get; set; }
    public decimal Deducciones   { get; set; }
    public decimal SalarioNeto   { get; set; }
    public bool    Cerrado       { get; set; }
    public int?    TransaccionId { get; set; }
    public string  Notas         { get; set; } = "";

    // Parámetros vigentes al cerrar. Sin ellos, un aumento de viáticos o de salario cambiaría lo
    // que se calcula para períodos ya pagados. Nulos en los cerrados antes de existir.
    public decimal? SalarioMensualAlCierre      { get; set; }
    public decimal? PorcentajeDeduccionAlCierre { get; set; }
    public decimal? ViaticosAlCierre            { get; set; }

    /// <summary>La configuración actual con los parámetros congelados al cierre, si los hay.</summary>
    public ConfiguracionLaboral ConfiguracionAlCierre(ConfiguracionLaboral actual) => new()
    {
        UsuarioId           = actual.UsuarioId,
        SalarioMensual      = SalarioMensualAlCierre      ?? actual.SalarioMensual,
        PorcentajeDeduccion = PorcentajeDeduccionAlCierre ?? actual.PorcentajeDeduccion,
        Viaticos            = ViaticosAlCierre            ?? actual.Viaticos,
        JornadaSemanal      = actual.JornadaSemanal,
        DiasPorSemana       = actual.DiasPorSemana,
        ModoPago            = actual.ModoPago,
        DiaPago             = actual.DiaPago,
        DiaPago2            = actual.DiaPago2,
        DiaSemana           = actual.DiaSemana
    };

    public int Dias => (int)(FechaFin.Date - FechaInicio.Date).TotalDays + 1;

    /// <summary>Ej. «15 – 28 set 2026», o «29 set – 14 oct 2026» si cruza de mes.</summary>
    public string Label => FechaInicio.Month == FechaFin.Month
        ? $"{FechaInicio:dd} – {FechaFin:dd MMM yyyy}".ToLower(Cr)
        : $"{FechaInicio:dd MMM} – {FechaFin:dd MMM yyyy}".ToLower(Cr);

    public string RangoTexto => $"{FechaInicio:dd/MM/yyyy} — {FechaFin:dd/MM/yyyy}";
}
