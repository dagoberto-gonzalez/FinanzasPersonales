using FinanzasPersonales.Models;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Prueba ejecutable de los defectos del diagnóstico.
/// <para>
/// ATENCIÓN: estas pruebas afirman el comportamiento <b>roto de hoy</b>, no el correcto.
/// Sirven como evidencia de que el problema es real y reproducible, no una teoría.
/// </para>
/// <para>
/// Al corregir cada defecto en su bloque, hay que <b>invertir la aserción</b> y mover la prueba
/// a <see cref="RutasDeDineroTests"/>. Que una de estas se ponga roja es la señal de que el
/// arreglo funcionó.
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

        Assert.Equal(0m, tarjeta.SaldoUsado);                       // ← HOY (incorrecto)
        Assert.Single(t.Db.ObtenerTransaccionesPorTarjeta(tarjetaId)); // el cargo sigue ahí
        // Correcto sería: Assert.Equal(45000m, tarjeta.SaldoUsado);
    }

    /// <summary>
    /// §2.2 — Un Ingreso guardado con una tarjeta de crédito como origen queda excluido de
    /// TODOS los totales (balance, reportes, cierre de mes), aunque se vea en la lista.
    /// Correcto: un ingreso no puede originarse en una tarjeta de crédito.
    /// Se corrige en: Bloque 1 (guard en la UI) y Bloque 2 · decisión D2 (modelo).
    /// </summary>
    [Fact]
    public void Bug_2_2_UnIngresoConTarjetaDesapareceDeLosTotales()
    {
        using var t = new BaseDePrueba();
        var tarjetaId = (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Ingreso", Monto = 250000, Categoria = "Salario",
            Fecha = new DateTime(2026, 8, 10), TarjetaCreditoId = tarjetaId
        }, t.UsuarioId);

        // Está guardado y se ve en la lista…
        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));

        // … pero no suma en ningún total.
        var totales = t.Db.ObtenerTotalesMes(t.UsuarioId, 2026, 8);
        Assert.Equal(0m, totales.Ingresos);   // ← HOY (incorrecto)
        Assert.Equal(0,  totales.NumTrans);

        t.Db.CerrarMes(t.UsuarioId, 2026, 8);
        Assert.Equal(0m, t.Db.ObtenerResumenesMensuales(t.UsuarioId).Single().Ingresos);
        // Correcto sería: Assert.Equal(250000m, totales.Ingresos);
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

        var saldoTrasRecibir = t.Db.ObtenerCuentas(t.UsuarioId).Single().SaldoActual;
        Assert.Equal(600000m, saldoTrasRecibir);

        // Simula la edición que el Bloque 4.2 va a introducir: baja el monto del ingreso fijo.
        t.Sql("UPDATE IngresosFijos SET Monto = 100000");

        // Al desmarcar revierte 100.000 en vez de los 600.000 que realmente entraron.
        t.Db.MarcarIngresoFijoRecibido(pago.PagoMensualId, false, t.UsuarioId);

        Assert.Equal(500000m, t.Db.ObtenerCuentas(t.UsuarioId).Single().SaldoActual); // ← HOY (incorrecto)
        Assert.NotEmpty(t.Db.VerificarIntegridad());                                  // el verificador lo caza
        // Correcto sería: Assert.Equal(0m, ...) y VerificarIntegridad() vacío.
    }

    /// <summary>
    /// §2.4 — <c>EliminarIngresoFijo</c> hace <c>GetInt64</c> sobre un <c>TransaccionId</c> que
    /// puede ser NULL (filas migradas desde el esquema antiguo de PagosMensuales), y revienta.
    /// La versión de gastos sí filtra <c>TransaccionId IS NOT NULL</c>; esta se olvidó.
    /// Correcto: no debe lanzar.
    /// Se corrige en: Bloque 1 · ítem 1.2.
    /// </summary>
    [Fact]
    public void Bug_2_4_EliminarIngresoFijoRevientaConPagosLegados()
    {
        using var t = new BaseDePrueba();
        var hoy = DateTime.Today;

        t.Db.InsertarIngresoFijo(new IngresoFijo
        {
            Nombre = "Sueldo", Monto = 600000, DiaIngreso = 15
        }, t.UsuarioId);

        var ingresoId = (int)t.Escalar<long>("SELECT Id FROM IngresosFijos LIMIT 1");
        t.Db.ObtenerIngresosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId); // crea el PagoMensual

        // Estado que deja la migración PagosMensuales_v2: Pagado=1 pero sin TransaccionId.
        t.Sql("UPDATE PagosMensuales SET Pagado = 1, TransaccionId = NULL");

        var ex = Assert.Throws<InvalidOperationException>(() => t.Db.EliminarIngresoFijo(ingresoId)); // ← HOY
        Assert.Contains("NULL", ex.Message);
        // Correcto sería: t.Db.EliminarIngresoFijo(ingresoId); sin excepción.
    }

    /// <summary>
    /// HALLAZGO NUEVO (lo destapó la suite, no estaba en el diagnóstico inicial).
    /// <para>
    /// Las transacciones que genera la propia aplicación usan categorías fijas escritas a mano
    /// —«Gastos Fijos», «Ingresos Fijos», «Salario», «Tarjeta»— que <b>nunca se siembran</b> en
    /// la tabla <c>Categorias</c>. Sólo se siembran las 10 genéricas (Alimentación, Transporte…).
    /// </para>
    /// <para>
    /// Consecuencia: esas categorías aparecen en el gráfico de torta (que agrupa por texto) pero
    /// NO en el desplegable de filtro de Transacciones, así que no se puede filtrar por ellas
    /// —y suelen ser las de mayor importe—.
    /// </para>
    /// Correcto: sembrarlas como categorías globales, o marcarlas como categorías de sistema.
    /// Se corrige en: pendiente de asignar (candidato a Bloque 1).
    /// </summary>
    [Fact]
    public void Bug_6_LasCategoriasDelSistemaNoExistenEnLaTabla()
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

        // La transacción quedó en «Gastos Fijos» …
        Assert.Equal("Gastos Fijos", t.Db.ObtenerTransacciones(t.UsuarioId).Single().Categoria);

        // … pero esa categoría no existe, así que no aparece en el desplegable de filtro.
        Assert.DoesNotContain("Gastos Fijos", t.Db.ObtenerCategorias(t.UsuarioId));   // ← HOY

        var p = t.Db.VerificarIntegridad()
                 .FirstOrDefault(x => x.Area == "Categorías");
        Assert.NotNull(p);
        // Correcto sería: Assert.Contains("Gastos Fijos", t.Db.ObtenerCategorias(t.UsuarioId));
    }

    /// <summary>
    /// §5 — Un fijo con el segundo día igual al primero produce DOS filas en la interfaz que
    /// comparten el MISMO PagoMensual: marcar una marca las dos y el total se cuenta doble.
    /// Correcto: la interfaz debe rechazar <c>Dia2 == Dia</c>.
    /// Se corrige en: Bloque 1 · ítem 1.5.
    /// </summary>
    [Fact]
    public void Bug_5_GastoFijoConDiasIgualesDuplicaLaFilaYCompartePago()
    {
        using var t = new BaseDePrueba();
        var hoy = DateTime.Today;

        t.Db.InsertarGastoFijo(new GastoFijo
        {
            Nombre = "Alquiler", Monto = 300000, DiaVencimiento = 15, DiaVencimiento2 = 15
        }, t.UsuarioId);

        var filas = t.Db.ObtenerGastosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId);

        Assert.Equal(2, filas.Count);                                          // ← HOY: dos filas
        Assert.Equal(filas[0].PagoMensualId, filas[1].PagoMensualId);          // ← con el mismo pago
        Assert.Equal(600000m, filas.Sum(f => f.Monto));                        // ← total duplicado
        // Correcto sería: que InsertarGastoFijo no acepte DiaVencimiento2 == DiaVencimiento.
    }
}
