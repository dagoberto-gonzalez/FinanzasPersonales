namespace FinanzasPersonales.Models;

public class PeriodoLaboral
{
    public int     Id            { get; set; }
    public int     UsuarioId     { get; set; }
    public int     Anio          { get; set; }
    public int     Mes           { get; set; }
    public decimal SalarioBruto  { get; set; }
    public decimal Deducciones   { get; set; }
    public decimal SalarioNeto   { get; set; }
    public bool    Cerrado       { get; set; }
    public int?    TransaccionId { get; set; }
    public string  Notas         { get; set; } = "";

    public string MesLabel => new DateTime(Anio, Mes, 1).ToString("MMMM yyyy",
        System.Globalization.CultureInfo.GetCultureInfo("es-CR"));
}
