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
/// §2.3 (reversión con el monto equivocado) dejó de existir con la decisión D1: al derivar el
/// saldo de las cuentas ya no hay ningún importe que revertir.
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

}
