using System.Globalization;

namespace FinanzasPersonales.Models;

public class ResumenMensual
{
    public int     Id               { get; set; }
    public int     UsuarioId        { get; set; }
    public int     Anio             { get; set; }
    public int     Mes              { get; set; }
    public decimal Ingresos         { get; set; }
    public decimal Gastos           { get; set; }
    public decimal Balance          { get; set; }
    public decimal BalanceAcumulado { get; set; }
    public int     NumTransacciones { get; set; }
    public bool    PeriodoLaboral   { get; set; }
    public string  FechaCierre      { get; set; } = string.Empty;
    public string  Notas            { get; set; } = string.Empty;

    private static readonly CultureInfo CrCulture =
        CultureInfo.GetCultureInfo("es-CR");

    public string MesLabel           => new DateTime(Anio, Mes, 1).ToString("MMMM yyyy", CrCulture);
    public string PeriodoLaboralTexto => PeriodoLaboral ? "✓" : "—";
}
