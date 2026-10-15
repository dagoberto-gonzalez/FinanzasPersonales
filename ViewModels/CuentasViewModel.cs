using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;

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
    private string  _editSaldoInicial = "0";
    private string  _error      = string.Empty;
    private string  _mensajeOk  = string.Empty;

    public Cuenta? Seleccionada
    {
        get => _seleccionada;
        set
        {
            // La fila Efectivo no es una cuenta: no abre el panel de edición.
            if (value is { EsEfectivo: true }) value = null;
            _seleccionada = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HaySeleccion));
            if (value is not null)
            {
                EditNombre       = value.Nombre;
                EditTipo         = value.Tipo;
                EditBanco        = value.Banco;
                EditActiva       = value.Activa;
                EditSaldoInicial = value.SaldoInicial.ToString("N0");
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

    /// <summary>
    /// Saldo de apertura: lo que había en la cuenta antes de la primera transacción registrada.
    /// El saldo mostrado se deriva de este valor más las transacciones, así que editarlo aquí
    /// es la forma de enderezar una cuenta que no cuadra con el banco.
    /// </summary>
    public string EditSaldoInicial
    {
        get => _editSaldoInicial;
        set { _editSaldoInicial = value; OnPropertyChanged(); }
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
    private string _nuevaSaldoInicial = "0";
    private string _nuevaError  = string.Empty;

    /// <summary>Saldo que ya tenía la cuenta al empezar a registrarla en la aplicación.</summary>
    public string NuevoSaldoInicial
    {
        get => _nuevaSaldoInicial;
        set { _nuevaSaldoInicial = value; OnPropertyChanged(); }
    }

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

    // ── Transferir ────────────────────────────────────────────────────────────
    // Antes, pasar dinero de una cuenta a otra obligaba a registrar un gasto y un ingreso, y los
    // dos contaban en reportes y presupuesto como dinero gastado y ganado.

    /// <summary>Origen o destino de una transferencia: una cuenta, o el efectivo (Id null).</summary>
    public sealed record OpcionCuenta(int? Id, string Nombre)
    {
        public override string ToString() => Nombre;
    }

    public ObservableCollection<OpcionCuenta> OpcionesTransferencia { get; } = [];

    private OpcionCuenta? _transOrigen;
    private OpcionCuenta? _transDestino;
    private string   _transMonto = string.Empty;
    private DateTime _transFecha = DateTime.Today;
    private string   _transNota  = string.Empty;
    private string   _transError = string.Empty;

    public OpcionCuenta? TransOrigen
    {
        get => _transOrigen;
        set { _transOrigen = value; OnPropertyChanged(); }
    }
    public OpcionCuenta? TransDestino
    {
        get => _transDestino;
        set { _transDestino = value; OnPropertyChanged(); }
    }
    public string TransMonto
    {
        get => _transMonto;
        set { _transMonto = value; OnPropertyChanged(); }
    }
    public DateTime TransFecha
    {
        get => _transFecha;
        set { _transFecha = value; OnPropertyChanged(); }
    }
    public string TransNota
    {
        get => _transNota;
        set { _transNota = value; OnPropertyChanged(); }
    }
    public string TransError
    {
        get => _transError;
        set { _transError = value; OnPropertyChanged(); OnPropertyChanged(nameof(TransErrorVisibility)); }
    }
    public Visibility TransErrorVisibility =>
        string.IsNullOrEmpty(_transError) ? Visibility.Collapsed : Visibility.Visible;

    // Propio, no MensajeOk: aquél vive en el panel de edición, que sólo se ve con una cuenta
    // seleccionada, y la confirmación de la transferencia no aparecería.
    private string _transOk = string.Empty;
    public string TransOk
    {
        get => _transOk;
        set { _transOk = value; OnPropertyChanged(); OnPropertyChanged(nameof(TransOkVisibility)); }
    }
    public Visibility TransOkVisibility =>
        string.IsNullOrEmpty(_transOk) ? Visibility.Collapsed : Visibility.Visible;

    public ICommand TransferirCommand { get; }

    private void Transferir()
    {
        TransError = string.Empty;
        TransOk    = string.Empty;

        if (_transOrigen is null || _transDestino is null)
        {
            TransError = "Elige de dónde sale y a dónde va el dinero.";
            return;
        }
        if (!Dinero.TryParsePositivo(_transMonto, out var monto))
        {
            TransError = "Escribe un monto mayor que cero.";
            return;
        }

        try
        {
            _db.Transferir(_transOrigen.Id, _transDestino.Id, monto, _transFecha, _transNota, _uid);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            TransError = ex.Message;
            return;
        }

        TransOk    = $"Transferidos ₡{monto:N0} de {_transOrigen.Nombre} a {_transDestino.Nombre}.";
        TransMonto = string.Empty;
        TransNota  = string.Empty;
        TransFecha = DateTime.Today;
        Cargar();
    }

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
        TransferirCommand     = new RelayCommand(Transferir);
        Cargar();
    }

    public void Actualizar() => Cargar();

    private void Cargar()
    {
        var prevId = _seleccionada?.Id;
        Cuentas.Clear();
        foreach (var c in _db.ObtenerCuentas(_uid))
            Cuentas.Add(c);
        // El efectivo al final, para ver todo el dinero en un sitio. De paso, ocupar el nombre
        // impide crear una cuenta llamada "Efectivo" (saldrían dos en las transferencias).
        Cuentas.Add(Cuenta.Efectivo(_db.ObtenerSaldoEfectivo(_uid)));

        if (prevId.HasValue)
            Seleccionada = Cuentas.FirstOrDefault(c => c.Id == prevId);

        // Origen y destino: el efectivo y las cuentas activas. Son records, así que después de
        // reconstruir la lista la opción elegida sigue siendo igual a una de las nuevas.
        var (origen, destino) = (_transOrigen, _transDestino);
        OpcionesTransferencia.Clear();
        OpcionesTransferencia.Add(new OpcionCuenta(null, "Efectivo"));
        foreach (var c in Cuentas.Where(c => c.Activa && !c.EsEfectivo))
            OpcionesTransferencia.Add(new OpcionCuenta(c.Id, c.Nombre));
        TransOrigen  = OpcionesTransferencia.FirstOrDefault(o => o == origen);
        TransDestino = OpcionesTransferencia.FirstOrDefault(o => o == destino);
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

        if (!Dinero.TryParse(_nuevaSaldoInicial, out var saldoInicial))
        {
            NuevaError = "Saldo inicial inválido. Usa 0 si la cuenta arranca vacía.";
            return;
        }

        try
        {
            _db.InsertarCuenta(new Cuenta
            {
                Nombre       = _nuevaNombre.Trim(),
                Tipo         = _nuevaTipo,
                Banco        = _nuevaBanco.Trim(),
                Activa       = true,
                SaldoInicial = saldoInicial
            }, _uid);

            NuevaNombre       = string.Empty;
            NuevoTipo         = "Cuenta corriente";
            NuevoBanco        = string.Empty;
            NuevoSaldoInicial = "0";
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

        if (!Dinero.TryParse(_editSaldoInicial, out var saldoInicial))
        {
            MensajeError = "Saldo inicial inválido.";
            return;
        }

        try
        {
            _seleccionada.Nombre       = _editNombre.Trim();
            _seleccionada.Tipo         = _editTipo;
            _seleccionada.Banco        = _editBanco.Trim();
            _seleccionada.Activa       = _editActiva;
            _seleccionada.SaldoInicial = saldoInicial;
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
        MensajeError = string.Empty;
        MensajeOk    = string.Empty;

        // Una cuenta con movimientos no se elimina: se desactiva. Se avisa antes de preguntar,
        // en vez de dejar que la persona confirme algo que después se va a rechazar.
        var movimientos = _db.ContarMovimientosDeCuenta(_seleccionada.Id);
        if (movimientos > 0)
        {
            var desactivar = System.Windows.MessageBox.Show(
                $"\"{_seleccionada.Nombre}\" tiene {movimientos} movimiento{(movimientos == 1 ? "" : "s")} " +
                "registrado" + (movimientos == 1 ? "" : "s") + ", así que no se puede eliminar sin " +
                "dejar ese historial sin cuenta.\n\n" +
                "¿Quieres desactivarla? Dejará de aparecer al registrar transacciones, pero el " +
                "historial y los saldos se conservan.",
                "La cuenta tiene movimientos",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (desactivar != System.Windows.MessageBoxResult.Yes) return;

            _seleccionada.Activa = false;
            _db.ActualizarCuenta(_seleccionada);
            MensajeOk = "Cuenta desactivada. El historial se mantiene intacto.";
            Cargar();
            return;
        }

        var res = System.Windows.MessageBox.Show(
            $"¿Eliminar la cuenta \"{_seleccionada.Nombre}\"?",
            "Confirmar eliminación",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (res != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            _db.EliminarCuenta(_seleccionada.Id);
            Seleccionada = null;
            Cargar();
        }
        catch (InvalidOperationException ex)
        {
            MensajeError = ex.Message;
        }
    }
}
