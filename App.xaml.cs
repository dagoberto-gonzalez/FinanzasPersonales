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
        _db = new AppDatabase();
        MostrarLogin();
    }

    private void MostrarLogin()
    {
        var loginWin = new LoginWindow(_db!);
        if (loginWin.ShowDialog() == true)
        {
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
