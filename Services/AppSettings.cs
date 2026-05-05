namespace FinanzasPersonales.Services;

/// <summary>Configuración global de la aplicación, cargada en tiempo de ejecución.</summary>
public static class AppSettings
{
    /// <summary>Símbolo de moneda activo (ej. "₡", "$", "€").</summary>
    public static string Moneda { get; set; } = "₡";

    /// <summary>Formatea un monto con el símbolo de moneda y separadores de miles.</summary>
    public static string FormatMonto(decimal monto) => $"{Moneda}{monto:N0}";
}
