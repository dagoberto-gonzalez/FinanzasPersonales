namespace FinanzasPersonales.Models;

public class ConfiguracionLaboral
{
    public int     UsuarioId      { get; set; }
    public decimal SalarioPorHora { get; set; }
    public decimal JornadaSemanal { get; set; } = 48m;
    public string  ModoPago       { get; set; } = "Mensual"; // "Mensual", "Quincenal", "Semanal"
    public int     DiaPago        { get; set; } = 15;
    public int     DiaPago2       { get; set; }              // quincenal: segundo día del mes
    public int     DiaSemana      { get; set; } = 5;         // semanal: 1=Lunes … 7=Domingo
    public decimal Viaticos       { get; set; }              // monto fijo por período
}
