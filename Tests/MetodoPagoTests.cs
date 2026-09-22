using FinanzasPersonales.Models;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Bloque 1 · 1.4 — el desplegable de método de pago según el tipo de transacción.
/// Cubre la lógica que el XAML consume; el renderizado hay que verlo en la aplicación.
/// </summary>
public class MetodoPagoTests
{
    [Fact]
    public void EnUnGasto_TarjetaEstaDisponible()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        Assert.True(vm.EsGasto);
        Assert.Contains("Tarjeta", vm.MetodosPago);
    }

    [Fact]
    public void AlPasarAIngreso_TarjetaDesaparece()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.EsIngreso = true;

        Assert.DoesNotContain("Tarjeta", vm.MetodosPago);
        Assert.Equal(["Efectivo", "Transferencia", "SINPE Móvil"], vm.MetodosPago);
    }

    [Fact]
    public void AlVolverAGasto_TarjetaReaparece()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.EsIngreso = true;
        vm.EsGasto   = true;

        Assert.Contains("Tarjeta", vm.MetodosPago);
    }

    [Fact]
    public void SiTarjetaEstabaSeleccionada_AlPasarAIngresoVuelveAEfectivo()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.MetodoPago = "Tarjeta";
        Assert.Equal("Tarjeta", vm.MetodoPago);

        vm.EsIngreso = true;

        Assert.Equal("Efectivo", vm.MetodoPago);
    }

    /// <summary>
    /// La causa real del campo en blanco: si la colección se vacía, WPF anula el SelectedItem
    /// y no vuelve a evaluar el binding al repoblarla. El método seleccionado tiene que estar
    /// SIEMPRE presente en la lista, en todo momento de la transición.
    /// </summary>
    [Fact]
    public void ElMetodoSeleccionadoSiempreEstaEnLaLista()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        void Comprobar() => Assert.Contains(vm.MetodoPago, vm.MetodosPago);

        Comprobar();
        vm.MetodoPago = "Tarjeta";      Comprobar();
        vm.EsIngreso  = true;           Comprobar();
        vm.EsGasto    = true;           Comprobar();
        vm.MetodoPago = "SINPE Móvil";  Comprobar();
        vm.EsIngreso  = true;           Comprobar();
    }

    /// <summary>
    /// El invariante que de verdad rompía la pantalla: un ComboBox al que le quitan el
    /// elemento que tiene seleccionado anula su selección por su cuenta y deja de leer el
    /// binding, así que el campo se ve en blanco aunque el ViewModel tenga un valor válido.
    /// La selección debe moverse ANTES de quitar el elemento.
    /// </summary>
    [Fact]
    public void NuncaSeQuitaDeLaListaElMetodoSeleccionado()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.MetodoPago = "Tarjeta";

        vm.MetodosPago.CollectionChanged += (_, e) =>
        {
            if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
                return;
            foreach (string quitado in e.OldItems!)
                Assert.NotEqual(vm.MetodoPago, quitado);
        };

        vm.EsIngreso = true;

        Assert.Equal("Efectivo", vm.MetodoPago);
    }

    /// <summary>
    /// La lista nunca debe quedar vacía: ese era el estado intermedio que rompía el ComboBox.
    /// </summary>
    [Fact]
    public void LaListaNuncaSeVacia()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.MetodosPago.CollectionChanged += (_, _) => Assert.NotEmpty(vm.MetodosPago);

        vm.EsIngreso = true;
        vm.EsGasto   = true;
    }

    [Fact]
    public void MetodoPagoNuncaQuedaNulo()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        // Es lo que hace WPF mientras la colección está vacía durante el Clear().
        vm.MetodoPago = null!;

        Assert.Equal("Efectivo", vm.MetodoPago);
    }

    [Fact]
    public void UnMetodoValidoQueNoEsTarjeta_SeConservaAlCambiarDeTipo()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.MetodoPago = "SINPE Móvil";
        vm.EsIngreso  = true;

        Assert.Equal("SINPE Móvil", vm.MetodoPago);
    }

    /// <summary>
    /// 1.8 — la categoría elegida se respeta en un ingreso. El desplegable estaba oculto en
    /// los ingresos pero Guardar() seguía escribiendo la categoría seleccionada, así que todos
    /// acababan en la primera de la lista ("Alimentación") sin que se viera en pantalla.
    /// </summary>
    [Fact]
    public void UnIngreso_ConservaLaCategoriaElegida()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.EsIngreso = true;
        vm.Categoria = "Salario";
        vm.Monto     = "450000";

        vm.GuardarCommand.Execute(null);

        var guardada = Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Equal("Ingreso",  guardada.Tipo);
        Assert.Equal("Salario",  guardada.Categoria);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    /// <summary>
    /// Las categorías que ofrece el formulario son las mismas en gasto y en ingreso: el modelo
    /// no distingue unas de otras, y ahora incluye las del sistema (Salario, Gastos Fijos…).
    /// </summary>
    [Fact]
    public void ElFormularioOfreceLasMismasCategoriasEnAmbosTipos()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        var enGasto = vm.CategoriasForm.ToList();
        vm.EsIngreso = true;

        Assert.Equal(enGasto, vm.CategoriasForm);
        Assert.Contains("Salario", vm.CategoriasForm);
    }

    /// <summary>
    /// La prueba de fondo de 1.4: por la ruta de la interfaz ya no se puede llegar a guardar
    /// un ingreso con tarjeta, así que <c>InsertarTransaccion</c> nunca llega a rechazarlo.
    /// </summary>
    [Fact]
    public void GuardarUnIngreso_NuncaAsociaUnaTarjeta()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal" }, t.UsuarioId);

        var vm = new TransaccionesViewModel(t.Db, t.UsuarioId);
        vm.MetodoPago = "Tarjeta";      // se elige con el tipo en Gasto
        vm.EsIngreso  = true;           // y luego se cambia a Ingreso
        vm.Monto      = "250000";
        vm.Categoria  = "Salario";

        vm.GuardarCommand.Execute(null);

        var guardada = Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Equal("Ingreso", guardada.Tipo);
        Assert.Null(guardada.TarjetaCreditoId);
        Assert.Empty(t.Db.VerificarIntegridad());
    }
}
