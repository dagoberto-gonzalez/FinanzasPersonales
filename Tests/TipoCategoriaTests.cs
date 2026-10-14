using System.Collections.Specialized;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Categorías de gastos, de ingresos o de ambos. Antes al registrar un ingreso el desplegable
/// ofrecía "Alimentación", "Transporte"… y había que buscar "Salario" entre ellas.
/// </summary>
[Collection("Sesion")]
public class TipoCategoriaTests
{
    private static Transaccion Mov(string tipo, string categoria) => new()
    {
        Tipo = tipo, Monto = 1000, Categoria = categoria, Fecha = new DateTime(2026, 9, 10)
    };

    // ── Base de datos ──

    [Fact]
    public void LasCategoriasDeFabricaTienenSuTipo()
    {
        using var t = new BaseDePrueba();
        var cats = t.Db.ObtenerCategoriasDetalle(t.UsuarioId).ToDictionary(c => c.Nombre, c => c.Tipo);

        Assert.Equal(Categoria.TipoGasto,   cats["Alimentación"]);
        Assert.Equal(Categoria.TipoGasto,   cats["Gastos Fijos"]);
        Assert.Equal(Categoria.TipoIngreso, cats["Salario"]);
        Assert.Equal(Categoria.TipoIngreso, cats["Ingresos Fijos"]);
        Assert.Equal(Categoria.TipoAmbos,   cats["Otros"]);
    }

    [Fact]
    public void ParaUnIngresoSoloSeOfrecenLasDeIngresosYLasDeAmbos()
    {
        using var t = new BaseDePrueba();

        var ingresos = t.Db.ObtenerCategorias(t.UsuarioId, Categoria.TipoIngreso);
        var gastos   = t.Db.ObtenerCategorias(t.UsuarioId, Categoria.TipoGasto);

        Assert.Contains("Salario", ingresos);
        Assert.Contains("Otros", ingresos);
        Assert.DoesNotContain("Alimentación", ingresos);
        Assert.Contains("Alimentación", gastos);
        Assert.Contains("Otros", gastos);
        Assert.DoesNotContain("Salario", gastos);
    }

    /// <summary>
    /// Una base anterior no tenía la columna: las propias se clasifican por cómo se usaron, y la
    /// que nunca se usó queda en Ambos para no desaparecer de ninguna lista.
    /// </summary>
    [Fact]
    public void AlMigrarLasPropiasSeClasificanPorSuUso()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCategoria("Ventas",  t.UsuarioId);
        t.Db.InsertarCategoria("Regalos", t.UsuarioId);
        t.Db.InsertarCategoria("Mascota", t.UsuarioId);
        t.Db.InsertarCategoria("Nueva",   t.UsuarioId);
        t.Db.InsertarTransaccion(Mov("Ingreso", "Ventas"),  t.UsuarioId);
        t.Db.InsertarTransaccion(Mov("Ingreso", "Regalos"), t.UsuarioId);
        t.Db.InsertarTransaccion(Mov("Gasto",   "Regalos"), t.UsuarioId);
        t.Db.InsertarTransaccion(Mov("Gasto",   "Mascota"), t.UsuarioId);
        t.Sql("ALTER TABLE Categorias DROP COLUMN Tipo");   // como estaba antes

        var db   = new AppDatabase(t.Ruta);                  // vuelve a migrar
        var cats = db.ObtenerCategoriasDetalle(t.UsuarioId).ToDictionary(c => c.Nombre, c => c.Tipo);

        Assert.Equal(Categoria.TipoIngreso, cats["Ventas"]);
        Assert.Equal(Categoria.TipoAmbos,   cats["Regalos"]);
        Assert.Equal(Categoria.TipoGasto,   cats["Mascota"]);
        Assert.Equal(Categoria.TipoAmbos,   cats["Nueva"]);
        Assert.Equal(Categoria.TipoIngreso, cats["Salario"]);
        Assert.Equal(Categoria.TipoAmbos,   cats["Otros"]);
        Assert.Equal(Categoria.TipoGasto,   cats["Alimentación"]);
    }

    [Fact]
    public void UnaCategoriaDeSistemaNoCambiaDeTipo()
    {
        using var t = new BaseDePrueba();
        var salario = t.Db.ObtenerCategoriasDetalle(t.UsuarioId).Single(c => c.Nombre == "Salario");

        t.Db.CambiarTipoCategoria(salario.Id, Categoria.TipoGasto, t.UsuarioId, esAdmin: true);

        Assert.Equal(Categoria.TipoIngreso,
            t.Db.ObtenerCategoriasDetalle(t.UsuarioId).Single(c => c.Nombre == "Salario").Tipo);
    }

    [Fact]
    public void UnUsuarioNormalNoCambiaElTipoDeUnaGlobal()
    {
        using var t = new BaseDePrueba();
        var comida = t.Db.ObtenerCategoriasDetalle(t.UsuarioId).Single(c => c.Nombre == "Alimentación");

        t.Db.CambiarTipoCategoria(comida.Id, Categoria.TipoAmbos, t.UsuarioId, esAdmin: false);

        Assert.Equal(Categoria.TipoGasto,
            t.Db.ObtenerCategoriasDetalle(t.UsuarioId).Single(c => c.Nombre == "Alimentación").Tipo);
    }

    // ── Transacciones ──

    [Fact]
    public void AlPasarAIngresoElDesplegableSoloTieneCategoriasDeIngreso()
    {
        using var t = new BaseDePrueba();
        var vm = new TransaccionesViewModel(t.Db, t.UsuarioId);
        Assert.Contains("Alimentación", vm.CategoriasForm);

        vm.EsIngreso = true;

        Assert.DoesNotContain("Alimentación", vm.CategoriasForm);
        Assert.Contains("Salario", vm.CategoriasForm);
        Assert.Contains(vm.Categoria, vm.CategoriasForm);   // lo elegido sigue siendo válido

        vm.EsGasto = true;

        Assert.Contains("Alimentación", vm.CategoriasForm);
        Assert.DoesNotContain("Salario", vm.CategoriasForm);
        Assert.Contains(vm.Categoria, vm.CategoriasForm);
    }

    /// <summary>
    /// La restricción real de WPF, no el resultado final: si al ComboBox le quitan el elemento
    /// que tiene seleccionado, anula la selección y deja de leer el binding, y el campo queda en
    /// blanco aunque el ViewModel tenga un valor. Ya pasó con el método de pago.
    /// </summary>
    [Fact]
    public void AlCambiarDeTipoNuncaSeQuitaLaCategoriaSeleccionada()
    {
        using var t = new BaseDePrueba();
        var vm = new TransaccionesViewModel(t.Db, t.UsuarioId);
        vm.Categoria = "Alimentación";

        var quitadasSeleccionadas = new List<string>();
        vm.CategoriasForm.CollectionChanged += (_, e) =>
        {
            if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Reset)
                foreach (string quitada in e.OldItems ?? new List<string>())
                    if (quitada == vm.Categoria) quitadasSeleccionadas.Add(quitada);
            if (e.Action == NotifyCollectionChangedAction.Reset)
                quitadasSeleccionadas.Add("(Reset: vacía la lista entera)");
        };

        vm.EsIngreso = true;
        vm.EsGasto   = true;
        vm.EsIngreso = true;

        Assert.Empty(quitadasSeleccionadas);
    }

    [Fact]
    public void LaListaConservaElOrdenDeLasCategorias()
    {
        using var t = new BaseDePrueba();
        var vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.EsIngreso = true;
        vm.EsGasto   = true;

        var esperado = t.Db.ObtenerCategorias(t.UsuarioId, Categoria.TipoGasto).Where(c => c != "Tarjeta");
        Assert.Equal(esperado, vm.CategoriasForm);
    }

    // ── Pantalla de Categorías ──

    [Fact]
    public void UnaCategoriaCreadaComoIngresoApareceAlRegistrarUnIngreso()
    {
        using var t = new BaseDePrueba();
        SessionService.IniciarSesion(t.UsuarioId, "tester", "Normal");
        var cats = new CategoriasViewModel(t.Db, t.UsuarioId)
        {
            NuevaCategoria = "Ventas", NuevaTipo = Categoria.TipoIngreso
        };
        cats.AgregarCommand.Execute(null);

        var vm = new TransaccionesViewModel(t.Db, t.UsuarioId);
        Assert.DoesNotContain("Ventas", vm.CategoriasForm);
        vm.EsIngreso = true;
        Assert.Contains("Ventas", vm.CategoriasForm);
    }

    [Fact]
    public void EditarUnaFilaPermiteCambiarSoloElTipo()
    {
        using var t = new BaseDePrueba();
        SessionService.IniciarSesion(t.UsuarioId, "tester", "Normal");
        t.Db.InsertarCategoria("Ventas", t.UsuarioId);
        var cats = new CategoriasViewModel(t.Db, t.UsuarioId);
        var fila = cats.Categorias.Single(c => c.Nombre == "Ventas");

        fila.EditarCommand.Execute(null);
        fila.TipoEditado = Categoria.TipoIngreso;
        fila.GuardarCommand.Execute(null);

        Assert.Equal(Categoria.TipoIngreso, cats.Categorias.Single(c => c.Nombre == "Ventas").Tipo);
        Assert.Contains("ingresos", cats.MensajeOk);
    }
}
