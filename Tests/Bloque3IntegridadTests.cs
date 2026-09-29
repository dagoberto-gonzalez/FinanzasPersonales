using FinanzasPersonales.Models;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Bloque 3 — integridad referencial. Los borrados dejaban filas apuntando al vacío; ahora se
/// niegan cuando romperían algo, y las limpiezas que sí proceden se hacen completas.
/// </summary>
public class Bloque3IntegridadTests
{
    private static int CrearCuenta(BaseDePrueba t, string nombre = "Principal")
    {
        t.Db.InsertarCuenta(new Cuenta { Nombre = nombre }, t.UsuarioId);
        return t.Db.ObtenerCuentas(t.UsuarioId).First(c => c.Nombre == nombre).Id;
    }

    // ── Cuentas ──

    [Fact]
    public void UnaCuentaSinMovimientos_SePuedeEliminar()
    {
        using var t = new BaseDePrueba();
        var id = CrearCuenta(t);

        t.Db.EliminarCuenta(id);

        Assert.Empty(t.Db.ObtenerCuentas(t.UsuarioId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void UnaCuentaConMovimientos_NoSeElimina()
    {
        using var t = new BaseDePrueba();
        var id = CrearCuenta(t);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 5000, Categoria = "Hogar",
            Fecha = new DateTime(2026, 9, 10), CuentaId = id
        }, t.UsuarioId);

        var ex = Assert.Throws<InvalidOperationException>(() => t.Db.EliminarCuenta(id));
        Assert.Contains("1 movimiento", ex.Message);

        // Y lo importante: nada quedó a medias.
        Assert.Single(t.Db.ObtenerCuentas(t.UsuarioId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void DesactivarEsLaAlternativa_YConservaElHistorial()
    {
        using var t = new BaseDePrueba();
        var id = CrearCuenta(t);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 5000, Categoria = "Hogar",
            Fecha = new DateTime(2026, 9, 10), CuentaId = id
        }, t.UsuarioId);

        var cuenta = t.Db.ObtenerCuentas(t.UsuarioId).Single();
        cuenta.Activa = false;
        t.Db.ActualizarCuenta(cuenta);

        var despues = t.Db.ObtenerCuentas(t.UsuarioId).Single();
        Assert.False(despues.Activa);
        Assert.Equal(-5000m, despues.SaldoActual);      // el saldo sigue siendo correcto
        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
    }

    [Fact]
    public void AlEliminarUnaCuenta_LosIngresosFijosQueLaUsabanSeDesvinculan()
    {
        using var t = new BaseDePrueba();
        var id = CrearCuenta(t);

        t.Db.InsertarIngresoFijo(new IngresoFijo
        {
            Nombre = "Sueldo", Monto = 600000, DiaIngreso = 15, CuentaId = id
        }, t.UsuarioId);

        t.Db.EliminarCuenta(id);   // sin movimientos, procede

        Assert.Equal(0, t.Escalar<long>($"SELECT COUNT(*) FROM IngresosFijos WHERE CuentaId={id}"));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    // ── Tarjetas ──

    [Fact]
    public void UnaTarjetaConCompras_NoSeElimina()
    {
        using var t = new BaseDePrueba();
        var tarjetaId = (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 30000, Categoria = "Tecnología",
            Fecha = DateTime.Today, TarjetaCreditoId = tarjetaId
        }, t.UsuarioId);

        var ex = Assert.Throws<InvalidOperationException>(() => t.Db.EliminarTarjeta(tarjetaId));
        Assert.Contains("sin forma de consultarse", ex.Message);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void UnaTarjetaConPagos_TampocoSeElimina()
    {
        using var t = new BaseDePrueba();
        var tarjetaId = (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 10000, Categoria = "Tecnología",
            Fecha = DateTime.Today, TarjetaCreditoId = tarjetaId
        }, t.UsuarioId);
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 10000, Categoria = "Tarjeta", Descripcion = "Pago",
            Fecha = DateTime.Today, PagoDeTarjetaId = tarjetaId
        }, t.UsuarioId);

        Assert.Equal(2, t.Db.ContarMovimientosDeTarjeta(tarjetaId));
        Assert.Throws<InvalidOperationException>(() => t.Db.EliminarTarjeta(tarjetaId));
    }

    [Fact]
    public void UnaTarjetaSinUsar_SePuedeEliminar()
    {
        using var t = new BaseDePrueba();
        var tarjetaId = (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");

        t.Db.EliminarTarjeta(tarjetaId);

        Assert.DoesNotContain(t.Db.ObtenerTarjetas(t.UsuarioId), x => x.Id == tarjetaId);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    // ── Categorías ──

    /// <summary>
    /// El defecto concreto: las transacciones guardan el NOMBRE, así que renombrar dejaba todo
    /// el pasado con la etiqueta vieja, fuera del desplegable de filtro.
    /// </summary>
    [Fact]
    public void RenombrarUnaCategoriaArrastraElHistorial()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCategoria("Mascotas", t.UsuarioId);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 8000, Categoria = "Mascotas",
            Fecha = new DateTime(2026, 9, 2)
        }, t.UsuarioId);

        var id = (int)t.Escalar<long>("SELECT Id FROM Categorias WHERE Nombre='Mascotas'");
        t.Db.ActualizarCategoria(id, "Mascota", t.UsuarioId, esAdmin: false);

        var trans = t.Db.ObtenerTransacciones(t.UsuarioId).Single();
        Assert.Equal("Mascota", trans.Categoria);                       // antes seguía en "Mascotas"
        Assert.Contains("Mascota", t.Db.ObtenerCategorias(t.UsuarioId));
        Assert.Empty(t.Db.VerificarIntegridad());                       // y no queda huérfana
    }

    [Fact]
    public void UnaCategoriaEnUso_NoSeElimina()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCategoria("Mascotas", t.UsuarioId);
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 8000, Categoria = "Mascotas",
            Fecha = new DateTime(2026, 9, 2)
        }, t.UsuarioId);

        var id = (int)t.Escalar<long>("SELECT Id FROM Categorias WHERE Nombre='Mascotas'");
        var ex = Assert.Throws<InvalidOperationException>(
            () => t.Db.EliminarCategoria(id, t.UsuarioId, esAdmin: false));

        Assert.Contains("1 transacción", ex.Message);
        Assert.Contains("Mascotas", t.Db.ObtenerCategorias(t.UsuarioId));
    }

    [Fact]
    public void UnaCategoriaSinUsar_SeEliminaSinProblema()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCategoria("Mascotas", t.UsuarioId);
        var id = (int)t.Escalar<long>("SELECT Id FROM Categorias WHERE Nombre='Mascotas'");

        t.Db.EliminarCategoria(id, t.UsuarioId, esAdmin: false);

        Assert.DoesNotContain("Mascotas", t.Db.ObtenerCategorias(t.UsuarioId));
    }

    // ── Usuarios ──

    /// <summary>
    /// El diálogo prometía eliminar "todos sus datos" pero limpiaba 7 de 13 tablas. Las otras 6
    /// quedaban como filas huérfanas de un usuario que ya no existía.
    /// </summary>
    [Fact]
    public void EliminarUnUsuarioNoDejaNingunaFilaSuelta()
    {
        using var t = new BaseDePrueba();
        var otro = t.Db.InsertarUsuario("victima" + Guid.NewGuid().ToString("N")[..6], "clave123").Id;

        // Datos repartidos por las tablas que antes NO se limpiaban.
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Suya" }, otro);
        t.Db.GuardarConfiguracionLaboral(new ConfiguracionLaboral { UsuarioId = otro, SalarioPorHora = 3000 });
        t.Db.InsertarOActualizarRegistroDia(new RegistroDiaLaboral
        {
            UsuarioId = otro, Fecha = new DateTime(2026, 9, 1), HorasExtraDiurnas = 3
        });
        t.Db.ConsolidarPeriodoLaboral(otro, 2026, 9, 800000, 86400, 713600);
        t.Db.InsertarIngresoLaboralDirecto(otro, new DateTime(2026, 9, 15), 400000, "Quincena", null);
        t.Db.CerrarMes(otro, 2026, 9);
        // Y algunas de las que sí se limpiaban.
        t.Db.InsertarCategoria("Propia", otro);
        t.Db.InsertarMeta(new MetaAhorro
        {
            Nombre = "Viaje", MontoObjetivo = 100000, FechaLimite = new DateTime(2027, 1, 1)
        }, otro);

        t.Db.EliminarUsuario(otro);

        foreach (var tabla in new[]
                 {
                     "Transacciones", "MetasAhorro", "TarjetasCredito", "GastosFijos",
                     "IngresosFijos", "PagosMensuales", "Categorias", "Cuentas",
                     "ConfiguracionLaboral", "RegistrosDiasLaborales", "PeriodosLaborales",
                     "IngresoLaboralDirecto", "ResumenMensual"
                 })
        {
            Assert.Equal(0, t.Escalar<long>($"SELECT COUNT(*) FROM {tabla} WHERE UsuarioId={otro}"));
        }

        Assert.DoesNotContain(t.Db.ObtenerUsuarios(), u => u.Id == otro);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    // ── Claves foráneas ──

    [Fact]
    public void ElEsquemaDeclaraLasClavesForaneas()
    {
        using var t = new BaseDePrueba();

        // Transacciones tiene cuatro: usuario, cuenta, tarjeta de compra y tarjeta de pago.
        Assert.Equal(4, t.Escalar<long>("SELECT COUNT(*) FROM pragma_foreign_key_list('Transacciones')"));

        foreach (var tabla in new[] { "Cuentas", "TarjetasCredito", "Categorias", "MetasAhorro",
                                      "GastosFijos", "IngresosFijos", "PagosMensuales",
                                      "ConfiguracionLaboral", "RegistrosDiasLaborales",
                                      "PeriodosLaborales", "IngresoLaboralDirecto", "ResumenMensual" })
            Assert.True(t.Escalar<long>($"SELECT COUNT(*) FROM pragma_foreign_key_list('{tabla}')") > 0,
                $"«{tabla}» debería declarar al menos una clave foránea.");
    }

    [Fact]
    public void LasClavesForaneasEstanActivadasEnCadaConexion()
    {
        using var t = new BaseDePrueba();

        // El ajuste es por conexión, no por base: si Abrir() no lo pusiera, las claves
        // declaradas no se comprobarían nunca.
        Assert.ThrowsAny<Microsoft.Data.Sqlite.SqliteException>(() => t.Sql(
            "INSERT INTO MetasAhorro (UsuarioId,Nombre,MontoObjetivo,FechaLimite) VALUES (9999,'X',1,'2027-01-01')"));
    }

    /// <summary>
    /// Lo que hace que la lista de tablas a mano deje de ser necesaria: el motor se encarga.
    /// </summary>
    [Fact]
    public void BorrarUnUsuarioArrastraSusDatosPorCascada()
    {
        using var t = new BaseDePrueba();
        var otro = t.Db.InsertarUsuario("cascada" + Guid.NewGuid().ToString("N")[..6], "clave123").Id;

        t.Db.InsertarCuenta(new Cuenta { Nombre = "Suya" }, otro);
        t.Db.InsertarMeta(new MetaAhorro
        {
            Nombre = "Viaje", MontoObjetivo = 100000, FechaLimite = new DateTime(2027, 1, 1)
        }, otro);

        // Se borra sólo la fila del usuario: el resto lo arrastra la cascada.
        t.Sql($"DELETE FROM Usuarios WHERE Id={otro}");

        Assert.Equal(0, t.Escalar<long>($"SELECT COUNT(*) FROM Cuentas WHERE UsuarioId={otro}"));
        Assert.Equal(0, t.Escalar<long>($"SELECT COUNT(*) FROM MetasAhorro WHERE UsuarioId={otro}"));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void LasCategoriasGlobalesSobrevivenAlBorrarUnUsuario()
    {
        using var t = new BaseDePrueba();
        var otro = t.Db.InsertarUsuario("global" + Guid.NewGuid().ToString("N")[..6], "clave123").Id;

        var globalesAntes = t.Escalar<long>("SELECT COUNT(*) FROM Categorias WHERE UsuarioId IS NULL");
        t.Db.EliminarUsuario(otro);

        // UsuarioId NULL significa "de todos": la cascada no debe llevárselas.
        Assert.Equal(globalesAntes, t.Escalar<long>("SELECT COUNT(*) FROM Categorias WHERE UsuarioId IS NULL"));
    }

    /// <summary>
    /// Con la clave foránea sola, borrar la transacción dejaría el pago marcado apuntando a nada
    /// — el estado que antes hacía reventar la aplicación al eliminar el fijo.
    /// </summary>
    [Fact]
    public void BorrarLaTransaccionDeUnGastoFijoLoDesmarca()
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

        // Se borra desde la pantalla de Transacciones, no desmarcando el fijo.
        var generada = t.Db.ObtenerTransacciones(t.UsuarioId).Single();
        t.Db.EliminarTransaccion(generada.Id);

        var despues = t.Db.ObtenerGastosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId).Single();
        Assert.False(despues.PagadoEsteMes);
        Assert.Equal(0m, t.Db.ObtenerCuentas(t.UsuarioId).Single().SaldoActual);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    /// <summary>
    /// La migración recrea catorce tablas, y corre en cada arranque de la aplicación. Si no
    /// detectara que ya se hizo, volvería a copiarlo todo cada vez que se abre.
    /// </summary>
    [Fact]
    public void AbrirLaBaseVariasVecesNoVuelveAMigrar()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal", SaldoInicial = 400000 }, t.UsuarioId);
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 15000, Categoria = "Hogar",
            Fecha = new DateTime(2026, 9, 4),
            CuentaId = t.Db.ObtenerCuentas(t.UsuarioId).Single().Id
        }, t.UsuarioId);

        for (int i = 0; i < 3; i++)
        {
            var otra = new FinanzasPersonales.Data.AppDatabase(t.Ruta);

            Assert.Single(otra.ObtenerCuentas(t.UsuarioId));
            Assert.Equal(385000m, otra.ObtenerCuentas(t.UsuarioId).Single().SaldoActual);
            Assert.Single(otra.ObtenerTransacciones(t.UsuarioId));
            Assert.Empty(otra.VerificarIntegridad());
        }

        // Y los índices siguen ahí: se pierden al recrear las tablas y hay que rehacerlos.
        Assert.Equal(1, t.Escalar<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_Transacciones_CuentaId'"));
    }

    [Fact]
    public void EliminarUnUsuarioNoTocaLosDatosDeOtro()
    {
        using var t = new BaseDePrueba();
        CrearCuenta(t, "Mía");
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 1000, Categoria = "Otros", Fecha = DateTime.Today
        }, t.UsuarioId);

        var otro = t.Db.InsertarUsuario("otro" + Guid.NewGuid().ToString("N")[..6], "clave123").Id;
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Suya" }, otro);

        t.Db.EliminarUsuario(otro);

        Assert.Single(t.Db.ObtenerCuentas(t.UsuarioId));
        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }
}
