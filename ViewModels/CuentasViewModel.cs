using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;

namespace FinanzasPersonales.ViewModels;

public class CuentasViewModel : BaseViewModel
{
    private readonly AppDatabase _db;
    private readonly int         _uid;

    // ── Selección / edición inline ────────────────────────────────────────────
    private Cuenta? _seleccionada;
    private string  _editNombre = string.Empty;
    private string  _editTipo   = "Cuenta corriente";
    private string  _editBanco  = string.Empty;
    private bool    _editActiva = true;
    private string  _error      = string.Empty;
    private string  _mensajeOk  = string.Empty;

    public Cuenta? Seleccionada
    {
        get => _seleccionada;
        set
        {
            _seleccionada = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HaySeleccion));
            if (value is not null)
            {
                EditNombre = value.Nombre;
                EditTipo   = value.Tipo;
                EditBanco  = value.Banco;
                EditActiva = value.Activa;
            }
            MensajeError = string.Empty;
            MensajeOk    = string.Empty;
        }
    }

    public bool HaySeleccion => _seleccionada is not null;

    public string EditNombre
    {
        get => _editNombre;
        set { _editNombre = value; OnPropertyChanged(); }
    }
    public string EditTipo
    {
        get => _editTipo;
        set { _editTipo = value; OnPropertyChanged(); }
    }
    public string EditBanco
    {
        get => _editBanco;
        set { _editBanco = value; OnPropertyChanged(); }
    }
    public bool EditActiva
    {
        get => _editActiva;
        set { _editActiva = value; OnPropertyChanged(); }
    }

    public string MensajeError
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeErrorVisibility)); }
    }
    public Visibility MensajeErrorVisibility =>
        string.IsNullOrEmpty(_error) ? Visibility.Collapsed : Visibility.Visible;

    public string MensajeOk
    {
        get => _mensajeOk;
        set { _mensajeOk = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeOkVisibility)); }
    }
    public Visibility MensajeOkVisibility =>
        string.IsNullOrEmpty(_mensajeOk) ? Visibility.Collapsed : Visibility.Visible;

    // ── Nueva cuenta ──────────────────────────────────────────────────────────
    private string _nuevaNombre = string.Empty;
    private string _nuevaTipo   = "Cuenta corriente";
    private string _nuevaBanco  = string.Empty;
    private string _nuevaError  = string.Empty;

    public string NuevaNombre
    {
        get => _nuevaNombre;
        set { _nuevaNombre = value; OnPropertyChanged(); }
    }
    public string NuevoTipo
    {
        get => _nuevaTipo;
        set { _nuevaTipo = value; OnPropertyChanged(); }
    }
    public string NuevoBanco
    {
        get => _nuevaBanco;
        set { _nuevaBanco = value; OnPropertyChanged(); }
    }
    public string NuevaError
    {
        get => _nuevaError;
        set { _nuevaError = value; OnPropertyChanged(); OnPropertyChanged(nameof(NuevaErrorVisibility)); }
    }
    public Visibility NuevaErrorVisibility =>
        string.IsNullOrEmpty(_nuevaError) ? Visibility.Collapsed : Visibility.Visible;

    // ── Colecciones ───────────────────────────────────────────────────────────
    public ObservableCollection<Cuenta> Cuentas { get; } = [];

    public IReadOnlyList<string> Tipos { get; } =
        ["Cuenta corriente", "Cuenta de ahorros", "Cuenta en dólares"];

    // ── Comandos ──────────────────────────────────────────────────────────────
    public ICommand AgregarCommand       { get; }
    public ICommand GuardarCambiosCommand { get; }
    public ICommand EliminarCommand      { get; }

    public CuentasViewModel(AppDatabase db, int usuarioId)
    {
        _db                   = db;
        _uid                  = usuarioId;
        AgregarCommand        = new RelayCommand(Agregar);
        GuardarCambiosCommand = new RelayCommand(GuardarCambios);
        EliminarCommand       = new RelayCommand(Eliminar);
        Cargar();
    }

    public void Actualizar() => Cargar();

    private void Cargar()
    {
        var prevId = _seleccionada?.Id;
        Cuentas.Clear();
        foreach (var c in _db.ObtenerCuentas(_uid))
            Cuentas.Add(c);

        if (prevId.HasValue)
            Seleccionada = Cuentas.FirstOrDefault(c => c.Id == prevId);
    }

    private void Agregar()
    {
        NuevaError = string.Empty;

        if (string.IsNullOrWhiteSpace(_nuevaNombre))
        {
            NuevaError = "El nombre es obligatorio.";
            return;
        }

        if (Cuentas.Any(c => c.Nombre.Equals(_nuevaNombre.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            NuevaError = "Ya existe una cuenta con ese nombre.";
            return;
        }

        try
        {
            _db.InsertarCuenta(new Cuenta
            {
                Nombre = _nuevaNombre.Trim(),
                Tipo   = _nuevaTipo,
                Banco  = _nuevaBanco.Trim(),
                Activa = true
            }, _uid);

            NuevaNombre = string.Empty;
            NuevoTipo   = "Cuenta corriente";
            NuevoBanco  = string.Empty;
            Cargar();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            NuevaError = "Ya existe una cuenta con ese nombre.";
        }
    }

    private void GuardarCambios()
    {
        if (_seleccionada is null) return;
        MensajeError = string.Empty;
        MensajeOk    = string.Empty;

        if (string.IsNullOrWhiteSpace(_editNombre))
        {
            MensajeError = "El nombre no puede estar vacío.";
            return;
        }

        var duplicado = Cuentas.FirstOrDefault(c =>
            c.Id != _seleccionada.Id &&
            c.Nombre.Equals(_editNombre.Trim(), StringComparison.OrdinalIgnoreCase));

        if (duplicado is not null)
        {
            MensajeError = "Ya existe otra cuenta con ese nombre.";
            return;
        }

        try
        {
            _seleccionada.Nombre = _editNombre.Trim();
            _seleccionada.Tipo   = _editTipo;
            _seleccionada.Banco  = _editBanco.Trim();
            _seleccionada.Activa = _editActiva;
            _db.ActualizarCuenta(_seleccionada);
            MensajeOk = "Cambios guardados.";
            Cargar();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            MensajeError = "Ya existe otra cuenta con ese nombre.";
        }
    }

    private void Eliminar()
    {
        if (_seleccionada is null) return;

        var res = System.Windows.MessageBox.Show(
            $"¿Eliminar la cuenta \"{_seleccionada.Nombre}\"?",
            "Confirmar eliminación",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (res != System.Windows.MessageBoxResult.Yes) return;

        _db.EliminarCuenta(_seleccionada.Id);
        Seleccionada = null;
        Cargar();
    }
}
