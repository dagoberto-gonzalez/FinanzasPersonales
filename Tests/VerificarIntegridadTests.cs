using FinanzasPersonales.Models;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Valida el oráculo. Un verificador que nunca se prueba en rojo no sirve de nada:
/// cada prueba inyecta una corrupción concreta y exige que la detecte.
/// </summary>
public class VerificarIntegridadTests
{
    private static ProblemaIntegridad? Buscar(IEnumerable<ProblemaIntegridad> ps, string area, string fragmento) =>
        ps.FirstOrDefault(p => p.Area == area &&
                               p.Descripcion.Contains(fragmento, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void BaseReciennCreada_NoTieneProblemas()
    {
        using var t = new BaseDePrueba();
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void DetectaDerivaDeSaldoEnCuenta()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal" }, t.UsuarioId);
        var cuentaId = t.Db.ObtenerCuentas(t.UsuarioId).Single().Id;

        // Un gasto real de 900 …
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 900, Categoria = "Otros",
            Fecha = new DateTime(2026, 9, 1), CuentaId = cuentaId
        }, t.UsuarioId);

        // … pero el contador dice otra cosa (esto es la deriva que buscamos).
        t.Sql($"UPDATE Cuentas SET SaldoActual = 50000 WHERE Id = {cuentaId}");

        var p = Buscar(t.Db.VerificarIntegridad(), "Cuentas", "no coincide");
        Assert.NotNull(p);
        Assert.Equal(SeveridadProblema.Critico, p!.Severidad);
        Assert.Contains("50.900", p.Detalle); // 50000 - (-900)
    }

    [Fact]
    public void DetectaDerivaDeSaldoEnTarjeta()
    {
        using var t = new BaseDePrueba();
        var tarjetaId = t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");

        t.Sql($"UPDATE TarjetasCredito SET SaldoUsado = 12345 WHERE Id = {tarjetaId}");

        var p = Buscar(t.Db.VerificarIntegridad(), "Tarjetas", "no coincide");
        Assert.NotNull(p);
        Assert.Equal(SeveridadProblema.Critico, p!.Severidad);
    }

    [Fact]
    public void DetectaTransaccionConCuentaInexistente()
    {
        using var t = new BaseDePrueba();
        t.Sql($"""
            INSERT INTO Transacciones (UsuarioId,Tipo,Monto,Categoria,Descripcion,Fecha,CuentaId)
            VALUES ({t.UsuarioId},'Gasto',100,'Otros','fantasma','2026-09-01',9999)
            """);

        Assert.NotNull(Buscar(t.Db.VerificarIntegridad(), "Cuentas", "ya no existe"));
    }

    [Fact]
    public void DetectaTransaccionConTarjetaInexistente()
    {
        using var t = new BaseDePrueba();
        t.Sql($"""
            INSERT INTO Transacciones (UsuarioId,Tipo,Monto,Categoria,Descripcion,Fecha,TarjetaCreditoId)
            VALUES ({t.UsuarioId},'Gasto',100,'Otros','fantasma','2026-09-01',9999)
            """);

        Assert.NotNull(Buscar(t.Db.VerificarIntegridad(), "Tarjetas", "ya no existe"));
    }

    [Fact]
    public void DetectaPagoMarcadoSinTransaccion()
    {
        using var t = new BaseDePrueba();
        t.Sql($"""
            INSERT INTO PagosMensuales (UsuarioId,TipoFijo,FijoId,Anio,Mes,Dia,Pagado)
            VALUES ({t.UsuarioId},'Ingreso',1,2026,9,1,1)
            """);

        var p = Buscar(t.Db.VerificarIntegridad(), "Pagos fijos", "sin transacción asociada");
        Assert.NotNull(p);
        Assert.Equal(SeveridadProblema.Critico, p!.Severidad);
    }

    [Fact]
    public void DetectaIngresoRegistradoConTarjetaDeCredito()
    {
        using var t = new BaseDePrueba();
        var tarjetaId = t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");
        t.Sql($"""
            INSERT INTO Transacciones (UsuarioId,Tipo,Monto,Categoria,Descripcion,Fecha,TarjetaCreditoId)
            VALUES ({t.UsuarioId},'Ingreso',700,'Otros','sueldo','2026-09-01',{tarjetaId})
            """);

        var p = Buscar(t.Db.VerificarIntegridad(), "Transacciones", "Ingresos registrados");
        Assert.NotNull(p);
        Assert.Equal(SeveridadProblema.Critico, p!.Severidad);
    }

    // Desde el Bloque 1 · 1.5 InsertarGastoFijo/InsertarIngresoFijo normalizan el día repetido,
    // así que la corrupción se inyecta por SQL: el verificador tiene que seguir cazando las
    // filas que ya estuvieran mal en bases anteriores a esa corrección.

    [Fact]
    public void DetectaGastoFijoConSegundoDiaIgualAlPrimero()
    {
        using var t = new BaseDePrueba();
        t.Sql($"""
            INSERT INTO GastosFijos (UsuarioId,Nombre,Monto,DiaVencimiento,DiaVencimiento2,Activo)
            VALUES ({t.UsuarioId},'Luz',20000,15,15,1)
            """);

        var p = Buscar(t.Db.VerificarIntegridad(), "Gastos fijos", "segundo día igual");
        Assert.NotNull(p);
        Assert.Equal(SeveridadProblema.Critico, p!.Severidad);
    }

    [Fact]
    public void DetectaIngresoFijoConSegundoDiaIgualAlPrimero()
    {
        using var t = new BaseDePrueba();
        t.Sql($"""
            INSERT INTO IngresosFijos (UsuarioId,Nombre,Monto,DiaIngreso,DiaIngreso2,Activo)
            VALUES ({t.UsuarioId},'Sueldo',500000,15,15,1)
            """);

        Assert.NotNull(Buscar(t.Db.VerificarIntegridad(), "Ingresos fijos", "segundo día igual"));
    }

    [Fact]
    public void DetectaCategoriaHuerfana()
    {
        using var t = new BaseDePrueba();
        t.Sql($"""
            INSERT INTO Transacciones (UsuarioId,Tipo,Monto,Categoria,Descripcion,Fecha)
            VALUES ({t.UsuarioId},'Gasto',100,'CategoriaBorrada','x','2026-09-01')
            """);

        Assert.NotNull(Buscar(t.Db.VerificarIntegridad(), "Categorías", "ya no existe"));
    }

    [Fact]
    public void DetectaFilasDeUsuarioEliminado()
    {
        using var t = new BaseDePrueba();
        t.Sql("INSERT INTO Cuentas (UsuarioId,Nombre,Tipo,Banco,Activa) VALUES (4242,'Huerfana','Débito','',1)");

        Assert.NotNull(Buscar(t.Db.VerificarIntegridad(), "Usuarios", "Cuentas"));
    }

    [Fact]
    public void DetectaCierreMensualDesactualizado()
    {
        using var t = new BaseDePrueba();
        t.Sql($"""
            INSERT INTO ResumenMensual
                (UsuarioId,Anio,Mes,Ingresos,Gastos,Balance,BalanceAcumulado,NumTransacciones,FechaCierre)
            VALUES ({t.UsuarioId},2026,8,999999,111111,888888,888888,5,'01/09/2026')
            """);

        var p = Buscar(t.Db.VerificarIntegridad(), "Cierre mensual", "ya no coincide");
        Assert.NotNull(p);
        Assert.Contains("08/2026", p!.Descripcion);
    }

    [Fact]
    public void DetectaTransaccionConMontoNoPositivo()
    {
        using var t = new BaseDePrueba();
        t.Sql($"""
            INSERT INTO Transacciones (UsuarioId,Tipo,Monto,Categoria,Descripcion,Fecha)
            VALUES ({t.UsuarioId},'Gasto',0,'Otros','x','2026-09-01')
            """);

        Assert.NotNull(Buscar(t.Db.VerificarIntegridad(), "Transacciones", "cero o negativo"));
    }

    [Fact]
    public void VerificarIntegridad_NoModificaLaBase()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal" }, t.UsuarioId);
        var cuentaId = t.Db.ObtenerCuentas(t.UsuarioId).Single().Id;
        t.Sql($"UPDATE Cuentas SET SaldoActual = 777 WHERE Id = {cuentaId}");

        t.Db.VerificarIntegridad();
        t.Db.VerificarIntegridad();

        // El diagnóstico reporta, nunca repara.
        Assert.Equal(777.0, t.Escalar<double>($"SELECT SaldoActual FROM Cuentas WHERE Id={cuentaId}"));
    }
}
