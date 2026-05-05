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

    // ── Comandos ──────────────────────────────────────────────────────────────
    public ICommand ToggleActivoCommand  { get; }
    public ICommand EliminarUsuarioCommand { get; }
    public ICommand GuardarConfigCommand { get; }

    public AdminViewModel(AppDatabase db)
    {
        _db = db;
        ToggleActivoCommand    = new RelayCommand<Usuario>(ToggleActivo);
        EliminarUsuarioCommand = new RelayCommand<Usuario>(EliminarUsuario);
        GuardarConfigCommand   = new RelayCommand(GuardarConfig);
        Cargar();
    }

    public void Actualizar() => Cargar();

    private void Cargar()
    {
        Usuarios.Clear();
        foreach (var u in _db.ObtenerUsuarios())
            Usuarios.Add(u);

        Moneda      = _db.ObtenerConfig("moneda", "₡");
        MaxUsuarios = _db.ObtenerConfig("max_usuarios", "20");
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
