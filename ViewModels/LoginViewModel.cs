using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Services;

namespace FinanzasPersonales.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly AppDatabase _db;

    private string _usuario  = string.Empty;
    private string _password = string.Empty;
    private string _error    = string.Empty;

    public string Usuario
    {
        get => _usuario;
        set { _usuario = value; OnPropertyChanged(); }
    }

    public string Password
    {
        get => _password;
        set { _password = value; OnPropertyChanged(); }
    }

    public string MensajeError
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeErrorVisibility)); }
    }

    public System.Windows.Visibility MensajeErrorVisibility =>
        string.IsNullOrEmpty(_error)
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;

    public ICommand IniciarSesionCommand { get; }

    public event Action? LoginExitoso;

    public LoginViewModel(AppDatabase db)
    {
        _db = db;
        IniciarSesionCommand = new RelayCommand(Autenticar);
    }

    private void Autenticar()
    {
        if (string.IsNullOrWhiteSpace(_usuario) || string.IsNullOrWhiteSpace(_password))
        {
            MensajeError = "Ingresa usuario y contraseña.";
            return;
        }

        var usuario = _db.ValidarCredenciales(_usuario, _password);

        if (usuario is null)
        {
            MensajeError = "Usuario o contraseña incorrectos.";
            return;
        }

        if (!usuario.Activo)
        {
            MensajeError = "Tu cuenta está desactivada. Contacta al administrador.";
            return;
        }

        SessionService.IniciarSesion(usuario.Id, usuario.NombreUsuario, usuario.Rol);

        // Cargar configuración global en AppSettings
        AppSettings.Moneda = _db.ObtenerConfig("moneda", "₡");

        LoginExitoso?.Invoke();
    }
}
