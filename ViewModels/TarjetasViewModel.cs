using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;

namespace FinanzasPersonales.ViewModels;

public class TarjetasViewModel : BaseViewModel
{
    private readonly AppDatabase _db;
    private readonly int         _uid;

    // ── Edición inline ────────────────────────────────────────────────────────
    private TarjetaCredito? _seleccionada;
    private string _editNombre  = string.Empty;
    private string _editLimite  = string.Empty;
    private string _editCierre  = string.Empty;
    private string _editPago    = string.Empty;
    private string _error       = string.Empty;
    private string _mensajeOk   = string.Empty;

    public TarjetaCredito? Seleccionada
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
                EditLimite = value.LimiteCredito.ToString("N0");
                EditCierre = value.DiaCierre.ToString();
                EditPago   = value.DiaPago.ToString();
                AbonoMonto = string.Empty;
                CargarMovimientos(value.Id);
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
    public string EditLimite
    {
        get => _editLimite;
        set { _editLimite = value; OnPropertyChanged(); }
    }
    public string EditCierre
    {
        get => _editCierre;
        set { _editCierre = value; OnPropertyChanged(); }
    }
    public string EditPago
    {
        get => _editPago;
        set { _editPago = value; OnPropertyChanged(); }
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

    // ── Nueva tarjeta ─────────────────────────────────────────────────────────
    private string _nuevaNombre  = string.Empty;
    private string _nuevaLimite  = string.Empty;
    private string _nuevaCierre  = "15";
    private string _nuevaPago    = "5";
    private string _nuevaError   = string.Empty;

    public string NuevaNombre
    {
        get => _nuevaNombre;
        set { _nuevaNombre = value; OnPropertyChanged(); }
    }
    public string NuevaLimite
    {
        get => _nuevaLimite;
        set { _nuevaLimite = value; OnPropertyChanged(); }
    }
    public string NuevaCierre
    {
        get => _nuevaCierre;
        set { _nuevaCierre = value; OnPropertyChanged(); }
    }
    public string NuevaPago
    {
        get => _nuevaPago;
        set { _nuevaPago = value; OnPropertyChanged(); }
    }
    public string NuevaError
    {
        get => _nuevaError;
        set { _nuevaError = value; OnPropertyChanged(); OnPropertyChanged(nameof(NuevaErrorVisibility)); }
    }
    public Visibility NuevaErrorVisibility =>
        string.IsNullOrEmpty(_nuevaError) ? Visibility.Collapsed : Visibility.Visible;

    // ── Abono ─────────────────────────────────────────────────────────────────
    private string  _abonoMonto = string.Empty;
    private Cuenta? _cuentaAbono;

    public string AbonoMonto
    {
        get => _abonoMonto;
        set { _abonoMonto = value; OnPropertyChanged(); }
    }

    /// <summary>Cuenta de la que sale el pago. Si es null, el abono se registra como efectivo.</summary>
    public Cuenta? CuentaAbono
    {
        get => _cuentaAbono;
        set { _cuentaAbono = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Cuenta> CuentasDisponibles { get; } = [];

    // ── Colecciones ───────────────────────────────────────────────────────────
    public ObservableCollection<TarjetaCredito> Tarjetas    { get; } = [];
    public ObservableCollection<Transaccion>    Movimientos { get; } = [];

    // ── Comandos ──────────────────────────────────────────────────────────────
    public ICommand GuardarCambiosCommand { get; }
    public ICommand AgregarTarjetaCommand { get; }
    public ICommand EliminarTarjetaCommand { get; }
    public ICommand AbonarCommand          { get; }

    public TarjetasViewModel(AppDatabase db, int usuarioId)
    {
        _db                   = db;
        _uid                  = usuarioId;
        GuardarCambiosCommand  = new RelayCommand(GuardarCambios);
        AgregarTarjetaCommand  = new RelayCommand(AgregarTarjeta);
        EliminarTarjetaCommand = new RelayCommand<TarjetaCredito>(EliminarTarjeta);
        AbonarCommand          = new RelayCommand(Abonar);
        Cargar();
    }

    public void Actualizar() => Cargar();

    private void Cargar()
    {
        var prevId = _seleccionada?.Id;
        Tarjetas.Clear();
        var lista = _db.ObtenerTarjetas(_uid);
        foreach (var t in lista)
            Tarjetas.Add(t);

        var prevCuentaId = _cuentaAbono?.Id;
        CuentasDisponibles.Clear();
        foreach (var c in _db.ObtenerCuentas(_uid).Where(c => c.Activa))
            CuentasDisponibles.Add(c);
        CuentaAbono = prevCuentaId.HasValue
            ? CuentasDisponibles.FirstOrDefault(c => c.Id == prevCuentaId)
            : CuentasDisponibles.FirstOrDefault();

        if (prevId.HasValue)
            Seleccionada = Tarjetas.FirstOrDefault(t => t.Id == prevId);
    }

    private void CargarMovimientos(int tarjetaId)
    {
        Movimientos.Clear();
        var lista = _db.ObtenerTransaccionesPorTarjeta(tarjetaId);
        foreach (var m in lista)
            Movimientos.Add(m);
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

        // El campo llega pre-rellenado con ToString("N0"), o sea con separadores de miles.
        if (!Dinero.TryParsePositivo(_editLimite, out var limite))
        {
            MensajeError = "Límite de crédito inválido.";
            return;
        }

        if (!int.TryParse(_editCierre, out var cierre) || cierre < 1 || cierre > 31)
        {
            MensajeError = "Día de cierre debe ser entre 1 y 31.";
            return;
        }

        if (!int.TryParse(_editPago, out var pago) || pago < 1 || pago > 31)
        {
            MensajeError = "Día de pago debe ser entre 1 y 31.";
            return;
        }

        _seleccionada.Nombre        = _editNombre.Trim();
        _seleccionada.LimiteCredito = limite;
        _seleccionada.DiaCierre     = cierre;
        _seleccionada.DiaPago       = pago;
        _db.ActualizarTarjeta(_seleccionada);
        MensajeOk = "Cambios guardados correctamente.";
        Cargar();
    }

    private void AgregarTarjeta()
    {
        NuevaError = string.Empty;

        if (string.IsNullOrWhiteSpace(_nuevaNombre))
        {
            NuevaError = "El nombre es requerido.";
            return;
        }

        if (!Dinero.TryParsePositivo(_nuevaLimite, out var limite))
        {
            NuevaError = "Límite inválido.";
            return;
        }

        if (!int.TryParse(_nuevaCierre, out var cierre) || cierre < 1 || cierre > 31)
        {
            NuevaError = "Día de cierre inválido.";
            return;
        }

        if (!int.TryParse(_nuevaPago, out var pago) || pago < 1 || pago > 31)
        {
            NuevaError = "Día de pago inválido.";
            return;
        }

        _db.InsertarTarjeta(new TarjetaCredito
        {
            Nombre        = _nuevaNombre.Trim(),
            LimiteCredito = limite,
            DiaCierre     = cierre,
            DiaPago       = pago
        }, _uid);

        NuevaNombre = string.Empty;
        NuevaLimite = string.Empty;
        NuevaCierre = "15";
        NuevaPago   = "5";
        Cargar();
    }

    private void EliminarTarjeta(TarjetaCredito? t)
    {
        if (t is null) return;
        var res = System.Windows.MessageBox.Show(
            $"¿Eliminar la tarjeta \"{t.Nombre}\"? Esta acción no se puede deshacer.",
            "Confirmar eliminación",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (res != System.Windows.MessageBoxResult.Yes) return;
        _db.EliminarTarjeta(t.Id);
        if (_seleccionada?.Id == t.Id) Seleccionada = null;
        Cargar();
    }

    private void Abonar()
    {
        if (_seleccionada is null) return;
        MensajeError = string.Empty;

        if (!Dinero.TryParsePositivo(_abonoMonto, out var monto))
        {
            MensajeError = "Monto de abono inválido.";
            return;
        }

        // El pago sale de una cuenta concreta. Antes venía fijo como "Banco BAC" y con
        // CuentaId nulo, así que bajaba el balance global sin descontar de ninguna cuenta.
        var cuenta = _cuentaAbono;

        _db.InsertarTransaccion(new Transaccion
        {
            Tipo             = "Gasto",
            Monto            = monto,
            Categoria        = "Tarjeta",
            Descripcion      = $"Pago tarjeta {_seleccionada.Nombre}",
            Notas            = "Abono registrado manualmente",
            Fecha            = DateTime.Today,
            CuentaNombre     = cuenta?.Nombre ?? "Efectivo",
            CuentaId         = cuenta?.Id,
            TarjetaCreditoId = null,                    // no es una compra con la tarjeta…
            PagoDeTarjetaId  = _seleccionada.Id         // …es el pago que la salda
        }, _uid);

        // Reducir saldo usado directamente
        _db.AbonarTarjeta(_seleccionada.Id, monto);

        AbonoMonto = string.Empty;
        Cargar();
    }
}
