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

    public int Dias => (int)(FechaFin.Date - FechaInicio.Date).TotalDays + 1;

    /// <summary>Ej. «15 – 28 set 2026», o «29 set – 14 oct 2026» si cruza de mes.</summary>
    public string Label => FechaInicio.Month == FechaFin.Month
        ? $"{FechaInicio:dd} – {FechaFin:dd MMM yyyy}".ToLower(Cr)
        : $"{FechaInicio:dd MMM} – {FechaFin:dd MMM yyyy}".ToLower(Cr);

    public string RangoTexto => $"{FechaInicio:dd/MM/yyyy} — {FechaFin:dd/MM/yyyy}";
}
