using System.Windows;
using FinanzasPersonales.Models;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Lógica de la tarjeta "Diagnóstico de integridad" del panel de Admin.
/// (El renderizado del XAML no se cubre aquí: hay que verlo en la aplicación.)
/// </summary>
public class DiagnosticoAdminTests
{
    [Fact]
    public void AntesDeEjecutar_NoMuestraNada()
    {
        using var t  = new BaseDePrueba();
        var       vm = new AdminViewModel(t.Db);

        Assert.Empty(vm.Problemas);
        Assert.Equal(Visibility.Collapsed, vm.DiagnosticoResumenVisibility);
        Assert.Equal(Visibility.Collapsed, vm.DiagnosticoListaVisibility);
    }

    [Fact]
    public void BaseSana_ResumenEnVerdeYSinLista()
    {
        using var t  = new BaseDePrueba();
        var       vm = new AdminViewModel(t.Db);

        vm.EjecutarDiagnosticoCommand.Execute(null);

        Assert.Empty(vm.Problemas);
        Assert.Equal(Visibility.Visible,   vm.DiagnosticoResumenVisibility);
        Assert.Equal(Visibility.Collapsed, vm.DiagnosticoListaVisibility);
        Assert.Equal("#A6E3A1", vm.DiagnosticoColor);      // verde
        Assert.Contains("Sin discrepancias", vm.DiagnosticoResumen);
    }

    [Fact]
    public void ConProblemaCritico_ResumenEnRojoYListaVisible()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal" }, t.UsuarioId);
        var cuentaId = t.Db.ObtenerCuentas(t.UsuarioId).Single().Id;
        t.Sql($"UPDATE Cuentas SET SaldoActual = 50000 WHERE Id = {cuentaId}");

        var vm = new AdminViewModel(t.Db);
        vm.EjecutarDiagnosticoCommand.Execute(null);

        Assert.NotEmpty(vm.Problemas);
        Assert.Equal(Visibility.Visible, vm.DiagnosticoListaVisibility);
        Assert.Equal("#F38BA8", vm.DiagnosticoColor);      // rojo
        Assert.Contains("crítica", vm.DiagnosticoResumen);
        Assert.Contains(vm.Problemas, p => p.Severidad == SeveridadProblema.Critico);
    }

    [Fact]
    public void ConSoloAdvertencias_ResumenEnAmbar()
    {
        using var t = new BaseDePrueba();
        t.Sql($"""
            INSERT INTO Transacciones (UsuarioId,Tipo,Monto,Categoria,Descripcion,Fecha)
            VALUES ({t.UsuarioId},'Gasto',100,'CategoriaBorrada','x','2026-09-01')
            """);

        var vm = new AdminViewModel(t.Db);
        vm.EjecutarDiagnosticoCommand.Execute(null);

        Assert.NotEmpty(vm.Problemas);
        Assert.DoesNotContain(vm.Problemas, p => p.Severidad == SeveridadProblema.Critico);
        Assert.Equal("#FAB387", vm.DiagnosticoColor);      // ámbar
    }

    [Fact]
    public void EjecutarDosVeces_NoDuplicaLaLista()
    {
        using var t = new BaseDePrueba();
        t.Sql($"""
            INSERT INTO Transacciones (UsuarioId,Tipo,Monto,Categoria,Descripcion,Fecha)
            VALUES ({t.UsuarioId},'Gasto',100,'CategoriaBorrada','x','2026-09-01')
            """);

        var vm = new AdminViewModel(t.Db);
        vm.EjecutarDiagnosticoCommand.Execute(null);
        var primera = vm.Problemas.Count;
        vm.EjecutarDiagnosticoCommand.Execute(null);

        Assert.Equal(primera, vm.Problemas.Count);
    }
}
