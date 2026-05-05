namespace FinanzasPersonales.Models;

public class GastoFijo
{
    public int     Id              { get; set; }
    public string  Nombre          { get; set; } = string.Empty;
    public decimal Monto           { get; set; }
    public int     DiaVencimiento  { get; set; } // Día principal (1-31)
    public int     DiaVencimiento2 { get; set; } // Día secundario opcional (0 = sin segundo día)
    public bool    Activo          { get; set; } = true;

    // Populado por AppDatabase al cargar con estado
    public int    DiaEspecifico  { get; set; } // Qué día específico representa este registro
    public bool   PagadoEsteMes  { get; set; }
    public int    PagoMensualId  { get; set; }
    public string MetodoPago     { get; set; } = string.Empty;

    public int DiasParaVencer
    {
        get
        {
            var hoy  = DateTime.Today;
            var dia  = DiaEspecifico > 0 ? DiaEspecifico : DiaVencimiento;
            var vence = new DateTime(hoy.Year, hoy.Month,
                Math.Min(dia, DateTime.DaysInMonth(hoy.Year, hoy.Month)));
            return (int)(vence - hoy).TotalDays;
        }
    }

    public bool EsUrgente => !PagadoEsteMes && DiasParaVencer >= 0 && DiasParaVencer <= 3;
    public bool EsVencido  => !PagadoEsteMes && DiasParaVencer < 0;
}
