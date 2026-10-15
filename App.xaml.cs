using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using FinanzasPersonales.Data;
using FinanzasPersonales.Services;
using FinanzasPersonales.Windows;

namespace FinanzasPersonales;

public partial class App : Application
{
    private AppDatabase? _db;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Formato de montos: ₡75.000 (punto como separador de miles, estilo español)
        var cultura = new CultureInfo("es-ES");
        CultureInfo.DefaultThreadCurrentCulture   = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        // Aplicar cultura a bindings XAML con StringFormat
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        // La versión de desarrollo usa otra base (ver AppDatabase.EsDesarrollo): que se note.
        if (AppDatabase.EsDesarrollo)
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) =>
                {
                    var w = (Window)s;
                    if (!w.Title.EndsWith(" · DESARROLLO")) w.Title += " · DESARROLLO";
                }));

        _db = new AppDatabase();

        // Copia del día, antes de que nadie toque nada. Si falla (disco lleno, permisos) la
        // aplicación sigue: el Panel Admin muestra la fecha de la última copia.
        try { CopiasDeSeguridad.CopiaAutomatica(_db, DateTime.Now); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Copia automática: {ex.Message}"); }
        // Antes de saber quién entra: el último tema usado en este equipo.
        Temas.Aplicar(_db.ObtenerTemaDelEquipo());
        MostrarLogin();
    }

    private void MostrarLogin()
    {
        var loginWin = new LoginWindow(_db!);
        if (loginWin.ShowDialog() == true)
        {
            Temas.Aplicar(_db!.ObtenerTema(SessionService.UsuarioId));
            var mainWin = new MainWindow(_db!);
            mainWin.WindowState = WindowState.Maximized;
            mainWin.Closed += MainWin_Closed;
            mainWin.Show();
        }
        else
        {
            Shutdown();
        }
    }

    private void MainWin_Closed(object? sender, EventArgs e)
    {
        // Si la sesión ya no está activa es un logout → volver al login
        if (!SessionService.EstaAutenticado)
            MostrarLogin();
        else
            Shutdown();
    }
}
