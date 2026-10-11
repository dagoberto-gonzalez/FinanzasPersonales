using System.Security.Cryptography;
using System.Text;

namespace FinanzasPersonales.Services;

/// <summary>
/// Cómo se guardan y comprueban las contraseñas.
/// <para>
/// Antes era SHA-256 sin sal: rápido a propósito para datos, que es justo lo contrario de lo que
/// conviene con contraseñas. Quien copiara <c>finanzas.db</c> podía probar miles de millones de
/// candidatas por segundo, y dos usuarios con la misma contraseña tenían el mismo hash.
/// </para>
/// <para>
/// Ahora es PBKDF2-HMAC-SHA256 con sal aleatoria por usuario y 600.000 iteraciones (OWASP). El
/// formato guarda el algoritmo, las iteraciones y la sal, así que se puede subir el coste más
/// adelante sin invalidar lo ya guardado: <c>pbkdf2-sha256$iteraciones$sal$hash</c> (Base64).
/// </para>
/// </summary>
public static class Contrasenas
{
    private const string Prefijo   = "pbkdf2-sha256";
    private const int    BytesSal  = 16;
    private const int    BytesHash = 32;

    /// <summary>
    /// Iteraciones para las contraseñas nuevas. Las pruebas la bajan (crean un usuario por prueba
    /// y 600.000 iteraciones cada vez sumarían un minuto); por eso es interna y no constante.
    /// </summary>
    internal static int Iteraciones { get; set; } = 600_000;

    public static string Crear(string password)
    {
        var sal  = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Derivar(password, sal, Iteraciones);
        return $"{Prefijo}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Comprueba una contraseña contra lo guardado. <paramref name="debeActualizarse"/> indica
    /// que coincide pero está guardada en un formato viejo o más débil y conviene volver a
    /// guardarla con <see cref="Crear"/>: así las cuentas existentes se migran solas al iniciar
    /// sesión, sin pedir a nadie que cambie su contraseña.
    /// </summary>
    public static bool Verificar(string password, string guardado, out bool debeActualizarse)
    {
        debeActualizarse = false;

        var partes = guardado.Split('$');
        if (partes.Length == 4 && partes[0] == Prefijo
            && int.TryParse(partes[1], out var iteraciones) && iteraciones > 0)
        {
            byte[] sal, esperado;
            try
            {
                sal      = Convert.FromBase64String(partes[2]);
                esperado = Convert.FromBase64String(partes[3]);
            }
            catch (FormatException) { return false; }

            bool ok = CryptographicOperations.FixedTimeEquals(
                Derivar(password, sal, iteraciones, esperado.Length), esperado);
            debeActualizarse = ok && iteraciones < Iteraciones;
            return ok;
        }

        // Formato anterior: SHA-256 en hexadecimal, sin sal. Sólo se acepta para migrarlo.
        if (guardado.Length == 64 && guardado.All(Uri.IsHexDigit))
        {
            var legado = Convert.FromHexString(guardado);
            bool ok = CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(password)), legado);
            debeActualizarse = ok;
            return ok;
        }

        return false;
    }

    private static byte[] Derivar(string password, byte[] sal, int iteraciones, int bytes = BytesHash) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), sal, iteraciones,
                                  HashAlgorithmName.SHA256, bytes);
}
