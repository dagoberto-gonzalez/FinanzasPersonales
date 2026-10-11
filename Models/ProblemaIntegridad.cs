namespace FinanzasPersonales.Models;

public enum SeveridadProblema
{
    /// <summary>El dinero mostrado es incorrecto, o la operación revienta la aplicación.</summary>
    Critico,
    /// <summary>Datos inconsistentes que aún no producen un número equivocado.</summary>
    Advertencia,
    /// <summary>Observación útil, sin impacto directo.</summary>
    Informativo
}

/// <summary>
/// Una discrepancia detectada por <c>AppDatabase.VerificarIntegridad()</c>.
/// El objetivo del sistema es que esa lista venga siempre vacía.
/// </summary>
public class ProblemaIntegridad
{
    public SeveridadProblema Severidad   { get; set; }
    public string            Area        { get; set; } = string.Empty;
    public string            Descripcion { get; set; } = string.Empty;
    public string            Detalle     { get; set; } = string.Empty;
    public int               Cantidad    { get; set; } = 1;

    public string SeveridadTexto => Severidad switch
    {
        SeveridadProblema.Critico     => "CRÍTICO",
        SeveridadProblema.Advertencia => "ADVERTENCIA",
        _                             => "INFO"
    };

    // Paleta del tema oscuro (ver App.xaml)
    public string SeveridadColor => Severidad switch
    {
        SeveridadProblema.Critico     => "#F38BA8",
        SeveridadProblema.Advertencia => "#FAB387",
        _                             => "#89B4FA"
    };
}
