using System.Windows;
using System.Windows.Media;

namespace FinanzasPersonales.Services;

/// <summary>
/// Los dos temas de la aplicación y el que está aplicado.
/// <para>
/// Esta es la <b>única</b> fuente de colores. <see cref="Aplicar"/> convierte la paleta en los
/// recursos que usa el XAML (<c>{DynamicResource BgBrush}</c>…), y los ViewModels que eligen un
/// color según el dato (verde/rojo, avisos) lo leen de <see cref="Actual"/>. Un color escrito a
/// mano en un XAML o en C# no cambia con el tema: hay que añadirlo aquí con un nombre.
/// </para>
/// <para>
/// El XAML tiene que pedirlos con <c>DynamicResource</c>: un <c>StaticResource</c> se resuelve
/// una sola vez y se quedaría con el tema con el que se abrió la ventana.
/// </para>
/// </summary>
public static class Temas
{
    public const string Oscuro = "Oscuro";
    public const string Claro  = "Claro";

    public sealed record Paleta(
        string Nombre,
        string MahAppsTema,
        // Base
        string Bg, string Surface, string Card, string Border, string BorderStrong,
        string Accent, string AccentLight, string AccentPressed,
        string Text, string TextSoft, string TextMuted, string TextOnColor,
        // Significado
        string Green, string Red, string Yellow, string Orange, string Blue,
        // Fondos suaves (avisos, etiquetas)
        string AccentTint, string GreenTint, string RedTint, string YellowTint,
        // Estados de filas y navegación
        string Hover, string Selected, string SelectedStrong, string NavHover, string AltRow,
        string DangerHover, string GreenRow, string RedRow, string YellowRow, string Track,
        // Porciones del gráfico de pastel
        string[] Grafico);

    public static readonly Paleta PaletaOscura = new(
        Oscuro, "Dark.Blue",
        Bg: "#0F172A", Surface: "#172033", Card: "#1E293B", Border: "#334155", BorderStrong: "#475569",
        Accent: "#0EA5E9", AccentLight: "#38BDF8", AccentPressed: "#0284C7",
        Text: "#E2E8F0", TextSoft: "#CBD5E1", TextMuted: "#94A3B8", TextOnColor: "#0F172A",
        Green: "#4ADE80", Red: "#FB7185", Yellow: "#FCD34D", Orange: "#FB923C", Blue: "#60A5FA",
        AccentTint: "#220EA5E9", GreenTint: "#224ADE80", RedTint: "#22FB7185", YellowTint: "#22FCD34D",
        Hover: "#1E3A5F", Selected: "#0C4A6E", SelectedStrong: "#075985", NavHover: "#1A2740", AltRow: "#1A2436",
        DangerHover: "#3F1A1F", GreenRow: "#123524", RedRow: "#3F1D25", YellowRow: "#3A2F12", Track: "#263348",
        Grafico: ["#FB7185", "#FB923C", "#FACC15", "#4ADE80", "#2DD4BF", "#A78BFA"]);

    public static readonly Paleta PaletaClara = new(
        Claro, "Light.Blue",
        Bg: "#EEF2F7", Surface: "#F8FAFC", Card: "#FFFFFF", Border: "#CBD5E1", BorderStrong: "#94A3B8",
        Accent: "#0284C7", AccentLight: "#0369A1", AccentPressed: "#075985",
        Text: "#0F172A", TextSoft: "#334155", TextMuted: "#64748B", TextOnColor: "#FFFFFF",
        Green: "#16A34A", Red: "#E11D48", Yellow: "#B45309", Orange: "#EA580C", Blue: "#2563EB",
        AccentTint: "#220284C7", GreenTint: "#2216A34A", RedTint: "#22E11D48", YellowTint: "#33F59E0B",
        Hover: "#F0F9FF", Selected: "#E0F2FE", SelectedStrong: "#BAE6FD", NavHover: "#E2E8F0", AltRow: "#F1F5F9",
        DangerHover: "#FFE4E6", GreenRow: "#DCFCE7", RedRow: "#FFE4E6", YellowRow: "#FEF3C7", Track: "#E2E8F0",
        Grafico: ["#E11D48", "#EA580C", "#CA8A04", "#16A34A", "#0D9488", "#7C3AED"]);

    /// <summary>Tema aplicado. Sin WPF (pruebas) es el oscuro.</summary>
    public static Paleta Actual { get; private set; } = PaletaOscura;

    public static Paleta Por(string? nombre) => nombre == Claro ? PaletaClara : PaletaOscura;

    /// <summary>Pincel de un color "#RRGGBB" / "#AARRGGBB", congelado.</summary>
    public static SolidColorBrush Pincel(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private const string MarcaMahApps = "/MahApps.Metro;component/Styles/Themes/";
    private static ResourceDictionary? _recursos;

    /// <summary>
    /// Aplica el tema a toda la aplicación. Las ventanas abiertas cambian solas en lo que usa
    /// <c>DynamicResource</c>; lo que un ViewModel ya calculó con el tema anterior necesita que
    /// la vista se vuelva a crear (lo hace <c>MainWindow</c>).
    /// </summary>
    public static void Aplicar(string? nombre)
    {
        var p = Por(nombre);
        Actual = p;
        var app = Application.Current;
        if (app is null) return;

        var dic = new ResourceDictionary();
        void Brush(string clave, string hex) => dic[clave] = Pincel(hex);

        Brush("BgBrush", p.Bg);               Brush("SurfaceBrush", p.Surface);
        Brush("CardBrush", p.Card);           Brush("BorderBrush", p.Border);
        Brush("BorderStrongBrush", p.BorderStrong);
        Brush("AccentBrush", p.Accent);       Brush("AccentLightBrush", p.AccentLight);
        Brush("AccentPressedBrush", p.AccentPressed);
        Brush("TextBrush", p.Text);           Brush("TextSoftBrush", p.TextSoft);
        Brush("TextMutedBrush", p.TextMuted); Brush("TextOnColorBrush", p.TextOnColor);
        Brush("GreenBrush", p.Green);         Brush("RedBrush", p.Red);
        Brush("YellowBrush", p.Yellow);       Brush("OrangeBrush", p.Orange);
        Brush("BlueBrush", p.Blue);
        Brush("AccentTintBrush", p.AccentTint); Brush("GreenTintBrush", p.GreenTint);
        Brush("RedTintBrush", p.RedTint);       Brush("YellowTintBrush", p.YellowTint);
        Brush("HoverBrush", p.Hover);           Brush("SelectedBrush", p.Selected);
        Brush("SelectedStrongBrush", p.SelectedStrong);
        Brush("NavHoverBrush", p.NavHover);     Brush("AltRowBrush", p.AltRow);
        Brush("DangerHoverBrush", p.DangerHover);
        Brush("GreenRowBrush", p.GreenRow);     Brush("RedRowBrush", p.RedRow);
        Brush("YellowRowBrush", p.YellowRow);   Brush("TrackBrush", p.Track);

        // Acento de MahApps (calendario del DatePicker) → el nuestro.
        var acento = (Color)ColorConverter.ConvertFromString(p.Accent);
        dic["MahApps.Colors.AccentBase"] = acento;
        dic["MahApps.Colors.Accent"]     = acento;
        Brush("MahApps.Brushes.Accent", p.Accent);
        Brush("MahApps.Brushes.AccentBase", p.Accent);

        var mezclados = app.Resources.MergedDictionaries;
        var mahApps = mezclados.FirstOrDefault(d => d.Source?.OriginalString.Contains(MarcaMahApps) == true);
        var nuevoMahApps = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,{MarcaMahApps}{p.MahAppsTema}.xaml")
        };
        if (mahApps is null) mezclados.Add(nuevoMahApps);
        else mezclados[mezclados.IndexOf(mahApps)] = nuevoMahApps;

        // El nuestro va el último: manda sobre los acentos de MahApps.
        if (_recursos is not null) mezclados.Remove(_recursos);
        mezclados.Add(dic);
        _recursos = dic;
    }
}
