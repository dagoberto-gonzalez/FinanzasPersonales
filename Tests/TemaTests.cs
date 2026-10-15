using FinanzasPersonales.Services;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// El tema (oscuro / claro): se guarda por usuario, y el último elegido queda como el del equipo
/// para la ventana de inicio de sesión.
/// </summary>
public class TemaTests
{
    [Fact]
    public void PorDefectoEsElOscuro()
    {
        using var t = new BaseDePrueba();
        Assert.Equal(Temas.Oscuro, t.Db.ObtenerTema(t.UsuarioId));
        Assert.Equal(Temas.Oscuro, t.Db.ObtenerTemaDelEquipo());
    }

    [Fact]
    public void CadaUsuarioTieneElSuyo_YElEquipoRecuerdaElUltimo()
    {
        using var t = new BaseDePrueba();
        var otro = t.Db.InsertarUsuario("otro" + Guid.NewGuid().ToString("N")[..6], "clave123").Id;

        t.Db.GuardarTema(t.UsuarioId, Temas.Claro);

        Assert.Equal(Temas.Claro,  t.Db.ObtenerTema(t.UsuarioId));
        Assert.Equal(Temas.Oscuro, t.Db.ObtenerTema(otro));
        Assert.Equal(Temas.Claro,  t.Db.ObtenerTemaDelEquipo());
    }

    [Fact]
    public void LasDosPaletasTienenLosMismosColoresDeGrafico()
    {
        // El gráfico reparte las categorías por posición: con distinta cantidad, una misma
        // categoría cambiaría de color al cambiar de tema.
        Assert.Equal(Temas.PaletaOscura.Grafico.Length, Temas.PaletaClara.Grafico.Length);
    }
}
