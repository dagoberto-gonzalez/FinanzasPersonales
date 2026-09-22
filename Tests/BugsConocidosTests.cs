using FinanzasPersonales.Models;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Prueba ejecutable de los defectos del diagnóstico que siguen SIN corregir.
/// <para>
/// ATENCIÓN: estas pruebas afirman el comportamiento <b>roto de hoy</b>, no el correcto.
/// Sirven como evidencia de que el problema es real y reproducible, no una teoría.
/// </para>
/// <para>
/// Al corregir cada defecto en su bloque, hay que <b>invertir la aserción</b> y mover la prueba
/// a <see cref="RutasDeDineroTests"/>. Que una de estas se ponga roja es la señal de que el
/// arreglo funcionó.
/// </para>
/// <para>
/// Ya corregidos y migrados: §2.2 (ingreso con tarjeta), §2.4 (crash al eliminar ingreso fijo),
/// §5 (días repetidos) y el hallazgo de las categorías del sistema — todos en el Bloque 1.
/// </para>
/// </summary>
public class BugsConocidosTests
{
    /// <summary>
    /// §2.1 — <c>ObtenerTarjetas()</c> es un SELECT que ESCRIBE: al cruzar el día de corte
    /// pone <c>SaldoUsado</c> en cero, aunque la deuda siga sin pagarse.
    /// Correcto: leer no debe modificar, y la deuda vive hasta que se paga.
    /// Se corrige en: Bloque 2 · decisión D3.
    /// </summary>
    [Fact]
    public void Bug_2_1_LeerLasTarjetasBorraElSaldoUsado()
    {
        using var t = new BaseDePrueba();
        var tarjetaId = (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");

        t.Db.ObtenerTarjetas(t.UsuarioId); // asienta el período

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 45000, Categoria = "Tecnología",
            Fecha = DateTime.Today, TarjetaCreditoId = tarjetaId
        }, t.UsuarioId);

        Assert.Equal(45000.0, t.Escalar<double>($"SELECT SaldoUsado FROM TarjetasCredito WHERE Id={tarjetaId}"));

        // Simula que pasó el día de corte: retrasamos la marca del último corte.
        t.Sql($"UPDATE TarjetasCredito SET FechaUltimoCorte='2000-01-01' WHERE Id={tarjetaId}");

        // Basta abrir cualquier pantalla que lea tarjetas para perder el dato.
        var tarjeta = t.Db.ObtenerTarjetas(t.UsuarioId).First(x => x.Id == tarjetaId);

        Assert.Equal(0m, tarjeta.SaldoUsado);                          // ← HOY (incorrecto)
        Assert.Single(t.Db.ObtenerTransaccionesPorTarjeta(tarjetaId));  // el cargo sigue ahí
        // Correcto sería: Assert.Equal(45000m, tarjeta.SaldoUsado);
    }

    /// <summary>
    /// §2.3 — Al desmarcar un ingreso fijo, la reversión lee el monto ACTUAL de
    /// <c>IngresosFijos</c> en vez del monto de la transacción que realmente se creó.
    /// Hoy es latente (no hay edición); se vuelve real en cuanto exista "editar ingreso fijo".
    /// Correcto: revertir con el importe de la transacción, como ya hace la versión de gastos.
    /// Se corrige en: Bloque 2 (antes de Bloque 4.2, que añade la edición).
    /// </summary>
    [Fact]
    public void Bug_2_3_RevertirIngresoFijoUsaElMontoActualNoElRegistrado()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal" }, t.UsuarioId);
        var cuentaId = t.Db.ObtenerCuentas(t.UsuarioId).Single().Id;
        var hoy      = DateTime.Today;

        t.Db.InsertarIngresoFijo(new IngresoFijo
        {
            Nombre = "Sueldo", Monto = 600000, DiaIngreso = 15, CuentaId = cuentaId
        }, t.UsuarioId);

        var pago = t.Db.ObtenerIngresosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId).Single();
        t.Db.MarcarIngresoFijoRecibido(pago.PagoMensualId, true, t.UsuarioId);

        Assert.Equal(600000m, t.Db.ObtenerCuentas(t.UsuarioId).Single().SaldoActual);

        // Simula la edición que el Bloque 4.2 va a introducir: baja el monto del ingreso fijo.
        t.Sql("UPDATE IngresosFijos SET Monto = 100000");

        // Al desmarcar revierte 100.000 en vez de los 600.000 que realmente entraron.
        t.Db.MarcarIngresoFijoRecibido(pago.PagoMensualId, false, t.UsuarioId);

        Assert.Equal(500000m, t.Db.ObtenerCuentas(t.UsuarioId).Single().SaldoActual); // ← HOY (incorrecto)
        Assert.NotEmpty(t.Db.VerificarIntegridad());                                  // el verificador lo caza
        // Correcto sería: Assert.Equal(0m, ...) y VerificarIntegridad() vacío.
    }
}
