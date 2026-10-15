using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using FinanzasPersonales.Data;
using Microsoft.Data.Sqlite;

namespace FinanzasPersonales.Services;

/// <summary>
/// Copias de seguridad de la base de datos.
/// <para>
/// Todas las copias usan la API de copia de SQLite (<see cref="SqliteConnection.BackupDatabase(SqliteConnection)"/>),
/// nunca <c>File.Copy</c>: copiar el archivo mientras hay una escritura a medias produce una
/// copia corrupta, y uno se entera el día que la necesita.
/// </para>
/// <para>
/// Las automáticas van a <c>Copias\</c>, junto a la base, con el nombre
/// <c>{base}_{aaaa-MM-dd}_{HHmmss}.db</c>; sólo esas se podan. Las que el usuario guarda en otro
/// sitio y las de "antes de restaurar" no se tocan nunca.
/// </para>
/// </summary>
public static class CopiasDeSeguridad
{
    /// <summary>Cuántas copias automáticas se conservan (una por día).</summary>
    public const int Conservar = 30;

    private const string FormatoFecha = "yyyy-MM-dd_HHmmss";

    public static string Carpeta(AppDatabase db) =>
        Path.Combine(Path.GetDirectoryName(db.RutaArchivo)!, "Copias");

    /// <summary>"finanzas" o "finanzas-dev": la versión de desarrollo tiene sus propias copias.</summary>
    private static string Base(AppDatabase db) => Path.GetFileNameWithoutExtension(db.RutaArchivo);

    private static Regex PatronAutomatica(AppDatabase db) =>
        new($"^{Regex.Escape(Base(db))}_(\\d{{4}}-\\d{{2}}-\\d{{2}}_\\d{{6}})\\.db$");

    /// <summary>Copia la base completa (todos los usuarios) a <paramref name="destino"/>.</summary>
    public static void Crear(AppDatabase db, string destino)
    {
        if (Path.GetFullPath(destino).Equals(Path.GetFullPath(db.RutaArchivo), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("No se puede guardar la copia encima de la base de datos en uso.");

        var dir = Path.GetDirectoryName(destino);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Si el destino ya existe (el usuario eligió sobrescribir), se reemplaza entero.
        if (File.Exists(destino)) File.Delete(destino);

        using var origen = new SqliteConnection($"Data Source={db.RutaArchivo}");
        // Sin pool: al cerrar se suelta el archivo, y se puede mover o copiar a un USB enseguida.
        using var copia  = new SqliteConnection($"Data Source={destino};Pooling=False");
        origen.Open();
        copia.Open();
        origen.BackupDatabase(copia);
    }

    /// <summary>
    /// La copia del día: si hoy todavía no se hizo, la hace y poda las más viejas.
    /// Devuelve la ruta creada, o <c>null</c> si ya había una de hoy.
    /// </summary>
    public static string? CopiaAutomatica(AppDatabase db, DateTime ahora)
    {
        var carpeta = Carpeta(db);
        Directory.CreateDirectory(carpeta);

        var hoy = $"{Base(db)}_{ahora:yyyy-MM-dd}_";
        if (Directory.EnumerateFiles(carpeta, hoy + "*.db").Any()) return null;

        var ruta = Path.Combine(carpeta, $"{Base(db)}_{ahora.ToString(FormatoFecha, CultureInfo.InvariantCulture)}.db");
        Crear(db, ruta);

        foreach (var vieja in Automaticas(db).Skip(Conservar))
            vieja.Delete();

        return ruta;
    }

    /// <summary>Copias automáticas, de la más nueva a la más vieja.</summary>
    public static List<FileInfo> Automaticas(AppDatabase db)
    {
        var carpeta = Carpeta(db);
        if (!Directory.Exists(carpeta)) return [];
        var patron = PatronAutomatica(db);
        return new DirectoryInfo(carpeta).EnumerateFiles("*.db")
            .Where(f => patron.IsMatch(f.Name))
            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Fecha de la copia automática más reciente, si hay alguna.</summary>
    public static DateTime? UltimaAutomatica(AppDatabase db)
    {
        var ultima = Automaticas(db).FirstOrDefault();
        if (ultima is null) return null;
        var m = PatronAutomatica(db).Match(ultima.Name);
        return DateTime.ParseExact(m.Groups[1].Value, FormatoFecha, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Por qué <paramref name="archivo"/> no sirve para restaurar, o <c>null</c> si sirve:
    /// tiene que ser una base SQLite sana y de esta aplicación.
    /// </summary>
    public static string? MotivoNoRestaurable(string archivo)
    {
        if (!File.Exists(archivo)) return "El archivo no existe.";
        try
        {
            using var c = new SqliteConnection($"Data Source={archivo};Mode=ReadOnly;Pooling=False");
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Usuarios','Transacciones')";
            if (Convert.ToInt32(cmd.ExecuteScalar()) < 2)
                return "El archivo no es una copia de DMT Finance.";
            cmd.CommandText = "PRAGMA integrity_check";
            var r = cmd.ExecuteScalar() as string;
            return r == "ok" ? null : "La copia está dañada y no se puede usar.";
        }
        catch (SqliteException)
        {
            return "El archivo no es una base de datos válida.";
        }
    }

    /// <summary>
    /// Reemplaza todos los datos por los de <paramref name="archivo"/>. Antes guarda lo actual en
    /// <c>Copias\{base}_antes-de-restaurar_…db</c> y devuelve esa ruta, para poder deshacerlo.
    /// <para>
    /// La aplicación debe cerrarse después: la copia puede ser de una versión anterior, y sus
    /// migraciones sólo corren al abrir la base.
    /// </para>
    /// </summary>
    public static string Restaurar(AppDatabase db, string archivo, DateTime ahora)
    {
        var motivo = MotivoNoRestaurable(archivo);
        if (motivo is not null) throw new InvalidOperationException(motivo);
        if (Path.GetFullPath(archivo).Equals(Path.GetFullPath(db.RutaArchivo), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ese archivo es la base de datos en uso, no una copia.");

        var seguridad = Path.Combine(Carpeta(db),
            $"{Base(db)}_antes-de-restaurar_{ahora.ToString(FormatoFecha, CultureInfo.InvariantCulture)}.db");
        Crear(db, seguridad);

        using (var origen  = new SqliteConnection($"Data Source={archivo};Mode=ReadOnly;Pooling=False"))
        using (var destino = new SqliteConnection($"Data Source={db.RutaArchivo}"))
        {
            origen.Open();
            destino.Open();
            origen.BackupDatabase(destino);
        }
        SqliteConnection.ClearAllPools();
        return seguridad;
    }
}
