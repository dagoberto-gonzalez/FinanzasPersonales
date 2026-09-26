using System.IO;
using FinanzasPersonales.Data;
using Microsoft.Data.Sqlite;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Base SQLite temporal y aislada para cada prueba. Se borra al terminar.
/// Depende de <c>AppDatabase(string? dbPath)</c> — sin ese parámetro nada de esto sería posible.
/// </summary>
public sealed class BaseDePrueba : IDisposable
{
    private readonly string _carpeta;

    public AppDatabase Db   { get; }
    public string      Ruta { get; }

    /// <summary>Usuario creado por defecto en cada base de prueba.</summary>
    public int UsuarioId { get; }

    public BaseDePrueba()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "fp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
        Ruta = Path.Combine(_carpeta, "prueba.db");

        Db        = new AppDatabase(Ruta);
        UsuarioId = Db.InsertarUsuario($"tester{Guid.NewGuid():N}".Substring(0, 12), "clave123").Id;
    }

    /// <summary>Ejecuta SQL directo. Para montar escenarios y para inyectar corrupción a propósito.</summary>
    public void Sql(string sql)
    {
        using var conn = new SqliteConnection($"Data Source={Ruta}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Sólo los problemas críticos. Útil cuando el escenario arrastra una advertencia conocida
    /// que no es lo que la prueba está comprobando.
    /// </summary>
    public List<Models.ProblemaIntegridad> Criticos() =>
        Db.VerificarIntegridad()
          .Where(p => p.Severidad == Models.SeveridadProblema.Critico)
          .ToList();

    /// <summary>Primer valor de una consulta escalar.</summary>
    public T Escalar<T>(string sql)
    {
        using var conn = new SqliteConnection($"Data Source={Ruta}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var r = cmd.ExecuteScalar();
        return r is null or DBNull ? default! : (T)Convert.ChangeType(r, typeof(T));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_carpeta, recursive: true); } catch { /* el SO lo limpiará */ }
    }
}
