using System.Globalization;

namespace FinanzasPersonales.Services;

/// <summary>
/// Interpretación de importes escritos por el usuario.
/// <para>
/// Hace falta porque conviven dos formatos: lo que la persona teclea (<c>1500,50</c> o
/// <c>1500.50</c>) y lo que la propia interfaz pre-rellena con <c>ToString("N0")</c>, que en
/// cultura es-ES trae separador de miles (<c>1.000.000</c>). Un único <c>decimal.TryParse</c>
/// no cubre los dos casos.
/// </para>
/// </summary>
public static class Dinero
{
    /// <summary>
    /// Convierte el texto de un campo de importe a <see cref="decimal"/>.
    /// <para>
    /// Regla: el último separador (<c>.</c> o <c>,</c>) es decimal sólo si le siguen una o dos
    /// cifras; en cualquier otro caso todos los separadores son de miles. Cubre los formatos
    /// realistas en colones sin obligar a la persona a escribir de una forma concreta.
    /// </para>
    /// </summary>
    /// <example>
    /// <c>"1.000.000"</c> → 1000000 · <c>"1500,50"</c> → 1500.50 · <c>"1500.50"</c> → 1500.50
    /// · <c>"25 000"</c> → 25000
    /// </example>
    public static bool TryParse(string? texto, out decimal valor)
    {
        valor = 0m;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        // Fuera espacios (incluido el espacio duro que usan algunas culturas como separador
        // de miles) y cualquier símbolo de moneda que se haya colado.
        var s = new string(texto.Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray());
        if (s.Length == 0) return false;

        bool negativo = s.StartsWith('-');
        if (negativo) s = s[1..];
        if (s.Contains('-')) return false;   // un guion en medio no es un número

        int ultimoSeparador = s.LastIndexOfAny(['.', ',']);

        string enteros, decimales = "";
        if (ultimoSeparador >= 0)
        {
            var cola = s[(ultimoSeparador + 1)..];
            // 1 o 2 cifras detrás ⇒ es la parte decimal. Si no, era separador de miles.
            if (cola.Length is 1 or 2)
            {
                enteros   = s[..ultimoSeparador];
                decimales = cola;
            }
            else
            {
                enteros = s;
            }
        }
        else
        {
            enteros = s;
        }

        enteros = new string(enteros.Where(char.IsDigit).ToArray());
        if (enteros.Length == 0 && decimales.Length == 0) return false;
        if (enteros.Length == 0) enteros = "0";

        var normalizado = decimales.Length > 0 ? $"{enteros}.{decimales}" : enteros;

        if (!decimal.TryParse(normalizado, NumberStyles.AllowDecimalPoint,
                              CultureInfo.InvariantCulture, out valor))
            return false;

        if (negativo) valor = -valor;
        return true;
    }

    /// <summary>Igual que <see cref="TryParse"/> pero exigiendo un importe mayor que cero.</summary>
    public static bool TryParsePositivo(string? texto, out decimal valor) =>
        TryParse(texto, out valor) && valor > 0m;
}
