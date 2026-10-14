using FinanzasPersonales.Models;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Qué identifica a una transacción en las listas. Las notas iban primero, y las transacciones
/// automáticas llevan en las notas un texto fijo que tapaba el nombre.
/// </summary>
public class EtiquetaTransaccionTests
{
    [Fact]
    public void UnIngresoFijoSeVePorSuNombreYNoPorElTextoFijo()
    {
        var t = new Transaccion { Categoria = "Ingresos Fijos", Descripcion = "prueba", Notas = "Ingreso fijo recibido" };

        Assert.Equal("prueba", t.EtiquetaDisplay);
        Assert.Equal("Ingreso fijo recibido", t.NotasTooltip);
    }

    [Fact]
    public void UnPagoDeTarjetaDiceQueTarjetaSePago()
    {
        var t = new Transaccion { Categoria = "Tarjeta", Descripcion = "Pago tarjeta Bac Economía", Notas = "Abono registrado manualmente" };

        Assert.Equal("Pago tarjeta Bac Economía", t.EtiquetaDisplay);
    }

    [Fact]
    public void SinDescripcionSeVenLasNotasYSinNadaLaCategoria()
    {
        Assert.Equal("Sólo notas", new Transaccion { Categoria = "Otros", Notas = "Sólo notas" }.EtiquetaDisplay);
        Assert.Equal("Otros",      new Transaccion { Categoria = "Otros" }.EtiquetaDisplay);
        Assert.Null(new Transaccion { Categoria = "Otros" }.NotasTooltip);
    }
}
