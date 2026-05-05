using System.Collections.ObjectModel;
using System.Globalization;
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

    public int    Anio         { get; private set; }
    public int    Mes          { get; private set; }
    public string MesAnioTexto => $"Mes actual — {new DateTime(Anio, Mes, 1):MMMM yyyy}";

    // ── Resumen ───────────────────────────────────────────────────────────────
    private decimal _totalPendiente;
    private decimal _totalRecibido;

    public string TotalPendienteTexto => $"₡{_totalPendiente:N0}";
    public string TotalRecibidoTexto  => $"₡{_totalRecibido:N0}";

    // ── Comandos ──────────────────────────────────────────────────────────────
    public ICommand GuardarCommand        { get; }
    public ICommand EliminarCommand       { get; }
    public ICommand ToggleRecibidoCommand { get; }

    public IngresosFijosViewModel(AppDatabase db, int usuarioId)
    {
        _db                 = db;
        _uid                = usuarioId;
        GuardarCommand      = new RelayCommand(Guardar);
        EliminarCommand     = new RelayCommand<IngresoFijo>(Eliminar);
        ToggleRecibidoCommand = new RelayCommand<IngresoFijo>(ToggleRecibido);
        Cargar();
    }

    public void Actualizar() => Cargar();

    private void Cargar()
    {
        var ahora = DateTime.Today;
        Anio = ahora.Year;
        Mes  = ahora.Month;

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

        if (!decimal.TryParse(_monto.Replace(",", "."),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var montoDecimal)
            || montoDecimal <= 0)
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
        }

        _db.InsertarIngresoFijo(new IngresoFijo
        {
            Nombre      = _nombre.Trim(),
            Monto       = montoDecimal,
            DiaIngreso  = dia,
            DiaIngreso2 = dia2,
            CuentaId    = _cuentaSeleccionada?.Id
        }, _uid);

        Nombre      = string.Empty;
        Monto       = string.Empty;
        DiaIngreso  = "1";
        DiaIngreso2 = string.Empty;
        Cargar();
    }

    private void Eliminar(IngresoFijo? i)
    {
        if (i is null) return;
        var res = System.Windows.MessageBox.Show(
            $"¿Eliminar el ingreso fijo \"{i.Nombre}\"?",
            "Confirmar eliminación",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (res != System.Windows.MessageBoxResult.Yes) return;
        _db.EliminarIngresoFijo(i.Id);
        Cargar();
    }

    private void ToggleRecibido(IngresoFijo? i)
    {
        if (i is null || i.PagoMensualId == 0) return;
        _db.MarcarIngresoFijoRecibido(i.PagoMensualId, !i.RecibidoEsteMes, _uid);
        Cargar();
    }
}
