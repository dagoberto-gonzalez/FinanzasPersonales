using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// La pantalla de Categorías. La edición ocurre dentro de cada fila (<c>CategoriaVm</c>): antes
/// vivía en un panel al final de la pantalla que, con catorce categorías, quedaba fuera de vista.
/// <para><c>CategoriasViewModel</c> lee <c>SessionService.EsAdmin</c> en su constructor, así que
/// cada prueba declara con qué rol se abre.</para>
/// </summary>
[Collection("Sesion")]
public class CategoriasPantallaTests
{
    private static CategoriasViewModel AbrirComo(BaseDePrueba t, string rol)
    {
        SessionService.IniciarSesion(t.UsuarioId, "tester", rol);
        return new CategoriasViewModel(t.Db, t.UsuarioId);
    }

    private static CategoriaVm Fila(CategoriasViewModel vm, string nombre) =>
        vm.Categorias.First(c => c.Nombre == nombre);

    // ── Editar en la fila ──

    [Fact]
    public void AlPulsarEditarLaFilaSeVuelveEditable()
    {
        using var t  = new BaseDePrueba();
        var       vm = AbrirComo(t, "Admin");
        var       fila = Fila(vm, "Alimentación");

        Assert.False(fila.EnEdicion);
        fila.EditarCommand.Execute(null);

        Assert.True(fila.EnEdicion);
        Assert.Equal("Alimentación", fila.NombreEditado);   // precargado con el nombre actual
    }

    [Fact]
    public void UnAdminPuedeRenombrarUnaCategoriaGlobal()
    {
        using var t  = new BaseDePrueba();
        var       vm = AbrirComo(t, "Admin");
        var       fila = Fila(vm, "Alimentación");

        fila.EditarCommand.Execute(null);
        fila.NombreEditado = "Comida";
        fila.GuardarCommand.Execute(null);

        Assert.Contains(vm.Categorias, c => c.Nombre == "Comida");
        Assert.DoesNotContain(vm.Categorias, c => c.Nombre == "Alimentación");
        Assert.Contains("Comida", vm.MensajeOk);
    }

    [Fact]
    public void RenombrarDesdeLaPantallaArrastraElHistorial()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCategoria("Mascotas", t.UsuarioId);
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 8000, Categoria = "Mascotas", Fecha = new DateTime(2026, 9, 2)
        }, t.UsuarioId);

        var vm   = AbrirComo(t, "Admin");
        var fila = Fila(vm, "Mascotas");
        fila.EditarCommand.Execute(null);
        fila.NombreEditado = "Mascota";
        fila.GuardarCommand.Execute(null);

        Assert.Equal("Mascota", t.Db.ObtenerTransacciones(t.UsuarioId).Single().Categoria);
    }

    [Fact]
    public void CambiarSoloMayusculasNoSeConfundeConUnDuplicado()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCategoria("mascotas", t.UsuarioId);

        var vm   = AbrirComo(t, "Admin");
        var fila = Fila(vm, "mascotas");
        fila.EditarCommand.Execute(null);
        fila.NombreEditado = "Mascotas";
        fila.GuardarCommand.Execute(null);

        Assert.Contains(vm.Categorias, c => c.Nombre == "Mascotas");
    }

    [Fact]
    public void ElNombreDeOtraCategoriaSeRechazaEnLaPropiaFila()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCategoria("Mascotas", t.UsuarioId);

        var vm   = AbrirComo(t, "Admin");
        var fila = Fila(vm, "Mascotas");
        fila.EditarCommand.Execute(null);
        fila.NombreEditado = "Hogar";           // ya existe
        fila.GuardarCommand.Execute(null);

        // El aviso sale donde se está editando, no en una tarjeta lejana.
        Assert.Contains("Ya existe otra categoría", fila.MensajeError);
        Assert.True(fila.EnEdicion);            // sigue abierta para corregir
        Assert.Contains(vm.Categorias, c => c.Nombre == "Mascotas");
    }

    [Fact]
    public void CancelarDejaLaFilaComoEstaba()
    {
        using var t  = new BaseDePrueba();
        var       vm = AbrirComo(t, "Admin");
        var       fila = Fila(vm, "Hogar");

        fila.EditarCommand.Execute(null);
        fila.NombreEditado = "Casa";
        fila.CancelarCommand.Execute(null);

        Assert.False(fila.EnEdicion);
        Assert.Contains(vm.Categorias, c => c.Nombre == "Hogar");
    }

    [Fact]
    public void SoloUnaFilaEnEdicionALaVez()
    {
        using var t  = new BaseDePrueba();
        var       vm = AbrirComo(t, "Admin");
        var       primera = Fila(vm, "Hogar");
        var       segunda = Fila(vm, "Ropa");

        primera.EditarCommand.Execute(null);
        segunda.EditarCommand.Execute(null);

        Assert.False(primera.EnEdicion);
        Assert.True(segunda.EnEdicion);
    }

    // ── Qué filas ofrecen acciones ──

    [Fact]
    public void LasCategoriasDeSistemaNoOfrecenAcciones()
    {
        using var t  = new BaseDePrueba();
        var       vm = AbrirComo(t, "Admin");

        // Antes mostraban Editar y ✕, botones que sólo servían para dar un error.
        foreach (var nombre in AppDatabase.CategoriasSistema)
        {
            var fila = Fila(vm, nombre);
            Assert.True(fila.EsDeSistema);
            Assert.False(fila.PuedeEditarse);
        }
    }

    [Fact]
    public void UnUsuarioNormalNoPuedeEditarLasGlobales()
    {
        using var t  = new BaseDePrueba();
        var       vm = AbrirComo(t, "Normal");

        Assert.False(Fila(vm, "Alimentación").PuedeEditarse);
    }

    [Fact]
    public void UnUsuarioNormalPuedeEditarLasSuyas()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCategoria("Mascotas", t.UsuarioId);

        var vm   = AbrirComo(t, "Normal");
        var fila = Fila(vm, "Mascotas");

        Assert.False(fila.EsGlobal);
        Assert.True(fila.PuedeEditarse);

        fila.EditarCommand.Execute(null);
        fila.NombreEditado = "Mascota";
        fila.GuardarCommand.Execute(null);

        Assert.Contains(vm.Categorias, c => c.Nombre == "Mascota");
    }

    // ── Altas y bajas ──

    [Fact]
    public void CrearUnaCategoriaConfirma()
    {
        using var t  = new BaseDePrueba();
        var       vm = AbrirComo(t, "Admin");

        vm.NuevaCategoria = "Mascotas";
        vm.AgregarCommand.Execute(null);

        Assert.Contains(vm.Categorias, c => c.Nombre == "Mascotas");
        Assert.Contains("creada", vm.MensajeOk);
    }

    [Fact]
    public void UnaCategoriaEnUsoNoSeElimina()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCategoria("Mascotas", t.UsuarioId);
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 8000, Categoria = "Mascotas", Fecha = new DateTime(2026, 9, 2)
        }, t.UsuarioId);

        var vm = AbrirComo(t, "Admin");
        Fila(vm, "Mascotas").EliminarCommand.Execute(null);

        Assert.Contains("1 transacción", vm.MensajeError);
        Assert.Contains(vm.Categorias, c => c.Nombre == "Mascotas");
    }
}

/// <summary>SessionService es estático: estas pruebas no pueden correr en paralelo.</summary>
[CollectionDefinition("Sesion", DisableParallelization = true)]
public class SesionCollection { }
