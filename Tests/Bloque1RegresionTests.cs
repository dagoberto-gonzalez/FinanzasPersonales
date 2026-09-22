using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Regresiones de los defectos corregidos en el Bloque 1. Cada una nació como prueba en
/// <c>BugsConocidosTests</c> afirmando el comportamiento roto; aquí afirman el correcto.
/// </summary>
public class Bloque1RegresionTests
{
    // ── 1.4 · §2.2 — un ingreso no puede originarse en una tarjeta de crédito ──

    [Fact]
    public void InsertarTransaccion_IngresoConTarjeta_EsRechazado()
    {
        using var t = new BaseDePrueba();
        var tarjetaId = (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");

        var ex = Assert.Throws<ArgumentException>(() => t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Ingreso", Monto = 250000, Categoria = "Salario",
            Fecha = new DateTime(2026, 8, 10), TarjetaCreditoId = tarjetaId
        }, t.UsuarioId));

        Assert.Contains("tarjeta de crédito", ex.Message);
        Assert.Empty(t.Db.ObtenerTransacciones(t.UsuarioId)); // no se guardó nada
    }

    [Fact]
    public void GastoConTarjeta_SigueSiendoValido()
    {
        using var t = new BaseDePrueba();
        var tarjetaId = (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");
        t.Db.ObtenerTarjetas(t.UsuarioId);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 30000, Categoria = "Tecnología",
            Fecha = DateTime.Today, TarjetaCreditoId = tarjetaId
        }, t.UsuarioId);

        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
    }

    // ── 1.5 · §5 — el segundo día no puede repetir al primero ──

    [Fact]
    public void GastoFijo_ConDiasIguales_SeGuardaComoUnSoloDia()
    {
        using var t = new BaseDePrueba();
        var hoy = DateTime.Today;

        t.Db.InsertarGastoFijo(new GastoFijo
        {
            Nombre = "Alquiler", Monto = 300000, DiaVencimiento = 15, DiaVencimiento2 = 15
        }, t.UsuarioId);

        var filas = t.Db.ObtenerGastosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId);

        Assert.Single(filas);
        Assert.Equal(300000m, filas.Sum(f => f.Monto));   // ya no se duplica
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void GastoFijo_ConDosDiasDistintos_SigueGenerandoDosFilas()
    {
        using var t = new BaseDePrueba();
        var hoy = DateTime.Today;

        t.Db.InsertarGastoFijo(new GastoFijo
        {
            Nombre = "Alquiler", Monto = 150000, DiaVencimiento = 15, DiaVencimiento2 = 30
        }, t.UsuarioId);

        var filas = t.Db.ObtenerGastosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId);

        Assert.Equal(2, filas.Count);
        Assert.NotEqual(filas[0].PagoMensualId, filas[1].PagoMensualId);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void IngresoFijo_ConDiasIguales_SeGuardaComoUnSoloDia()
    {
        using var t = new BaseDePrueba();
        var hoy = DateTime.Today;

        t.Db.InsertarIngresoFijo(new IngresoFijo
        {
            Nombre = "Sueldo", Monto = 500000, DiaIngreso = 15, DiaIngreso2 = 15
        }, t.UsuarioId);

        Assert.Single(t.Db.ObtenerIngresosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    // ── 1.7 — categorías del sistema ──

    [Theory]
    [InlineData("Gastos Fijos")]
    [InlineData("Ingresos Fijos")]
    [InlineData("Salario")]
    [InlineData("Tarjeta")]
    public void CategoriasDelSistema_ExistenYSonFiltrables(string nombre)
    {
        using var t = new BaseDePrueba();
        Assert.Contains(nombre, t.Db.ObtenerCategorias(t.UsuarioId));
    }

    [Fact]
    public void PagoDeGastoFijo_QuedaEnUnaCategoriaQueExiste()
    {
        using var t = new BaseDePrueba();
        var hoy = DateTime.Today;

        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal" }, t.UsuarioId);
        var cuentaId = t.Db.ObtenerCuentas(t.UsuarioId).Single().Id;

        t.Db.InsertarGastoFijo(new GastoFijo
        {
            Nombre = "Internet", Monto = 25000, DiaVencimiento = 10
        }, t.UsuarioId);
        var pago = t.Db.ObtenerGastosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId).Single();
        t.Db.MarcarGastoFijoPagado(pago.PagoMensualId, true, "Principal", t.UsuarioId, cuentaId, null);

        var categoria = t.Db.ObtenerTransacciones(t.UsuarioId).Single().Categoria;
        Assert.Equal("Gastos Fijos", categoria);
        Assert.Contains(categoria, t.Db.ObtenerCategorias(t.UsuarioId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void CategoriaDelSistema_NoSePuedeRenombrarNiSiendoAdmin()
    {
        using var t = new BaseDePrueba();
        var id = (int)t.Escalar<long>("SELECT Id FROM Categorias WHERE Nombre='Gastos Fijos'");

        t.Db.ActualizarCategoria(id, "Otro nombre", t.UsuarioId, esAdmin: true);

        Assert.Contains("Gastos Fijos", t.Db.ObtenerCategorias(t.UsuarioId));
        Assert.DoesNotContain("Otro nombre", t.Db.ObtenerCategorias(t.UsuarioId));
    }

    [Fact]
    public void CategoriaDelSistema_NoSePuedeEliminarNiSiendoAdmin()
    {
        using var t = new BaseDePrueba();
        var id = (int)t.Escalar<long>("SELECT Id FROM Categorias WHERE Nombre='Salario'");

        t.Db.EliminarCategoria(id, t.UsuarioId, esAdmin: true);

        Assert.Contains("Salario", t.Db.ObtenerCategorias(t.UsuarioId));
    }

    [Fact]
    public void CategoriaNormal_SigueSiendoEditableYBorrable()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCategoria("Mascotas", t.UsuarioId);
        var id = (int)t.Escalar<long>("SELECT Id FROM Categorias WHERE Nombre='Mascotas'");

        t.Db.ActualizarCategoria(id, "Mascota", t.UsuarioId, esAdmin: false);
        Assert.Contains("Mascota", t.Db.ObtenerCategorias(t.UsuarioId));

        t.Db.EliminarCategoria(id, t.UsuarioId, esAdmin: false);
        Assert.DoesNotContain("Mascota", t.Db.ObtenerCategorias(t.UsuarioId));
    }

    // ── 1.3 · §2.5 — interpretación de importes ──

    [Theory]
    [InlineData("1000000",     1000000)]
    [InlineData("1.000.000",   1000000)]   // pre-rellenado con ToString("N0") en cultura es-ES
    [InlineData("1,000,000",   1000000)]   // separador de miles anglosajón
    [InlineData("1500,50",     1500.50)]   // decimal con coma
    [InlineData("1500.50",     1500.50)]   // decimal con punto (el que se rompía)
    [InlineData("25 000",      25000)]     // con espacios
    [InlineData("₡ 12.345",    12345)]     // con símbolo de moneda
    [InlineData("0,99",        0.99)]
    [InlineData("-2.500",     -2500)]
    public void Dinero_InterpretaLosFormatosReales(string texto, decimal esperado)
    {
        Assert.True(Dinero.TryParse(texto, out var v));
        Assert.Equal(esperado, v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData(null)]
    public void Dinero_RechazaLoQueNoEsUnImporte(string? texto)
    {
        Assert.False(Dinero.TryParse(texto, out _));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-100")]
    public void Dinero_TryParsePositivo_ExigeMayorQueCero(string texto)
    {
        Assert.False(Dinero.TryParsePositivo(texto, out _));
    }

    // ── 1.1 · §3.1 — registrar el ingreso laboral ya no borra la bitácora del mes ──

    [Fact]
    public void RegistrarIngresoLaboral_ConservaLosRegistrosDelMes()
    {
        using var t = new BaseDePrueba();
        var hoy = DateTime.Today;

        t.Db.InsertarOActualizarRegistroDia(new RegistroDiaLaboral
        {
            UsuarioId = t.UsuarioId,
            Fecha     = new DateTime(hoy.Year, hoy.Month, 3),
            HorasExtraDiurnas = 4
        });
        t.Db.InsertarOActualizarRegistroDia(new RegistroDiaLaboral
        {
            UsuarioId = t.UsuarioId,
            Fecha     = new DateTime(hoy.Year, hoy.Month, 4),
            HorasDobles = 2
        });

        Assert.Equal(2, t.Db.ObtenerRegistrosDia(t.UsuarioId, hoy.Year, hoy.Month).Count);

        t.Db.InsertarIngresoLaboralDirecto(
            t.UsuarioId, hoy, 450000, "Quincena", null);

        // Antes esto se acompañaba de ReiniciarRegistrosDia() y las horas desaparecían.
        Assert.Equal(2, t.Db.ObtenerRegistrosDia(t.UsuarioId, hoy.Year, hoy.Month).Count);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    // ── 1.6 · §7 — el pago de la tarjeta sale de una cuenta real ──

    [Fact]
    public void AbonarTarjeta_DescuentaDeLaCuentaElegida()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal" }, t.UsuarioId);

        var vm = new ViewModels.TarjetasViewModel(t.Db, t.UsuarioId);
        vm.Seleccionada = vm.Tarjetas.First();
        vm.CuentaAbono  = vm.CuentasDisponibles.Single();
        vm.AbonoMonto   = "50.000";          // con separador de miles, como se teclea

        vm.AbonarCommand.Execute(null);

        var movimiento = t.Db.ObtenerTransacciones(t.UsuarioId).Single();
        Assert.Equal(50000m, movimiento.Monto);
        Assert.Equal("Principal", movimiento.CuentaNombre);   // antes: "Banco BAC" fijo
        Assert.NotNull(movimiento.CuentaId);                  // antes: null, no descontaba

        // El pago sí mueve efectivo: la cuenta baja.
        Assert.Equal(-50000m, t.Db.ObtenerCuentas(t.UsuarioId).Single().SaldoActual);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void AbonarTarjeta_SinCuenta_SeRegistraComoEfectivo()
    {
        using var t = new BaseDePrueba();

        var vm = new ViewModels.TarjetasViewModel(t.Db, t.UsuarioId);
        vm.Seleccionada = vm.Tarjetas.First();
        vm.CuentaAbono  = null;              // no hay cuentas creadas
        vm.AbonoMonto   = "10000";

        vm.AbonarCommand.Execute(null);

        var movimiento = t.Db.ObtenerTransacciones(t.UsuarioId).Single();
        Assert.Equal("Efectivo", movimiento.CuentaNombre);
        Assert.Null(movimiento.CuentaId);
        Assert.Empty(t.Db.VerificarIntegridad());
    }
}
