using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;

namespace FinanzasPersonales.ViewModels;

public class RegistroViewModel : BaseViewModel
{
    private readonly AppDatabase _db;

    private string _nombreUsuario = string.Empty;
    private string _password      = string.Empty;
    private string _confirmar     = string.Empty;
    private string _error         = string.Empty;

    public string NombreUsuario
    {
        get => _nombreUsuario;
        set { _nombreUsuario = value; OnPropertyChanged(); }
    }

    // Passwords inyectados desde code-behind
    public string Password
    {
        get => _password;
        set { _password = value; OnPropertyChanged(); }
    }

    public string Confirmar
    {
        get => _confirmar;
        set { _confirmar = value; OnPropertyChanged(); }
    }

    public string MensajeError
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeErrorVisibility)); }
    }

    public Visibility MensajeErrorVisibility =>
        string.IsNullOrEmpty(_error) ? Visibility.Collapsed : Visibility.Visible;

    public ICommand RegistrarCommand { get; }

    public event Action? RegistroExitoso;

    public RegistroViewModel(AppDatabase db)
    {
        _db              = db;
        RegistrarCommand = new RelayCommand(Registrar);
    }

    private void Registrar()
    {
        MensajeError = string.Empty;

        if (string.IsNullOrWhiteSpace(_nombreUsuario))
        {
            MensajeError = "El nombre de usuario es requerido.";
            return;
        }

        if (_nombreUsuario.Trim().Length < 3)
        {
            MensajeError = "El nombre de usuario debe tener al menos 3 caracteres.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_password) || _password.Length < 6)
        {
            MensajeError = "La contraseña debe tener al menos 6 caracteres.";
            return;
        }

        if (_password != _confirmar)
        {
            MensajeError = "Las contraseñas no coinciden.";
            return;
        }

        if (_db.ExisteUsuario(_nombreUsuario))
        {
            MensajeError = "Ese nombre de usuario ya está en uso.";
            return;
        }

        int totalUsuarios = _db.ContarUsuarios();
        int maxUsuarios   = _db.ObtenerMaxUsuarios();

        if (totalUsuarios >= maxUsuarios)
        {
            MensajeError = $"Se alcanzó el límite máximo de {maxUsuarios} usuarios.";
            return;
        }

        // El primer usuario registrado es Admin
        string rol = totalUsuarios == 0 ? "Admin" : "Normal";
        _db.InsertarUsuario(_nombreUsuario.Trim(), _password, rol);

        RegistroExitoso?.Invoke();
    }
}
