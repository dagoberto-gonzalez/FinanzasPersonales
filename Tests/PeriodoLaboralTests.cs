using FinanzasPersonales.Models;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Cálculo del período laboral. Es la pieza de la que depende todo lo demás del módulo, y la
/// que más fácilmente se equivoca en silencio: un día en el período equivocado desplaza horas
/// extra de una quincena a otra.
/// </summary>
public class PeriodoLaboralTests
{
    /// <summary>La configuración real de Dagoberto: quincenal, cortes el 14 y el 28.</summary>
    private static ConfiguracionLaboral Quincenal() => new()
    {
        SalarioMensual = 430_230.76m,
        JornadaSemanal = 42.5m,
        DiasPorSemana  = 5,
        ModoPago       = "Quincenal",
        DiaPago        = 14,
        DiaPago2       = 28
    };

    // ── Los cortes caen donde deben ──

    [Theory]
    [InlineData(2026, 9, 20, 2026, 9, 15, 2026, 9, 28)]   // dentro de la segunda quincena
    [InlineData(2026, 9, 28, 2026, 9, 15, 2026, 9, 28)]   // el propio día de corte, que la cierra
    [InlineData(2026, 9, 29, 2026, 9, 29, 2026, 10, 14)]  // el día siguiente ya es otra
    [InlineData(2026, 9, 14, 2026, 8, 29, 2026, 9, 14)]   // el otro corte
    [InlineData(2026, 9, 15, 2026, 9, 15, 2026, 9, 28)]
    [InlineData(2026, 10, 1, 2026, 9, 29, 2026, 10, 14)]  // el período cruza el cambio de mes
    public void CadaFechaCaeEnSuPeriodo(int a, int m, int d,
                                        int ai, int mi, int di, int af, int mf, int df)
    {
        var (inicio, fin) = Quincenal().PeriodoQueContiene(new DateTime(a, m, d));

        Assert.Equal(new DateTime(ai, mi, di), inicio);
        Assert.Equal(new DateTime(af, mf, df), fin);
    }

    /// <summary>
    /// Febrero era la pregunta difícil: no existe el 29 en que debería empezar el período. Al
    /// definirlo como "el día siguiente al corte anterior" se resuelve solo.
    /// </summary>
    [Fact]
    public void FebreroNoNecesitaReglaEspecial()
    {
        var cfg = Quincenal();

        var (i1, f1) = cfg.PeriodoQueContiene(new DateTime(2026, 2, 20));
        Assert.Equal(new DateTime(2026, 2, 15), i1);
        Assert.Equal(new DateTime(2026, 2, 28), f1);   // el corte cae en el último día del mes

        var (i2, f2) = cfg.PeriodoQueContiene(new DateTime(2026, 3, 5));
        Assert.Equal(new DateTime(2026, 3, 1), i2);    // el día siguiente al 28 de febrero
        Assert.Equal(new DateTime(2026, 3, 14), f2);
    }

    [Fact]
    public void EnAnioBisiestoElVeintinueveDeFebreroTambienEncaja()
    {
        var (inicio, fin) = Quincenal().PeriodoQueContiene(new DateTime(2028, 2, 29));

        Assert.Equal(new DateTime(2028, 2, 29), inicio);
        Assert.Equal(new DateTime(2028, 3, 14), fin);
    }

    /// <summary>Lo esencial: ningún día puede quedar fuera, ni caer en dos períodos a la vez.</summary>
    [Fact]
    public void TodoDiaDelAnioPerteneceAExactamenteUnPeriodo()
    {
        var cfg   = Quincenal();
        var fecha = new DateTime(2026, 1, 1);

        while (fecha < new DateTime(2029, 1, 1))
        {
            var (inicio, fin) = cfg.PeriodoQueContiene(fecha);

            Assert.True(inicio <= fecha, $"{fecha:yyyy-MM-dd} queda antes de su período ({inicio:yyyy-MM-dd}).");
            Assert.True(fecha <= fin,    $"{fecha:yyyy-MM-dd} queda después de su período ({fin:yyyy-MM-dd}).");

            // Y el período siguiente empieza justo al día siguiente: sin huecos ni solapes.
            var (inicioSig, _) = cfg.PeriodoSiguiente(fin);
            Assert.Equal(fin.AddDays(1), inicioSig);

            fecha = fecha.AddDays(1);
        }
    }

    [Fact]
    public void NavegarAtrasYAdelanteVuelveAlMismoPeriodo()
    {
        var cfg = Quincenal();
        var (inicio, fin) = cfg.PeriodoQueContiene(new DateTime(2026, 9, 20));

        var (inicioAnt, finAnt) = cfg.PeriodoAnterior(inicio);
        Assert.Equal(new DateTime(2026, 8, 29), inicioAnt);
        Assert.Equal(inicio.AddDays(-1), finAnt);        // el anterior termina justo antes

        var (inicioVuelta, finVuelta) = cfg.PeriodoSiguiente(finAnt);
        Assert.Equal(inicio, inicioVuelta);
        Assert.Equal(fin,    finVuelta);
    }

    // ── Otros modos de pago ──

    [Fact]
    public void ElModoMensualDaUnPeriodoPorMes()
    {
        var cfg = new ConfiguracionLaboral
        {
            SalarioMensual = 480_000m, JornadaSemanal = 48m,
            ModoPago = "Mensual", DiaPago = 30
        };

        var (inicio, fin) = cfg.PeriodoQueContiene(new DateTime(2026, 9, 10));
        Assert.Equal(new DateTime(2026, 8, 31), inicio);
        Assert.Equal(new DateTime(2026, 9, 30), fin);
    }

    [Fact]
    public void ElModoSemanalDaPeriodosDeSieteDias()
    {
        var cfg = new ConfiguracionLaboral
        {
            SalarioMensual = 480_000m, JornadaSemanal = 48m,
            ModoPago = "Semanal", DiaSemana = 5   // viernes
        };

        var (inicio, fin) = cfg.PeriodoQueContiene(new DateTime(2026, 9, 23));

        Assert.Equal(DayOfWeek.Friday, fin.DayOfWeek);
        Assert.Equal(6, (fin - inicio).TotalDays);
    }

    [Fact]
    public void LaHoraDiariaSaleDeLosDiasQueSeTrabajan()
    {
        var cfg = Quincenal();

        Assert.Equal(8.5m, cfg.HorasDiarias);          // 42,5 / 5

        // Con el divisor clavado en 6 daban 7,08 h, y con ellas cada feriado se pagaba
        // unos ₡3.300 por debajo.
        Assert.NotEqual(42.5m / 6m, cfg.HorasDiarias);
    }
}
