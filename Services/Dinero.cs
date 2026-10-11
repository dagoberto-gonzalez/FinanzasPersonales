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
    /// Reglas: el último separador (<c>.</c> o <c>,</c>) es decimal si le siguen una o dos
    /// cifras; en cualquier otro caso todos los separadores son de miles. Un número NO puede
    /// usar el mismo carácter como separador de miles y como decimal, y los grupos de miles
    /// deben ser de tres cifras.
    /// </para>
    /// <para>
    /// Lo ambiguo se rechaza en vez de interpretarse: <c>"1.200.00"</c> no es válido en ninguna
    /// convención (sería <c>1.200,00</c> o <c>1,200.00</c>), y adivinar en silencio con un
    /// importe es cómo se registran cifras equivocadas sin que nadie se entere.
    /// </para>
    /// </summary>
    /// <example>
    /// <c>"1.000.000"</c> → 1000000 · <c>"1500,50"</c> → 1500.50 · <c>"1500.50"</c> → 1500.50
    /// · <c>"25 000"</c> → 25000 · <c>"1.200.00"</c> → rechazado
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
        if (s.Length == 0 || s.Contains('-')) return false;   // un guion en medio no es un número

        int ultimoSeparador = s.LastIndexOfAny(['.', ',']);

        string enteros, decimales = "";
        if (ultimoSeparador >= 0)
        {
            var cola = s[(ultimoSeparador + 1)..];

            if (cola.Length is 1 or 2)
            {
                // Es la parte decimal. Ese mismo carácter no puede hacer además de separador
                // de miles: "1.200.00" mezcla las dos convenciones y no significa nada.
                var separadorDecimal = s[ultimoSeparador];
                enteros = s[..ultimoSeparador];
                if (enteros.Contains(separadorDecimal)) return false;
                decimales = cola;
            }
            else if (cola.Length == 3)
            {
                enteros = s;   // todo son separadores de miles
            }
            else
            {
                return false;  // "1.2345" no es ni una cosa ni la otra
            }
        }
        else
        {
            enteros = s;
        }

        if (!EsEnteroAgrupadoValido(enteros)) return false;

        enteros = new string(enteros.Where(char.IsDigit).ToArray());
        if (enteros.Length == 0) enteros = "0";

        var normalizado = decimales.Length > 0 ? $"{enteros}.{decimales}" : enteros;

        if (!decimal.TryParse(normalizado, NumberStyles.AllowDecimalPoint,
                              CultureInfo.InvariantCulture, out valor))
            return false;

        if (negativo) valor = -valor;
        return true;
    }

    /// <summary>
    /// La parte entera: o no lleva separadores, o todos son el mismo carácter y parten el
    /// número en grupos de tres cifras (el primero puede tener una, dos o tres).
    /// </summary>
    private static bool EsEnteroAgrupadoValido(string enteros)
    {
        if (enteros.Length == 0) return true;                       // ",50" → 0.50
        if (!enteros.All(c => char.IsDigit(c) || c is '.' or ',')) return false;

        var separadores = enteros.Where(c => c is '.' or ',').Distinct().ToList();
        if (separadores.Count == 0) return true;
        if (separadores.Count > 1) return false;                    // "1.000,000" mezcla

        var grupos = enteros.Split(separadores[0]);
        if (grupos[0].Length is < 1 or > 3) return false;
        return grupos.Skip(1).All(g => g.Length == 3);
    }

    /// <summary>Igual que <see cref="TryParse"/> pero exigiendo un importe mayor que cero.</summary>
    public static bool TryParsePositivo(string? texto, out decimal valor) =>
        TryParse(texto, out valor) && valor > 0m;
}
