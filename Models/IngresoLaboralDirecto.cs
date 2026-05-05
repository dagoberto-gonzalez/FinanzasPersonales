namespace FinanzasPersonales.Models;

public class IngresoLaboralDirecto
{
    public int      Id            { get; set; }
    public int      UsuarioId     { get; set; }
    public DateTime Fecha         { get; set; }
    public decimal  Monto         { get; set; }
    public string   Descripcion   { get; set; } = "";
    public int?     CuentaId      { get; set; }
    public int?     TransaccionId { get; set; }

    public string FechaDisplay => Fecha.ToString("dd/MM/yyyy");
}
