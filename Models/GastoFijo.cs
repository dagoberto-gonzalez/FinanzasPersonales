namespace FinanzasPersonales.Models;

public class GastoFijo
{
    public int     Id              { get; set; }
    public string  Nombre          { get; set; } = string.Empty;
    public decimal Monto           { get; set; }
    public int     DiaVencimiento  { get; set; } // Día principal (1-31)
    public int     DiaVencimiento2 { get; set; } // Día secundario opcional (0 = sin segundo día)
    public bool    Activo          { get; set; } = true;

    /// <summary>
    /// Primer mes en que existe (día 1). Sin esto, un gasto creado hoy aparecía como pendiente
    /// en todos los meses anteriores al navegar hacia atrás. <c>default</c> al crear = el mes
    /// actual.
    /// </summary>
    public DateTime FechaInicio { get; set; }

    // Populado por AppDatabase al cargar con estado
    public int    DiaEspecifico  { get; set; } // Qué día específico representa este registro
    public bool   PagadoEsteMes  { get; set; }
    public int    PagoMensualId  { get; set; }
    public string MetodoPago     { get; set; } = string.Empty;

    /// <summary>Mes de esta fila. 0 = el actual (filas que no vienen de un mes concreto).</summary>
    public int Anio { get; set; }
    public int Mes  { get; set; }

    /// <summary>
    /// Días hasta el vencimiento EN EL MES DE LA FILA. Antes se calculaba siempre con el mes de
    /// hoy, que sólo tiene sentido en el mes actual: en uno pasado, lo no pagado está vencido; en
    /// uno futuro, todavía falta.
    /// </summary>
    public int DiasParaVencer
    {
        get
        {
            var hoy  = DateTime.Today;
            int anio = Anio > 0 ? Anio : hoy.Year, mes = Mes > 0 ? Mes : hoy.Month;
            var dia  = DiaEspecifico > 0 ? DiaEspecifico : DiaVencimiento;
            var vence = new DateTime(anio, mes, Math.Min(dia, DateTime.DaysInMonth(anio, mes)));
            return (int)(vence - hoy).TotalDays;
        }
    }

    public bool EsUrgente => !PagadoEsteMes && DiasParaVencer >= 0 && DiasParaVencer <= 3;
    public bool EsVencido  => !PagadoEsteMes && DiasParaVencer < 0;
}
