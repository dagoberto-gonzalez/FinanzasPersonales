using System.Collections.ObjectModel;
using FinanzasPersonales.Services;
using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;

namespace FinanzasPersonales.ViewModels;

public class IngresosFijosViewModel : BaseViewModel
{
    private readonly AppDatabase _db;
    private readonly int         _uid;

    // ── Formulario ────────────────────────────────────────────────────────────
    private string _nombre      = string.Empty;
    private string _monto       = string.Empty;
    private string _diaIngreso  = "1";
    private string _diaIngreso2 = string.Empty;
    private string _error       = string.Empty;

    public string Nombre
    {
        get => _nombre;
        set { _nombre = value; OnPropertyChanged(); }
    }
    public string Monto
    {
        get => _monto;
        set { _monto = value; OnPropertyChanged(); }
    }
    public string DiaIngreso
    {
        get => _diaIngreso;
        set { _diaIngreso = value; OnPropertyChanged(); }
    }
    public string DiaIngreso2
    {
        get => _diaIngreso2;
        set { _diaIngreso2 = value; OnPropertyChanged(); }
    }
    public string MensajeError
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeErrorVisibility)); }
    }
    public Visibility MensajeErrorVisibility =>
        string.IsNullOrEmpty(_error) ? Visibility.Collapsed : Visibility.Visible;

    // ── Edición ───────────────────────────────────────────────────────────────
    // Ver GastosFijosViewModel: antes sólo se podía crear y borrar.
    private int?   _editandoId;
    private string _nombreOriginal = "";

    public bool       EstaEditando       => _editandoId is not null;
    public Visibility EditandoVisibility => EstaEditando ? Visibility.Visible : Visibility.Collapsed;
    public string     TituloFormulario   => EstaEditando ? $"Editar ingreso fijo: {_nombreOriginal}" : "Agregar ingreso fijo";
    public string     TextoGuardar       => EstaEditando ? "Guardar cambios" : "Agregar";

    private void NotificarEdicion()
    {
        OnPropertyChanged(nameof(EstaEditando));
        OnPropertyChanged(nameof(EditandoVisibility));
        OnPropertyChanged(nameof(TituloFormulario));
        OnPropertyChanged(nameof(TextoGuardar));
    }

    // ── Cuenta para nuevos ingresos fijos ────────────────────────────────────
    private Cuenta? _cuentaSeleccionada;
    public  Cuenta? CuentaSeleccionada
    {
        get => _cuentaSeleccionada;
        set { _cuentaSeleccionada = value; OnPropertyChanged(); }
    }

    // ── Datos ─────────────────────────────────────────────────────────────────
    public ObservableCollection<IngresoFijo>           IngresosFijos      { get; } = [];
    public ObservableCollection<GrupoDia<IngresoFijo>> GruposIngresos     { get; } = [];
    public ObservableCollection<Cuenta>                CuentasDisponibles { get; } = [];

    // ── Mes que se mira (ver GastosFijosViewModel) ───────────────────────────
    private readonly MesNavegable _mes = new();

    public int    Anio         => _mes.Anio;
    public int    Mes          => _mes.Mes;
    public string MesAnioTexto => _mes.Texto;
    public Visibility VolverAlActualVisibility => _mes.VolverVisibility;

    public ICommand MesAnteriorCommand  { get; }
    public ICommand MesSiguienteCommand { get; }
    public ICommand MesActualCommand    { get; }

    private void IrA(Action mover)
    {
        mover();
        Cargar();
    }

    // ── Resumen ───────────────────────────────────────────────────────────────
    private decimal _totalPendiente;
    private decimal _totalRecibido;

    public string TotalPendienteTexto => $"₡{_totalPendiente:N0}";
    public string TotalRecibidoTexto  => $"₡{_totalRecibido:N0}";

    // ── Comandos ──────────────────────────────────────────────────────────────
    public ICommand GuardarCommand        { get; }
    public ICommand EliminarCommand       { get; }
    public ICommand ToggleRecibidoCommand { get; }
    public ICommand EditarCommand         { get; }
    public ICommand CancelarEdicionCommand { get; }

    public IngresosFijosViewModel(AppDatabase db, int usuarioId)
    {
        _db                 = db;
        _uid                = usuarioId;
        GuardarCommand      = new RelayCommand(Guardar);
        EliminarCommand     = new RelayCommand<IngresoFijo>(Eliminar);
        ToggleRecibidoCommand = new RelayCommand<IngresoFijo>(ToggleRecibido);
        EditarCommand       = new RelayCommand<IngresoFijo>(Editar);
        CancelarEdicionCommand = new RelayCommand(LimpiarFormulario);
        MesAnteriorCommand  = new RelayCommand(() => IrA(_mes.Anterior));
        MesSiguienteCommand = new RelayCommand(() => IrA(_mes.Siguiente));
        MesActualCommand    = new RelayCommand(() => IrA(_mes.Actual));
        Cargar();
    }

    public void Actualizar() => Cargar();

    private void Cargar()
    {
        _mes.SeguirAlActual();
        OnPropertyChanged(nameof(Anio));
        OnPropertyChanged(nameof(Mes));
        OnPropertyChanged(nameof(MesAnioTexto));
        OnPropertyChanged(nameof(VolverAlActualVisibility));

        // Cargar cuentas disponibles
        var prevCuentaId = _cuentaSeleccionada?.Id;
        CuentasDisponibles.Clear();
        foreach (var c in _db.ObtenerCuentas(_uid).Where(c => c.Activa))
            CuentasDisponibles.Add(c);
        CuentaSeleccionada = prevCuentaId.HasValue
            ? CuentasDisponibles.FirstOrDefault(c => c.Id == prevCuentaId)
            : null;

        IngresosFijos.Clear();
        var lista = _db.ObtenerIngresosFijosConEstado(Anio, Mes, _uid);
        foreach (var i in lista)
            IngresosFijos.Add(i);

        GruposIngresos.Clear();
        foreach (var grp in IngresosFijos.GroupBy(i => i.DiaEspecifico).OrderBy(g => g.Key))
            GruposIngresos.Add(new GrupoDia<IngresoFijo>(grp.Key, grp));

        _totalPendiente = lista.Where(i => !i.RecibidoEsteMes).Sum(i => i.Monto);
        _totalRecibido  = lista.Where(i =>  i.RecibidoEsteMes).Sum(i => i.Monto);
        OnPropertyChanged(nameof(TotalPendienteTexto));
        OnPropertyChanged(nameof(TotalRecibidoTexto));
    }

    private void Guardar()
    {
        MensajeError = string.Empty;

        if (string.IsNullOrWhiteSpace(_nombre))
        {
            MensajeError = "El nombre es requerido.";
            return;
        }

        // Dinero y no decimal.TryParse: al editar, el monto llega como "600.000".
        if (!Dinero.TryParsePositivo(_monto, out var montoDecimal))
        {
            MensajeError = "Ingresa un monto válido mayor a 0.";
            return;
        }

        if (!int.TryParse(_diaIngreso, out var dia) || dia < 1 || dia > 31)
        {
            MensajeError = "El día debe ser entre 1 y 31.";
            return;
        }

        int dia2 = 0;
        if (!string.IsNullOrWhiteSpace(_diaIngreso2))
        {
            if (!int.TryParse(_diaIngreso2, out dia2) || dia2 < 1 || dia2 > 31)
            {
                MensajeError = "El segundo día debe ser entre 1 y 31.";
                return;
            }
            if (dia2 == dia)
            {
                MensajeError = "El segundo día no puede ser igual al primero. Déjalo vacío si solo se recibe una vez al mes.";
                return;
            }
        }

        var ingreso = new IngresoFijo
        {
            Id          = _editandoId ?? 0,
            Nombre      = _nombre.Trim(),
            Monto       = montoDecimal,
            DiaIngreso  = dia,
            DiaIngreso2 = dia2,
            CuentaId    = _cuentaSeleccionada?.Id,
            FechaInicio = _mes.Inicio   // existe desde el mes que se está mirando
        };

        try
        {
            if (_editandoId is null) _db.InsertarIngresoFijo(ingreso, _uid);
            else                     _db.ActualizarIngresoFijo(ingreso, _uid);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            MensajeError = ex.Message;
            return;
        }

        LimpiarFormulario();
        Cargar();
    }

    private void LimpiarFormulario()
    {
        _editandoId     = null;
        _nombreOriginal = "";
        NotificarEdicion();
        MensajeError    = string.Empty;
        Nombre          = string.Empty;
        Monto           = string.Empty;
        DiaIngreso      = "1";
        DiaIngreso2     = string.Empty;
    }

    /// <summary>
    /// Carga el ingreso en el formulario, leído de la base: un ingreso con dos días aparece en
    /// dos filas y cada una sólo conoce su propio día.
    /// </summary>
    private void Editar(IngresoFijo? fila)
    {
        if (fila is null) return;
        var i = _db.ObtenerIngresoFijo(fila.Id, _uid);
        if (i is null) { Cargar(); return; }

        _editandoId     = i.Id;
        _nombreOriginal = i.Nombre;
        Nombre          = i.Nombre;
        Monto           = i.Monto % 1 == 0 ? i.Monto.ToString("N0") : i.Monto.ToString("N2");
        DiaIngreso      = i.DiaIngreso.ToString();
        DiaIngreso2     = i.DiaIngreso2 > 0 ? i.DiaIngreso2.ToString() : string.Empty;

        // Una cuenta desactivada no está en la lista, pero el ingreso sigue apuntando a ella.
        if (i.CuentaId is { } cid && CuentasDisponibles.All(c => c.Id != cid)
            && _db.ObtenerCuentas(_uid).FirstOrDefault(c => c.Id == cid) is { } inactiva)
            CuentasDisponibles.Add(inactiva);
        CuentaSeleccionada = i.CuentaId is { } id ? CuentasDisponibles.FirstOrDefault(c => c.Id == id) : null;

        MensajeError = string.Empty;
        NotificarEdicion();
    }

    private void Eliminar(IngresoFijo? i)
    {
        if (i is null) return;
        var res = System.Windows.MessageBox.Show(
            $"¿Eliminar el ingreso fijo \"{i.Nombre}\"?\n\n" +
            "Dejará de aparecer como pendiente. Lo que ya recibiste se conserva en Transacciones.",
            "Confirmar eliminación",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (res != System.Windows.MessageBoxResult.Yes) return;
        _db.EliminarIngresoFijo(i.Id);
        if (_editandoId == i.Id) LimpiarFormulario();
        Cargar();
    }

    private void ToggleRecibido(IngresoFijo? i)
    {
        if (i is null || i.PagoMensualId == 0) return;
        _db.MarcarIngresoFijoRecibido(i.PagoMensualId, !i.RecibidoEsteMes, _uid);
        Cargar();
    }
}
