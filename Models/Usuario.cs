namespace FinanzasPersonales.Models;

public class Usuario
{
    public int      Id            { get; set; }
    public string   NombreUsuario { get; set; } = string.Empty;
    public string   PasswordHash  { get; set; } = string.Empty;
    public string   Rol           { get; set; } = "Normal"; // "Admin" | "Normal"
    public bool     Activo        { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.Today;

    public bool EsAdmin => Rol == "Admin";
}
