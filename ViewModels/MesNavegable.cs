using System.Globalization;
using System.Windows;

namespace FinanzasPersonales.ViewModels;

/// <summary>
/// El mes que muestra una pantalla de fijos, con anterior / siguiente / volver al actual.
/// <para>
/// Si se está mirando el mes actual, se sigue al cambiar la fecha: las vistas se cachean, y sin
/// esto una pantalla abierta el 31 seguía mostrando ese mes el día 1. Si se está mirando otro,
/// recargar no lo mueve. Es el mismo criterio que la navegación de períodos de Control Laboral.
/// </para>
/// </summary>
public sealed class MesNavegable
{
    private static readonly CultureInfo Cr = CultureInfo.GetCultureInfo("es-CR");

    private readonly Func<DateTime> _hoy;
    private bool _siguiendoAlActual = true;

    public MesNavegable(Func<DateTime>? hoy = null) => _hoy = hoy ?? (() => DateTime.Today);

    /// <summary>Día 1 del mes que se mira.</summary>
    public DateTime Inicio { get; private set; }

    public int  Anio => Inicio.Year;
    public int  Mes  => Inicio.Month;

    private DateTime MesDeHoy => new(_hoy().Year, _hoy().Month, 1);

    public bool EsActual => Inicio == MesDeHoy;

    public string Texto => EsActual
        ? $"Mes actual — {Inicio.ToString("MMMM yyyy", Cr)}"
        : Cr.TextInfo.ToTitleCase(Inicio.ToString("MMMM yyyy", Cr));

    public Visibility VolverVisibility => EsActual ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Al cargar: si se seguía el mes actual (o no se había elegido ninguno), ir a él.</summary>
    public void SeguirAlActual()
    {
        if (_siguiendoAlActual || Inicio == default) Inicio = MesDeHoy;
    }

    public void Anterior()  => Ir(Inicio.AddMonths(-1));
    public void Siguiente() => Ir(Inicio.AddMonths(1));
    public void Actual()    => Ir(MesDeHoy);

    private void Ir(DateTime mes)
    {
        Inicio = mes;
        _siguiendoAlActual = EsActual;
    }
}
