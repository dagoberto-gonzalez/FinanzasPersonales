namespace FinanzasPersonales.Models;

public class Transaccion
{
    public int      Id               { get; set; }
    public string   Tipo             { get; set; } = "Gasto"; // "Ingreso" | "Gasto"
    public decimal  Monto            { get; set; }
    public string   Categoria        { get; set; } = string.Empty;
    public string   Descripcion      { get; set; } = string.Empty;
    public DateTime Fecha            { get; set; } = DateTime.Today;
    public string   Notas            { get; set; } = string.Empty;
    public string   CuentaNombre     { get; set; } = "Efectivo";
    public int?     TarjetaCreditoId { get; set; }
    public int?     CuentaId         { get; set; }

    public string EtiquetaDisplay =>
        !string.IsNullOrWhiteSpace(Notas) ? Notas :
        !string.IsNullOrWhiteSpace(Descripcion) ? Descripcion : Categoria;
}
