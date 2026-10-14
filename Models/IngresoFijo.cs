namespace FinanzasPersonales.Models;

public class IngresoFijo
{
    public int     Id          { get; set; }
    public string  Nombre      { get; set; } = string.Empty;
    public decimal Monto       { get; set; }
    public int     DiaIngreso  { get; set; } // Día principal (1-31)
    public int     DiaIngreso2 { get; set; } // Día secundario opcional (0 = sin segundo día)
    public bool    Activo      { get; set; } = true;

    public int? CuentaId { get; set; }

    /// <summary>Primer mes en que existe (día 1); ver <see cref="GastoFijo.FechaInicio"/>.</summary>
    public DateTime FechaInicio { get; set; }

    // Populado por AppDatabase al cargar con estado
    public int  DiaEspecifico   { get; set; }
    public bool RecibidoEsteMes { get; set; }
    public int  PagoMensualId   { get; set; }
}
