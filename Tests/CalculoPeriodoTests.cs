using FinanzasPersonales.Models;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// El cálculo contra dos comprobantes reales de Dagoberto (Farmacia Central de Grecia). Si estas
/// pruebas pasan, la app calcula como la planilla; antes no coincidía en la base, en el valor de
/// la hora ni en la deducción.
/// <para>
/// La planilla redondea algunas líneas en un sentido y otras en el contrario (₡4.033,42 por
/// 1,5 × ₡2.688,95 = 4.033,425), así que se compara al céntimo con un margen de 2 céntimos.
/// </para>
/// </summary>
public class CalculoPeriodoTests
{
    private const decimal Centimos = 0.02m;

    private static ConfiguracionLaboral Config(decimal viaticos) => new()
    {
        SalarioMensual      = 430_230.76m,
        ModoPago            = "Quincenal",
        DiaPago             = 14,
        DiaPago2            = 28,
        JornadaSemanal      = 42.5m,
        DiasPorSemana       = 5,
        PorcentajeDeduccion = 10.83m,
        Viaticos            = viaticos
    };

    private static void Cerca(decimal esperado, decimal real) =>
        Assert.InRange(real, esperado - Centimos, esperado + Centimos);

    [Fact]
    public void LaHoraEsElMensualEntre240()
    {
        Assert.Equal(1_792.63m, Config(0).SalarioPorHora);
        // El "Valor Base" de cada línea del comprobante.
        Assert.Equal(2_688.95m, CalculoPeriodo.Tarifa(1_792.63m, CalculoPeriodo.FactorExtra));
        Assert.Equal(3_585.26m, CalculoPeriodo.Tarifa(1_792.63m, CalculoPeriodo.FactorDoble));
    }

    [Fact]
    public void ElSalarioFijoNoDependeDeLosDiasDelPeriodo()
    {
        // Del 16 al 30 de setiembre y del 16 al 31 de marzo se pagó lo mismo.
        Assert.Equal(215_115.38m, Config(0).SalarioFijoPorPeriodo);
    }

    /// <summary>Comprobante 15876: 16–31 de marzo de 2026, 1,5 h extra, viáticos ₡15.000.</summary>
    [Fact]
    public void ReproduceElComprobanteDeMarzo()
    {
        var c = CalculoPeriodo.De(Config(15_000m),
        [
            new RegistroDiaLaboral { Fecha = new DateTime(2026, 3, 20), HorasExtraDiurnas = 1.5 }
        ]);

        Cerca(215_115.38m, c.SalarioFijo);
        Cerca(4_033.42m,   c.MontoExtra);
        Cerca(219_148.80m, c.Bruto);
        Cerca(23_733.81m,  c.Deducciones);
        Cerca(210_414.99m, c.Neto);
    }

    /// <summary>Comprobante 16237: 16–30 de setiembre de 2026, 11 h dobles (domingo), viáticos ₡25.000.</summary>
    [Fact]
    public void ReproduceElComprobanteDeSetiembre()
    {
        var c = CalculoPeriodo.De(Config(25_000m),
        [
            new RegistroDiaLaboral { Fecha = new DateTime(2026, 9, 20), HorasDobles = 11 }
        ]);

        Cerca(39_437.86m,  c.MontoDobles);
        Cerca(254_553.24m, c.Bruto);
        Cerca(27_568.12m,  c.Deducciones);
        Cerca(251_985.12m, c.Neto);
    }

    [Fact]
    public void LosViaticosNoPaganDeduccion()
    {
        var sin = CalculoPeriodo.De(Config(0m),       []);
        var con = CalculoPeriodo.De(Config(15_000m),  []);

        Assert.Equal(sin.Bruto,       con.Bruto);
        Assert.Equal(sin.Deducciones, con.Deducciones);
        Assert.Equal(sin.Neto + 15_000m, con.Neto);
    }

    // ── Comparación con el comprobante ──

    private static ComprobantePago Setiembre() => new()
    {
        SalarioFijo = 215_115.38m,
        HorasDobles = 11, MontoDobles = 39_437.86m,
        Viaticos    = 25_000m,
        Deducciones = 27_568.12m
    };

    [Fact]
    public void ElComprobanteImprimeLosMismosTotales()
    {
        var p = Setiembre();
        Assert.Equal(254_553.24m, p.Bruto);
        Assert.Equal(251_985.12m, p.Neto);
    }

    [Fact]
    public void SiTodoCoincideTodasLasLineasCuadran()
    {
        var c = CalculoPeriodo.De(Config(25_000m),
            [new RegistroDiaLaboral { Fecha = new DateTime(2026, 9, 20), HorasDobles = 11 }]);

        Assert.All(LineaComparacion.Comparar(c, Setiembre()), l => Assert.True(l.Cuadra, l.Concepto));
    }

    /// <summary>"Me pagaron 11 h y registré 12": es justo lo que el módulo tiene que encontrar.</summary>
    [Fact]
    public void SeñalaLasHorasQueNoSePagaron()
    {
        var c = CalculoPeriodo.De(Config(25_000m),
            [new RegistroDiaLaboral { Fecha = new DateTime(2026, 9, 20), HorasDobles = 12 }]);

        var lineas = LineaComparacion.Comparar(c, Setiembre());
        var dobles = lineas.Single(l => l.Concepto == "Horas dobles");

        Assert.False(dobles.Cuadra);
        Assert.Equal(-1m, dobles.DiferenciaHoras);
        Cerca(-3_585.26m, dobles.Diferencia);
        Assert.False(lineas.Single(l => l.Concepto == "Monto neto").Cuadra);
        Assert.True(lineas.Single(l => l.Concepto == "Salario fijo").Cuadra);
    }
}
