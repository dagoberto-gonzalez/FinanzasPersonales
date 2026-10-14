namespace FinanzasPersonales.Models;

public class Categoria
{
    /// <summary>Para qué movimientos se ofrece la categoría en Transacciones.</summary>
    public const string TipoGasto   = "Gasto";
    public const string TipoIngreso = "Ingreso";
    public const string TipoAmbos   = "Ambos";

    public static readonly string[] Tipos = [TipoGasto, TipoIngreso, TipoAmbos];

    public int    Id       { get; set; }
    public string Nombre   { get; set; } = string.Empty;
    public bool   EsGlobal { get; set; }

    /// <summary>
    /// Gasto, Ingreso o Ambos. Antes no existía: al registrar un ingreso se ofrecían
    /// "Alimentación", "Transporte"… y había que buscar "Salario" entre ellas.
    /// </summary>
    public string Tipo     { get; set; } = TipoGasto;

    /// <summary>¿Se ofrece para un movimiento de este tipo ("Gasto" o "Ingreso")?</summary>
    public bool SirvePara(string tipoMovimiento) => Tipo == TipoAmbos || Tipo == tipoMovimiento;
}
