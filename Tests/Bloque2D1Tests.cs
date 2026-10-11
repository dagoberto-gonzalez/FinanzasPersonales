using System.IO;
using FinanzasPersonales.Models;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Decisión D1 — el saldo de una cuenta se calcula (<c>SaldoInicial + Σ transacciones</c>)
/// en lugar de mantenerse como contador acumulado actualizado a mano en diez sitios.
/// </summary>
public class Bloque2D1Tests
{
    private static int CrearCuenta(BaseDePrueba t, decimal saldoInicial = 0, string nombre = "Principal")
    {
        t.Db.InsertarCuenta(new Cuenta { Nombre = nombre, SaldoInicial = saldoInicial }, t.UsuarioId);
        return t.Db.ObtenerCuentas(t.UsuarioId).First(c => c.Nombre == nombre).Id;
    }

    private static Cuenta Leer(BaseDePrueba t, int id) =>
        t.Db.ObtenerCuentas(t.UsuarioId).First(c => c.Id == id);

    // ── Saldo de apertura: antes era imposible ──

    [Fact]
    public void UnaCuentaNuevaPuedeAbrirConSaldo()
    {
        using var t = new BaseDePrueba();
        var id = CrearCuenta(t, saldoInicial: 500000);

        Assert.Equal(500000m, Leer(t, id).SaldoInicial);
        Assert.Equal(500000m, Leer(t, id).SaldoActual);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void ElSaldoInicialSeSumaALasTransacciones()
    {
        using var t = new BaseDePrueba();
        var id = CrearCuenta(t, saldoInicial: 500000);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 120000, Categoria = "Hogar",
            Fecha = new DateTime(2026, 9, 10), CuentaId = id
        }, t.UsuarioId);

        Assert.Equal(380000m, Leer(t, id).SaldoActual);
    }

    [Fact]
    public void ElSaldoInicialSePuedeCorregirDespues()
    {
        using var t = new BaseDePrueba();
        var id = CrearCuenta(t, saldoInicial: 100000);

        var cuenta = Leer(t, id);
        cuenta.SaldoInicial = 250000;
        t.Db.ActualizarCuenta(cuenta);

        // Antes de D1 no había forma de enderezar un saldo: ActualizarCuenta ni lo tocaba.
        Assert.Equal(250000m, Leer(t, id).SaldoActual);
    }

    // ── La deriva es imposible por construcción ──

    [Fact]
    public void ElSaldoSiempreCuadraConLasTransacciones()
    {
        using var t = new BaseDePrueba();
        var id = CrearCuenta(t, saldoInicial: 50000);

        for (int i = 1; i <= 5; i++)
            t.Db.InsertarTransaccion(new Transaccion
            {
                Tipo = i % 2 == 0 ? "Ingreso" : "Gasto", Monto = 10000 * i, Categoria = "Otros",
                Fecha = new DateTime(2026, 9, i), CuentaId = id
            }, t.UsuarioId);

        // -10.000 +20.000 -30.000 +40.000 -50.000 = -30.000, sobre 50.000 de apertura
        Assert.Equal(20000m, Leer(t, id).SaldoActual);

        // Y borrar transacciones sueltas sigue cuadrando, sin revertir nada a mano.
        var primera = t.Db.ObtenerTransacciones(t.UsuarioId).Last();
        t.Db.EliminarTransaccion(primera.Id);

        Assert.Equal(30000m, Leer(t, id).SaldoActual);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    /// <summary>
    /// Regresión de §2.3, que dejó de existir con D1: antes, desmarcar un ingreso fijo revertía
    /// el monto ACTUAL de <c>IngresosFijos</c> en vez del que se registró, y el saldo quedaba
    /// desviado. Ahora no se revierte nada — al borrarse la transacción deja de sumar sola.
    /// </summary>
    [Fact]
    public void CambiarElMontoDeUnIngresoFijoNoDesviaElSaldo()
    {
        using var t = new BaseDePrueba();
        var hoy = DateTime.Today;
        var id  = CrearCuenta(t);

        t.Db.InsertarIngresoFijo(new IngresoFijo
        {
            Nombre = "Sueldo", Monto = 600000, DiaIngreso = 15, CuentaId = id
        }, t.UsuarioId);

        var pago = t.Db.ObtenerIngresosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId).Single();
        t.Db.MarcarIngresoFijoRecibido(pago.PagoMensualId, true, t.UsuarioId);
        Assert.Equal(600000m, Leer(t, id).SaldoActual);

        // La edición que el Bloque 4.2 va a introducir: cambia el monto después de recibirlo.
        t.Sql("UPDATE IngresosFijos SET Monto = 100000");

        t.Db.MarcarIngresoFijoRecibido(pago.PagoMensualId, false, t.UsuarioId);

        Assert.Equal(0m, Leer(t, id).SaldoActual);   // antes de D1 quedaba en 500.000
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void BorrarUnGastoFijoConPagosNoDesviaElSaldo()
    {
        using var t = new BaseDePrueba();
        var hoy = DateTime.Today;
        var id  = CrearCuenta(t, saldoInicial: 100000);

        t.Db.InsertarGastoFijo(new GastoFijo
        {
            Nombre = "Internet", Monto = 25000, DiaVencimiento = 10
        }, t.UsuarioId);

        var pago = t.Db.ObtenerGastosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId).Single();
        t.Db.MarcarGastoFijoPagado(pago.PagoMensualId, true, "Principal", t.UsuarioId, id, null);
        Assert.Equal(75000m, Leer(t, id).SaldoActual);

        var gastoId = (int)t.Escalar<long>("SELECT Id FROM GastosFijos LIMIT 1");
        t.Db.EliminarGastoFijo(gastoId);

        Assert.Equal(100000m, Leer(t, id).SaldoActual);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    // ── La columna vieja ya no existe ──

    [Fact]
    public void LaTablaYaNoTieneColumnaSaldoActual()
    {
        using var t = new BaseDePrueba();

        var columnas = t.Escalar<long>(
            "SELECT COUNT(*) FROM pragma_table_info('Cuentas') WHERE name='SaldoActual'");

        Assert.Equal(0, columnas);
    }

    [Fact]
    public void ExistenLosIndicesQueSostienenElCalculo()
    {
        using var t = new BaseDePrueba();

        Assert.Equal(1, t.Escalar<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_Transacciones_CuentaId'"));
    }

    // ── Migración desde el esquema anterior ──

    /// <summary>
    /// Lo que no puede fallar: al migrar una base con el contador antiguo, el saldo que la
    /// persona veía en pantalla tiene que seguir siendo el mismo. La parte del acumulado que
    /// no se explica por las transacciones pasa a ser el saldo de apertura.
    /// </summary>
    [Fact]
    public void MigrarDesdeElEsquemaAntiguo_ConservaElSaldoQueSeVeia()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "fp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "vieja.db");

        try
        {
            // Base con el esquema anterior: Cuentas.SaldoActual como contador acumulado.
            using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={ruta}"))
            {
                c.Open();
                void X(string sql)
                {
                    using var k = c.CreateCommand();
                    k.CommandText = sql;
                    k.ExecuteNonQuery();
                }

                X("""
                  CREATE TABLE Cuentas (
                      Id INTEGER PRIMARY KEY AUTOINCREMENT, UsuarioId INTEGER NOT NULL,
                      Nombre TEXT NOT NULL, Tipo TEXT NOT NULL DEFAULT 'Débito',
                      Banco TEXT NOT NULL DEFAULT '', Activa INTEGER NOT NULL DEFAULT 1,
                      SaldoActual REAL NOT NULL DEFAULT 0, UNIQUE(UsuarioId, Nombre))
                  """);
                X("""
                  CREATE TABLE Transacciones (
                      Id INTEGER PRIMARY KEY AUTOINCREMENT, UsuarioId INTEGER NOT NULL DEFAULT 1,
                      Tipo TEXT NOT NULL, Monto REAL NOT NULL, Categoria TEXT NOT NULL,
                      Descripcion TEXT NOT NULL DEFAULT '', Fecha TEXT NOT NULL,
                      Notas TEXT NOT NULL DEFAULT '', CuentaNombre TEXT NOT NULL DEFAULT 'Efectivo',
                      TarjetaCreditoId INTEGER, CuentaId INTEGER)
                  """);

                // La cuenta abrió con 700.000 y desde entonces gastó 100.000:
                // el contador viejo marcaba 600.000.
                X("INSERT INTO Cuentas (Id,UsuarioId,Nombre,SaldoActual) VALUES (1,1,'Principal',600000)");
                X("""
                  INSERT INTO Transacciones (UsuarioId,Tipo,Monto,Categoria,Fecha,CuentaId)
                  VALUES (1,'Gasto',100000,'Hogar','2026-08-01',1)
                  """);
            }

            var db = new FinanzasPersonales.Data.AppDatabase(ruta);
            var cuenta = db.ObtenerCuentas(1).Single(x => x.Nombre == "Principal");

            Assert.Equal(700000m, cuenta.SaldoInicial);  // 600.000 acumulado + 100.000 gastados
            Assert.Equal(600000m, cuenta.SaldoActual);   // lo que la persona veía, intacto
            Assert.Empty(db.VerificarIntegridad());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(carpeta, recursive: true); } catch { }
        }
    }

    // ── La pantalla de Cuentas ──

    [Fact]
    public void CrearCuentaDesdeLaPantalla_RespetaElSaldoInicial()
    {
        using var t  = new BaseDePrueba();
        var       vm = new ViewModels.CuentasViewModel(t.Db, t.UsuarioId);

        vm.NuevaNombre       = "BAC Débito";
        vm.NuevoSaldoInicial = "1.250.000";   // con separador de miles, como se teclea
        vm.AgregarCommand.Execute(null);

        var cuenta = Assert.Single(vm.Cuentas);
        Assert.Equal(1250000m, cuenta.SaldoInicial);
        Assert.Equal(1250000m, cuenta.SaldoActual);
    }

    [Fact]
    public void EditarElSaldoInicialDesdeLaPantalla_RecalculaElSaldo()
    {
        using var t = new BaseDePrueba();
        var id = CrearCuenta(t, saldoInicial: 100000);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 30000, Categoria = "Otros",
            Fecha = new DateTime(2026, 9, 5), CuentaId = id
        }, t.UsuarioId);

        var vm = new ViewModels.CuentasViewModel(t.Db, t.UsuarioId);
        vm.Seleccionada     = vm.Cuentas.Single();
        Assert.Equal("100.000", vm.EditSaldoInicial);   // se precarga formateado

        vm.EditSaldoInicial = "200.000";
        vm.GuardarCambiosCommand.Execute(null);

        Assert.Equal(170000m, Leer(t, id).SaldoActual); // 200.000 - 30.000
    }

    [Fact]
    public void UnSaldoInicialInvalido_MuestraErrorYNoGuarda()
    {
        using var t  = new BaseDePrueba();
        var       vm = new ViewModels.CuentasViewModel(t.Db, t.UsuarioId);

        vm.NuevaNombre       = "Cuenta rara";
        vm.NuevoSaldoInicial = "abc";
        vm.AgregarCommand.Execute(null);

        Assert.Empty(vm.Cuentas);
        Assert.Contains("Saldo inicial", vm.NuevaError);
    }
}
