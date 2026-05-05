using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Services;

namespace FinanzasPersonales.ViewModels;

public class CambiarPasswordViewModel : BaseViewModel
{
    private readonly AppDatabase _db;

    public string PasswordActual  { get; set; } = string.Empty;
    public string PasswordNuevo   { get; set; } = string.Empty;
    public string PasswordConfirm { get; set; } = string.Empty;

    private string _mensaje = string.Empty;
    private bool   _esExito;

    public string MensajeError
    {
        get => _mensaje;
        set { _mensaje = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeVisibility)); OnPropertyChanged(nameof(MensajeColor)); }
    }

    public bool EsExito
    {
        get => _esExito;
        set { _esExito = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeColor)); }
    }

    public Visibility MensajeVisibility =>
        string.IsNullOrEmpty(_mensaje) ? Visibility.Collapsed : Visibility.Visible;

    public string MensajeColor => _esExito ? "#A6E3A1" : "#F38BA8";

    public ICommand CambiarCommand { get; }

    public CambiarPasswordViewModel(AppDatabase db)
    {
        _db            = db;
        CambiarCommand = new RelayCommand(Cambiar);
    }

    private void Cambiar()
    {
        EsExito      = false;
        MensajeError = string.Empty;

        if (string.IsNullOrWhiteSpace(PasswordActual))
        {
            MensajeError = "Ingresa la contraseña actual.";
            return;
        }

        // Verificar contraseña actual
        var usuario = _db.ValidarCredenciales(SessionService.NombreUsuario, PasswordActual);
        if (usuario is null)
        {
            MensajeError = "La contraseña actual es incorrecta.";
            return;
        }

        if (string.IsNullOrWhiteSpace(PasswordNuevo) || PasswordNuevo.Length < 6)
        {
            MensajeError = "La nueva contraseña debe tener al menos 6 caracteres.";
            return;
        }

        if (PasswordNuevo != PasswordConfirm)
        {
            MensajeError = "Las contraseñas nuevas no coinciden.";
            return;
        }

        _db.CambiarPassword(SessionService.UsuarioId, PasswordNuevo);
        EsExito      = true;
        MensajeError = "Contraseña cambiada exitosamente.";

        PasswordActual  = string.Empty;
        PasswordNuevo   = string.Empty;
        PasswordConfirm = string.Empty;
    }
}
