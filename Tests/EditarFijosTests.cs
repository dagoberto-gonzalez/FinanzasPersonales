using FinanzasPersonales.Models;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Editar gastos e ingresos fijos. Lo delicado son los días: cada pago mensual se identifica por
/// su día, así que cambiarlo sin trasladar los pagos mostraría como pendiente algo ya pagado.
/// </summary>
public class EditarFijosTests
{
    private static readonly DateTime Hoy = DateTime.Today;

    private static int CrearCuenta(BaseDePrueba t, string nombre)
    {
        t.Db.InsertarCuenta(new Cuenta { Nombre = nombre }, t.UsuarioId);
        return t.Db.ObtenerCuentas(t.UsuarioId).First(c => c.Nombre == nombre).Id;
    }

    private static List<GastoFijo> Gastos(BaseDePrueba t, int anio, int mes) =>
        t.Db.ObtenerGastosFijosConEstado(anio, mes, t.UsuarioId);
    private static List<GastoFijo> GastosHoy(BaseDePrueba t) => Gastos(t, Hoy.Year, Hoy.Month);

    /// <summary>
    /// Desde hace tres meses: varias pruebas pagan meses anteriores, y un fijo no aparece en
    /// los meses previos a su alta.
    /// </summary>
    private static int CrearGasto(BaseDePrueba t, decimal monto, int dia, int dia2 = 0)
    {
        t.Db.InsertarGastoFijo(new GastoFijo
        {
            Nombre = "Alquiler", Monto = monto, DiaVencimiento = dia, DiaVencimiento2 = dia2,
            FechaInicio = Hoy.AddMonths(-3)
        }, t.UsuarioId);
        return GastosHoy(t).First().Id;
    }

    private static void Pagar(BaseDePrueba t, int dia, int anio = 0, int mes = 0)
    {
        var fila = (anio == 0 ? GastosHoy(t) : Gastos(t, anio, mes)).Single(g => g.DiaEspecifico == dia);
        t.Db.MarcarGastoFijoPagado(fila.PagoMensualId, true, "Efectivo", t.UsuarioId);
    }

    private static void Editar(BaseDePrueba t, int id, decimal monto, int dia, int dia2 = 0) =>
        t.Db.ActualizarGastoFijo(new GastoFijo { Id = id, Nombre = "Alquiler", Monto = monto, DiaVencimiento = dia, DiaVencimiento2 = dia2 }, t.UsuarioId);

    // ── Monto ──

    /// <summary>Lo ya pagado es una transacción con su monto: subir el alquiler no la reescribe.</summary>
    [Fact]
    public void SubirElMontoNoCambiaLoYaPagado()
    {
        using var t = new BaseDePrueba();
        var id = CrearGasto(t, 300_000, 5);
        Pagar(t, 5);

        Editar(t, id, 320_000, 5);

        Assert.Equal(300_000m, t.Db.ObtenerTransacciones(t.UsuarioId).Single().Monto);
        Assert.Equal(320_000m, GastosHoy(t).Single().Monto);
        Assert.True(GastosHoy(t).Single().PagadoEsteMes);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    // ── Días ──

    /// <summary>Sin trasladar el pago, el día 15 salía pendiente y se podía pagar dos veces.</summary>
    [Fact]
    public void CambiarElDiaConservaElPagoDelMes()
    {
        using var t = new BaseDePrueba();
        var id = CrearGasto(t, 25_000, 10);
        Pagar(t, 10);

        Editar(t, id, 25_000, 15);

        var fila = GastosHoy(t).Single();
        Assert.Equal(15, fila.DiaEspecifico);
        Assert.True(fila.PagadoEsteMes);
        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void CambiarElDiaTambienTrasladaLosMesesAnteriores()
    {
        using var t = new BaseDePrueba();
        var antes = Hoy.AddMonths(-1);
        var id    = CrearGasto(t, 25_000, 10);
        Pagar(t, 10, antes.Year, antes.Month);

        Editar(t, id, 25_000, 15);

        Assert.True(Gastos(t, antes.Year, antes.Month).Single().PagadoEsteMes);
    }

    /// <summary>Un día que se mantiene conserva sus pagos, aunque cambie de posición.</summary>
    [Fact]
    public void UnDiaQueSeMantieneConservaSuPago()
    {
        using var t = new BaseDePrueba();
        var id = CrearGasto(t, 50_000, 10);
        Pagar(t, 10);

        // Antes sólo el 10; ahora el 5 y el 10. El 10 sigue pagado; el 5 es nuevo.
        Editar(t, id, 50_000, 5, 10);

        var filas = GastosHoy(t);
        Assert.True (filas.Single(f => f.DiaEspecifico == 10).PagadoEsteMes);
        Assert.False(filas.Single(f => f.DiaEspecifico == 5).PagadoEsteMes);
    }

    [Fact]
    public void IntercambiarLosDosDiasNoChocaConLaClaveUnica()
    {
        using var t = new BaseDePrueba();
        var id = CrearGasto(t, 50_000, 10, 25);
        Pagar(t, 10);

        Editar(t, id, 50_000, 25, 10);   // mismos días, otro orden

        var filas = GastosHoy(t);
        Assert.True (filas.Single(f => f.DiaEspecifico == 10).PagadoEsteMes);
        Assert.False(filas.Single(f => f.DiaEspecifico == 25).PagadoEsteMes);
    }

    /// <summary>Quitar un día ya pagado: el pago fue dinero real y su transacción se queda.</summary>
    [Fact]
    public void QuitarUnDiaPagadoNoBorraSuTransaccion()
    {
        using var t = new BaseDePrueba();
        var id = CrearGasto(t, 50_000, 10, 25);
        Pagar(t, 25);

        Editar(t, id, 50_000, 10);

        Assert.Single(GastosHoy(t));
        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void SiElMismoMesYaTienePagosEnLosDosDiasNoSeJuntan()
    {
        using var t = new BaseDePrueba();
        var id = CrearGasto(t, 50_000, 10, 20);
        Pagar(t, 10);
        Pagar(t, 20);
        Editar(t, id, 50_000, 10);       // el 20 queda pagado pero oculto

        // Mover el 10 al 20 juntaría dos pagos del mismo mes en un solo día.
        Assert.Throws<InvalidOperationException>(() => Editar(t, id, 50_000, 20));

        // Y no cambió nada: la transacción entera se deshizo.
        var fila = GastosHoy(t).Single();
        Assert.Equal(10, fila.DiaEspecifico);
        Assert.True(fila.PagadoEsteMes);
    }

    [Fact]
    public void UnSegundoDiaIgualAlPrimeroSeDescarta()
    {
        using var t = new BaseDePrueba();
        var id = CrearGasto(t, 50_000, 10);

        Editar(t, id, 50_000, 12, 12);

        Assert.Equal(0, t.Db.ObtenerGastoFijo(id, t.UsuarioId)!.DiaVencimiento2);
    }

    [Fact]
    public void NoSeEditaElFijoDeOtroUsuario()
    {
        using var t = new BaseDePrueba();
        var otro = t.Db.InsertarUsuario("otro" + Guid.NewGuid().ToString("N")[..6], "clave123").Id;
        t.Db.InsertarGastoFijo(new GastoFijo { Nombre = "Suyo", Monto = 1000, DiaVencimiento = 5 }, otro);
        var suyo = t.Db.ObtenerGastosFijosConEstado(Hoy.Year, Hoy.Month, otro).Single().Id;

        Assert.Throws<InvalidOperationException>(() => Editar(t, suyo, 1, 5));
        Assert.Equal(1000m, t.Db.ObtenerGastosFijosConEstado(Hoy.Year, Hoy.Month, otro).Single().Monto);
    }

    // ── Borrar ──

    /// <summary>
    /// Borrar un fijo significa "ya no lo voy a pagar", no "nunca lo pagué". Antes se llevaba
    /// todas las transacciones de sus meses anteriores: del historial, los reportes y los saldos.
    /// </summary>
    [Fact]
    public void BorrarUnGastoFijoConservaSusPagosAnteriores()
    {
        using var t = new BaseDePrueba();
        var cuenta = CrearCuenta(t, "Principal");
        var antes  = Hoy.AddMonths(-1);
        var id     = CrearGasto(t, 25_000, 10);
        var fila   = Gastos(t, antes.Year, antes.Month).Single();
        t.Db.MarcarGastoFijoPagado(fila.PagoMensualId, true, "Principal", t.UsuarioId, cuenta, null);

        t.Db.EliminarGastoFijo(id);

        var tx = t.Db.ObtenerTransacciones(t.UsuarioId).Single();
        Assert.Equal(25_000m, tx.Monto);
        Assert.Equal("Alquiler", tx.Descripcion);
        Assert.Equal(-25_000m, t.Db.ObtenerCuentas(t.UsuarioId).Single().SaldoActual);
        Assert.Empty(GastosHoy(t));
        Assert.Equal(0, t.Escalar<long>("SELECT COUNT(*) FROM PagosMensuales"));
        // Y como ya no la genera ningún fijo, se edita como cualquier otra.
        Assert.Null(t.Db.MotivoNoEditable(tx.Id));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void BorrarUnIngresoFijoConservaLoRecibido()
    {
        using var t = new BaseDePrueba();
        var cuenta = CrearCuenta(t, "Nómina");
        t.Db.InsertarIngresoFijo(new IngresoFijo { Nombre = "Sueldo", Monto = 600_000, DiaIngreso = 15, CuentaId = cuenta }, t.UsuarioId);
        var fila = t.Db.ObtenerIngresosFijosConEstado(Hoy.Year, Hoy.Month, t.UsuarioId).Single();
        t.Db.MarcarIngresoFijoRecibido(fila.PagoMensualId, true, t.UsuarioId);

        t.Db.EliminarIngresoFijo(fila.Id);

        Assert.Equal(600_000m, t.Db.ObtenerCuentas(t.UsuarioId).Single().SaldoActual);
        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    // ── Ingresos fijos ──

    [Fact]
    public void CambiarLaCuentaDeUnIngresoValeDesdeElProximoCobro()
    {
        using var t = new BaseDePrueba();
        var vieja = CrearCuenta(t, "Vieja");
        var nueva = CrearCuenta(t, "Nueva");
        t.Db.InsertarIngresoFijo(new IngresoFijo { Nombre = "Sueldo", Monto = 600_000, DiaIngreso = 15, CuentaId = vieja }, t.UsuarioId);
        var fila = t.Db.ObtenerIngresosFijosConEstado(Hoy.Year, Hoy.Month, t.UsuarioId).Single();
        t.Db.MarcarIngresoFijoRecibido(fila.PagoMensualId, true, t.UsuarioId);

        t.Db.ActualizarIngresoFijo(new IngresoFijo
        {
            Id = fila.Id, Nombre = "Sueldo", Monto = 650_000, DiaIngreso = 30, CuentaId = nueva
        }, t.UsuarioId);

        var cuentas = t.Db.ObtenerCuentas(t.UsuarioId);
        Assert.Equal(600_000m, cuentas.Single(c => c.Id == vieja).SaldoActual);   // lo recibido, donde se recibió
        Assert.Equal(0m,       cuentas.Single(c => c.Id == nueva).SaldoActual);
        var ahora = t.Db.ObtenerIngresosFijosConEstado(Hoy.Year, Hoy.Month, t.UsuarioId).Single();
        Assert.Equal(30, ahora.DiaEspecifico);
        Assert.True(ahora.RecibidoEsteMes);                                       // el cobro se trasladó
        Assert.Equal(nueva, t.Db.ObtenerIngresoFijo(fila.Id, t.UsuarioId)!.CuentaId);
    }

    // ── Pantalla ──

    /// <summary>
    /// Un gasto con dos días aparece en dos filas, y la del segundo día no conoce el primero:
    /// por eso Editar lee el gasto de la base y no de la fila.
    /// </summary>
    [Fact]
    public void EditarDesdeLaFilaDelSegundoDiaCargaElGastoCompleto()
    {
        using var t = new BaseDePrueba();
        CrearGasto(t, 25_000, 10, 25);
        var vm = new GastosFijosViewModel(t.Db, t.UsuarioId);

        vm.EditarCommand.Execute(vm.GastosFijos.Single(g => g.DiaEspecifico == 25));

        Assert.True(vm.EstaEditando);
        Assert.Equal("Guardar cambios", vm.TextoGuardar);
        Assert.Equal("10", vm.DiaVencimiento);
        Assert.Equal("25", vm.DiaVencimiento2);
        Assert.Equal("Alquiler", vm.Nombre);
    }

    /// <summary>El monto se rellena como "25.000"; el decimal.TryParse de antes lo leía como 25.</summary>
    [Fact]
    public void GuardarSinCambiosNoAlteraElMonto()
    {
        using var t = new BaseDePrueba();
        CrearGasto(t, 25_000, 10);
        var vm = new GastosFijosViewModel(t.Db, t.UsuarioId);

        vm.EditarCommand.Execute(vm.GastosFijos.Single());
        vm.GuardarCommand.Execute(null);

        Assert.Equal(25_000m, GastosHoy(t).Single().Monto);
        Assert.False(vm.EstaEditando);
        Assert.Single(vm.GastosFijos);   // se editó, no se creó otro
    }

    [Fact]
    public void CancelarVuelveAlAlta()
    {
        using var t = new BaseDePrueba();
        CrearGasto(t, 25_000, 10);
        var vm = new GastosFijosViewModel(t.Db, t.UsuarioId);

        vm.EditarCommand.Execute(vm.GastosFijos.Single());
        vm.CancelarEdicionCommand.Execute(null);

        Assert.False(vm.EstaEditando);
        Assert.Equal("", vm.Nombre);
        Assert.Equal("Agregar", vm.TextoGuardar);
    }

    [Fact]
    public void EditarUnIngresoFijoDesdeLaPantallaSeleccionaSuCuenta()
    {
        using var t = new BaseDePrueba();
        var cuenta = CrearCuenta(t, "Nómina");
        t.Db.InsertarIngresoFijo(new IngresoFijo { Nombre = "Sueldo", Monto = 600_000, DiaIngreso = 15, CuentaId = cuenta }, t.UsuarioId);
        var vm = new IngresosFijosViewModel(t.Db, t.UsuarioId);

        vm.EditarCommand.Execute(vm.IngresosFijos.Single());
        Assert.Equal(cuenta, vm.CuentaSeleccionada?.Id);

        vm.Monto = "650.000";
        vm.GuardarCommand.Execute(null);

        Assert.Equal(650_000m, t.Db.ObtenerIngresoFijo(vm.IngresosFijos.Single().Id, t.UsuarioId)!.Monto);
        Assert.Single(vm.IngresosFijos);
    }
}
