namespace FinanzasPersonales.Models;

public class Categoria
{
    public int    Id       { get; set; }
    public string Nombre   { get; set; } = string.Empty;
    public bool   EsGlobal { get; set; }
}
