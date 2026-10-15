using System.IO;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Copias de seguridad: la automática diaria, la manual y restaurar. Desde que la app guarda
/// dinero real, una copia que no se pueda restaurar es peor que no tenerla: da falsa calma.
/// </summary>
public class CopiasDeSeguridadTests
{
    private static readonly DateTime Dia = new(2026, 10, 14, 9, 30, 0);

    private static void Gasto(BaseDePrueba t, decimal monto) =>
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = monto, Categoria = "Alimentación", Fecha = DateTime.Today
        }, t.UsuarioId);

    private static int Transacciones(string ruta)
    {
        using var c = new SqliteConnection($"Data Source={ruta};Mode=ReadOnly;Pooling=False");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Transacciones";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    [Fact]
    public void LaCopiaTieneLosDatos()
    {
        using var t = new BaseDePrueba();
        Gasto(t, 1_000);
        var destino = Path.Combine(Path.GetDirectoryName(t.Ruta)!, "usb", "copia.db");

        CopiasDeSeguridad.Crear(t.Db, destino);

        Assert.Equal(1, Transacciones(destino));
        Assert.Null(CopiasDeSeguridad.MotivoNoRestaurable(destino));
    }

    [Fact]
    public void NoSePuedeGuardarEncimaDeLaBaseEnUso()
    {
        using var t = new BaseDePrueba();
        Assert.Throws<InvalidOperationException>(() => CopiasDeSeguridad.Crear(t.Db, t.Ruta));
    }

    [Fact]
    public void LaAutomaticaSeHaceUnaVezPorDia()
    {
        using var t = new BaseDePrueba();

        Assert.NotNull(CopiasDeSeguridad.CopiaAutomatica(t.Db, Dia));
        Assert.Null(CopiasDeSeguridad.CopiaAutomatica(t.Db, Dia.AddHours(8)));
        Assert.NotNull(CopiasDeSeguridad.CopiaAutomatica(t.Db, Dia.AddDays(1)));

        Assert.Equal(2, CopiasDeSeguridad.Automaticas(t.Db).Count);
        Assert.Equal(Dia.AddDays(1), CopiasDeSeguridad.UltimaAutomatica(t.Db));
    }

    [Fact]
    public void SeConservanLasUltimas30_YNoSeTocanLasDemas()
    {
        using var t = new BaseDePrueba();
        var carpeta = CopiasDeSeguridad.Carpeta(t.Db);
        Directory.CreateDirectory(carpeta);
        var manual = Path.Combine(carpeta, "mi copia.db");
        File.WriteAllText(manual, "no es de la poda");

        for (var i = 0; i < CopiasDeSeguridad.Conservar + 5; i++)
            CopiasDeSeguridad.CopiaAutomatica(t.Db, Dia.AddDays(i));

        var quedan = CopiasDeSeguridad.Automaticas(t.Db);
        Assert.Equal(CopiasDeSeguridad.Conservar, quedan.Count);
        Assert.Equal(Dia.AddDays(CopiasDeSeguridad.Conservar + 4), CopiasDeSeguridad.UltimaAutomatica(t.Db));
        Assert.True(File.Exists(manual));
    }

    [Fact]
    public void RestaurarDevuelveLosDatosDeLaCopia_YGuardaLoQueHabia()
    {
        using var t = new BaseDePrueba();
        Gasto(t, 1_000);
        var copia = Path.Combine(Path.GetDirectoryName(t.Ruta)!, "copia.db");
        CopiasDeSeguridad.Crear(t.Db, copia);

        Gasto(t, 2_000);   // lo que se hizo después de la copia
        Gasto(t, 3_000);

        var seguridad = CopiasDeSeguridad.Restaurar(t.Db, copia, Dia);

        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Equal(3, Transacciones(seguridad));
        // La de "antes de restaurar" no cuenta como automática: la poda no la borra.
        Assert.DoesNotContain(CopiasDeSeguridad.Automaticas(t.Db), f => f.FullName == seguridad);
    }

    [Fact]
    public void NoSeRestauraUnArchivoQueNoEsUnaCopia()
    {
        using var t = new BaseDePrueba();
        var dir = Path.GetDirectoryName(t.Ruta)!;

        var texto = Path.Combine(dir, "texto.db");
        File.WriteAllText(texto, "esto no es una base de datos, aunque se llame .db");
        Assert.NotNull(CopiasDeSeguridad.MotivoNoRestaurable(texto));

        var otraBase = Path.Combine(dir, "otra.db");
        using (var c = new SqliteConnection($"Data Source={otraBase};Pooling=False"))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "CREATE TABLE Cosas (Id INTEGER)";
            cmd.ExecuteNonQuery();
        }
        Assert.NotNull(CopiasDeSeguridad.MotivoNoRestaurable(otraBase));

        Gasto(t, 1_000);
        Assert.Throws<InvalidOperationException>(() => CopiasDeSeguridad.Restaurar(t.Db, otraBase, Dia));
        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));   // nada cambió
    }
}
