using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;

namespace FinanzasPersonales.ViewModels;

public class AdminViewModel : BaseViewModel
{
    private readonly AppDatabase _db;

    // ── Usuarios ─────────────────────────────────────────────────────────────
    public ObservableCollection<Usuario> Usuarios { get; } = [];

    private string _errorUsuarios = string.Empty;
    public string ErrorUsuarios
    {
        get => _errorUsuarios;
        set { _errorUsuarios = value; OnPropertyChanged(); OnPropertyChanged(nameof(ErrorUsuariosVisibility)); }
    }
    public Visibility ErrorUsuariosVisibility =>
        string.IsNullOrEmpty(_errorUsuarios) ? Visibility.Collapsed : Visibility.Visible;

    // ── Configuración global ──────────────────────────────────────────────────
    private string _moneda      = "₡";
    private string _maxUsuarios = "20";
    private string _errorConfig = string.Empty;
    private string _okConfig    = string.Empty;

    public string Moneda
    {
        get => _moneda;
        set { _moneda = value; OnPropertyChanged(); }
    }

    public string MaxUsuarios
    {
        get => _maxUsuarios;
        set { _maxUsuarios = value; OnPropertyChanged(); }
    }

    public string ErrorConfig
    {
        get => _errorConfig;
        set { _errorConfig = value; OnPropertyChanged(); OnPropertyChanged(nameof(ErrorConfigVisibility)); }
    }
    public Visibility ErrorConfigVisibility =>
        string.IsNullOrEmpty(_errorConfig) ? Visibility.Collapsed : Visibility.Visible;

    public string OkConfig
    {
        get => _okConfig;
        set { _okConfig = value; OnPropertyChanged(); OnPropertyChanged(nameof(OkConfigVisibility)); }
    }
    public Visibility OkConfigVisibility =>
        string.IsNullOrEmpty(_okConfig) ? Visibility.Collapsed : Visibility.Visible;

    // ── Diagnóstico de integridad ─────────────────────────────────────────────
    public ObservableCollection<ProblemaIntegridad> Problemas { get; } = [];

    private bool   _diagnosticoEjecutado;
    private string _diagnosticoResumen = string.Empty;

    public string DiagnosticoResumen
    {
        get => _diagnosticoResumen;
        private set { _diagnosticoResumen = value; OnPropertyChanged(); }
    }

    public string DiagnosticoColor =>
        Problemas.Any(p => p.Severidad == SeveridadProblema.Critico) ? Temas.Actual.Red
        : Problemas.Count > 0                                        ? Temas.Actual.Orange
                                                                     : Temas.Actual.Green;

    public Visibility DiagnosticoResumenVisibility =>
        _diagnosticoEjecutado ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DiagnosticoListaVisibility =>
        Problemas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    // ── Copias de seguridad ───────────────────────────────────────────────────
    private string _copiasResumen = string.Empty;
    public string CopiasResumen
    {
        get => _copiasResumen;
        private set { _copiasResumen = value; OnPropertyChanged(); }
    }

    private string _copiasMensaje = string.Empty;
    private bool   _copiasOk;
    public string CopiasMensaje
    {
        get => _copiasMensaje;
        private set
        {
            _copiasMensaje = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CopiasMensajeColor));
            OnPropertyChanged(nameof(CopiasMensajeVisibility));
        }
    }
    public string CopiasMensajeColor => _copiasOk ? Temas.Actual.Green : Temas.Actual.Red;
    public Visibility CopiasMensajeVisibility =>
        string.IsNullOrEmpty(_copiasMensaje) ? Visibility.Collapsed : Visibility.Visible;

    private void MensajeCopias(string texto, bool ok)
    {
        _copiasOk = ok;
        CopiasMensaje = texto;
    }

    // ── Comandos ──────────────────────────────────────────────────────────────
    public ICommand GuardarCopiaCommand  { get; }
    public ICommand AbrirCarpetaCopiasCommand { get; }
    public ICommand RestaurarCopiaCommand { get; }
    public ICommand ToggleActivoCommand  { get; }
    public ICommand EliminarUsuarioCommand { get; }
    public ICommand GuardarConfigCommand { get; }
    public ICommand EjecutarDiagnosticoCommand { get; }

    public AdminViewModel(AppDatabase db)
    {
        _db = db;
        ToggleActivoCommand    = new RelayCommand<Usuario>(ToggleActivo);
        EliminarUsuarioCommand = new RelayCommand<Usuario>(EliminarUsuario);
        GuardarConfigCommand   = new RelayCommand(GuardarConfig);
        EjecutarDiagnosticoCommand = new RelayCommand(EjecutarDiagnostico);
        GuardarCopiaCommand        = new RelayCommand(GuardarCopia);
        AbrirCarpetaCopiasCommand  = new RelayCommand(AbrirCarpetaCopias);
        RestaurarCopiaCommand      = new RelayCommand(RestaurarCopia);
        Cargar();
    }

    private void CargarResumenCopias()
    {
        var copias = CopiasDeSeguridad.Automaticas(_db);
        var ultima = CopiasDeSeguridad.UltimaAutomatica(_db);
        CopiasResumen = ultima is null
            ? "Todavía no hay copias automáticas."
            : $"Última copia automática: {ultima:dd/MM/yyyy HH:mm} · {copias.Count} guardada{(copias.Count == 1 ? "" : "s")}";
    }

    private void GuardarCopia()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Guardar copia de seguridad",
            FileName   = $"DMT Finance {DateTime.Now:yyyy-MM-dd}.db",
            DefaultExt = ".db",
            Filter     = "Copia de DMT Finance (*.db)|*.db"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            CopiasDeSeguridad.Crear(_db, dlg.FileName);
            MensajeCopias($"Copia guardada en {dlg.FileName}", ok: true);
        }
        catch (Exception ex)
        {
            MensajeCopias($"No se pudo guardar la copia: {ex.Message}", ok: false);
        }
    }

    private void AbrirCarpetaCopias()
    {
        var carpeta = CopiasDeSeguridad.Carpeta(_db);
        System.IO.Directory.CreateDirectory(carpeta);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(carpeta) { UseShellExecute = true });
    }

    private void RestaurarCopia()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title            = "Elegir la copia que se va a restaurar",
            InitialDirectory = CopiasDeSeguridad.Carpeta(_db),
            Filter           = "Copia de DMT Finance (*.db)|*.db"
        };
        if (dlg.ShowDialog() != true) return;

        var motivo = CopiasDeSeguridad.MotivoNoRestaurable(dlg.FileName);
        if (motivo is not null)
        {
            MensajeCopias(motivo, ok: false);
            return;
        }

        var res = MessageBox.Show(
            $"Se reemplazarán TODOS los datos actuales, de todos los usuarios, por los de esta copia:\n\n" +
            $"{System.IO.Path.GetFileName(dlg.FileName)}\n\n" +
            "Antes se guardará una copia de lo que hay ahora, por si necesitas volver atrás.\n" +
            "Al terminar, la aplicación se cerrará y tendrás que abrirla de nuevo.\n\n¿Continuar?",
            "Restaurar copia de seguridad", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            var seguridad = CopiasDeSeguridad.Restaurar(_db, dlg.FileName, DateTime.Now);
            MessageBox.Show(
                "Copia restaurada. La aplicación se cerrará ahora; ábrela de nuevo.\n\n" +
                $"Lo que había antes quedó guardado en:\n{seguridad}",
                "Restaurar copia de seguridad", MessageBoxButton.OK, MessageBoxImage.Information);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MensajeCopias($"No se pudo restaurar: {ex.Message}", ok: false);
        }
    }

    private void EjecutarDiagnostico()
    {
        Problemas.Clear();
        foreach (var p in _db.VerificarIntegridad())
            Problemas.Add(p);

        _diagnosticoEjecutado = true;

        var criticos = Problemas.Count(p => p.Severidad == SeveridadProblema.Critico);
        DiagnosticoResumen = Problemas.Count == 0
            ? "✓ Sin discrepancias. Las distintas fuentes de saldo cuadran entre sí."
            : $"Se encontraron {Problemas.Count} discrepancia{(Problemas.Count == 1 ? "" : "s")}" +
              (criticos > 0 ? $", {criticos} de ellas críticas." : ".");

        OnPropertyChanged(nameof(DiagnosticoColor));
        OnPropertyChanged(nameof(DiagnosticoResumenVisibility));
        OnPropertyChanged(nameof(DiagnosticoListaVisibility));
    }

    public void Actualizar() => Cargar();

    private void Cargar()
    {
        Usuarios.Clear();
        foreach (var u in _db.ObtenerUsuarios())
            Usuarios.Add(u);

        Moneda      = _db.ObtenerConfig("moneda", "₡");
        MaxUsuarios = _db.ObtenerConfig("max_usuarios", "20");
        CargarResumenCopias();
    }

    private void ToggleActivo(Usuario? u)
    {
        if (u is null) return;
        if (u.Id == SessionService.UsuarioId)
        {
            ErrorUsuarios = "No puedes desactivar tu propia cuenta.";
            return;
        }
        ErrorUsuarios = string.Empty;
        _db.ToggleUsuarioActivo(u.Id, !u.Activo);
        Cargar();
    }

    private void EliminarUsuario(Usuario? u)
    {
        if (u is null) return;
        if (u.Id == SessionService.UsuarioId)
        {
            ErrorUsuarios = "No puedes eliminar tu propia cuenta.";
            return;
        }
        var res = System.Windows.MessageBox.Show(
            $"¿Eliminar el usuario \"{u.NombreUsuario}\" y todos sus datos? Esta acción no se puede deshacer.",
            "Confirmar eliminación",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (res != System.Windows.MessageBoxResult.Yes) return;
        ErrorUsuarios = string.Empty;
        _db.EliminarUsuario(u.Id);
        Cargar();
    }

    private void GuardarConfig()
    {
        ErrorConfig = string.Empty;
        OkConfig    = string.Empty;

        if (string.IsNullOrWhiteSpace(_moneda))
        {
            ErrorConfig = "El símbolo de moneda no puede estar vacío.";
            return;
        }

        if (!int.TryParse(_maxUsuarios, out var max) || max < 1 || max > 999)
        {
            ErrorConfig = "El límite de usuarios debe ser entre 1 y 999.";
            return;
        }

        _db.GuardarConfig("moneda",       _moneda.Trim());
        _db.GuardarConfig("max_usuarios", max.ToString());

        // Actualizar moneda en tiempo de ejecución
        AppSettings.Moneda = _moneda.Trim();

        OkConfig = "Configuración guardada. La moneda se aplicará al reiniciar la aplicación.";
    }
}
