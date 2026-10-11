using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;

namespace FinanzasPersonales.ViewModels;

// ── Wrapper observable por ítem ───────────────────────────────────────────────
public class GastoFijoVm : BaseViewModel
{
    public GastoFijo Datos { get; }

    private string _metodoPago = "Efectivo";
    public string MetodoPagoSeleccionado
    {
        get => _metodoPago;
        set { _metodoPago = value; OnPropertyChanged(); }
    }

    // Delegados de propiedades del modelo
    public int     Id              => Datos.Id;
    public string  Nombre          => Datos.Nombre;
    public decimal Monto           => Datos.Monto;
    public int     DiaEspecifico   => Datos.DiaEspecifico;
    public bool    PagadoEsteMes   => Datos.PagadoEsteMes;
    public int     PagoMensualId   => Datos.PagoMensualId;
    public bool    EsUrgente       => Datos.EsUrgente;
    public bool    EsVencido       => Datos.EsVencido;
    public int     DiasParaVencer  => Datos.DiasParaVencer;

    public GastoFijoVm(GastoFijo datos) => Datos = datos;
}

// ── ViewModel principal ───────────────────────────────────────────────────────
public class GastosFijosViewModel : BaseViewModel
{
    private readonly AppDatabase _db;
    private readonly int         _uid;

    // ── Formulario ────────────────────────────────────────────────────────────
    private string _nombre          = string.Empty;
    private string _monto           = string.Empty;
    private string _diaVencimiento  = "1";
    private string _diaVencimiento2 = string.Empty;
    private string _error           = string.Empty;

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
    public string DiaVencimiento
    {
        get => _diaVencimiento;
        set { _diaVencimiento = value; OnPropertyChanged(); }
    }
    public string DiaVencimiento2
    {
        get => _diaVencimiento2;
        set { _diaVencimiento2 = value; OnPropertyChanged(); }
    }
    public string MensajeError
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeErrorVisibility)); }
    }
    public Visibility MensajeErrorVisibility =>
        string.IsNullOrEmpty(_error) ? Visibility.Collapsed : Visibility.Visible;

    // ── Datos ─────────────────────────────────────────────────────────────────
    public ObservableCollection<GastoFijoVm>              GastosFijos   { get; } = [];
    public ObservableCollection<GrupoDia<GastoFijoVm>>    GruposGastos  { get; } = [];

    public ObservableCollection<string> MetodosPago { get; } = [];
    private readonly Dictionary<string, (int? CuentaId, int? TarjetaId)> _metodosMap = [];

    public int    Anio        { get; private set; }
    public int    Mes         { get; private set; }
    public string MesAnioTexto => $"Mes actual — {new DateTime(Anio, Mes, 1):MMMM yyyy}";

    // ── Resumen ───────────────────────────────────────────────────────────────
    private decimal _totalPendiente;
    private decimal _totalPagado;

    public string TotalPendienteTexto => $"₡{_totalPendiente:N0}";
    public string TotalPagadoTexto    => $"₡{_totalPagado:N0}";

    // ── Comandos ──────────────────────────────────────────────────────────────
    public ICommand GuardarCommand    { get; }
    public ICommand EliminarCommand   { get; }
    public ICommand TogglePagoCommand { get; }

    public GastosFijosViewModel(AppDatabase db, int usuarioId)
    {
        _db              = db;
        _uid             = usuarioId;
        GuardarCommand   = new RelayCommand(Guardar);
        EliminarCommand  = new RelayCommand<GastoFijoVm>(Eliminar);
        TogglePagoCommand = new RelayCommand<GastoFijoVm>(TogglePago);
        Cargar();
    }

    public void Actualizar() => Cargar();

    private void Cargar()
    {
        var ahora = DateTime.Today;
        Anio = ahora.Year;
        Mes  = ahora.Month;

        // Reconstruir métodos de pago desde cuentas y tarjetas reales
        _metodosMap.Clear();
        MetodosPago.Clear();
        MetodosPago.Add("Efectivo");
        _metodosMap["Efectivo"] = (null, null);
        foreach (var c in _db.ObtenerCuentas(_uid).Where(c => c.Activa))
        {
            MetodosPago.Add(c.Nombre);
            _metodosMap[c.Nombre] = (c.Id, null);
        }
        foreach (var t in _db.ObtenerTarjetas(_uid))
        {
            MetodosPago.Add(t.Nombre);
            _metodosMap[t.Nombre] = (null, t.Id);
        }

        GastosFijos.Clear();
        var lista = _db.ObtenerGastosFijosConEstado(Anio, Mes, _uid);
        foreach (var g in lista)
            GastosFijos.Add(new GastoFijoVm(g));

        GruposGastos.Clear();
        foreach (var grp in GastosFijos.GroupBy(g => g.DiaEspecifico).OrderBy(g => g.Key))
            GruposGastos.Add(new GrupoDia<GastoFijoVm>(grp.Key, grp));

        _totalPendiente = lista.Where(g => !g.PagadoEsteMes).Sum(g => g.Monto);
        _totalPagado    = lista.Where(g =>  g.PagadoEsteMes).Sum(g => g.Monto);
        OnPropertyChanged(nameof(TotalPendienteTexto));
        OnPropertyChanged(nameof(TotalPagadoTexto));
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

        if (!int.TryParse(_diaVencimiento, out var dia) || dia < 1 || dia > 31)
        {
            MensajeError = "El día debe ser entre 1 y 31.";
            return;
        }

        int dia2 = 0;
        if (!string.IsNullOrWhiteSpace(_diaVencimiento2))
        {
            if (!int.TryParse(_diaVencimiento2, out dia2) || dia2 < 1 || dia2 > 31)
            {
                MensajeError = "El segundo día debe ser entre 1 y 31.";
                return;
            }
            if (dia2 == dia)
            {
                MensajeError = "El segundo día no puede ser igual al primero. Déjalo vacío si solo se paga una vez al mes.";
                return;
            }
        }

        _db.InsertarGastoFijo(new GastoFijo
        {
            Nombre          = _nombre.Trim(),
            Monto           = montoDecimal,
            DiaVencimiento  = dia,
            DiaVencimiento2 = dia2
        }, _uid);

        Nombre          = string.Empty;
        Monto           = string.Empty;
        DiaVencimiento  = "1";
        DiaVencimiento2 = string.Empty;
        Cargar();
    }

    private void Eliminar(GastoFijoVm? vm)
    {
        if (vm is null) return;
        var res = System.Windows.MessageBox.Show(
            $"¿Eliminar el gasto fijo \"{vm.Nombre}\"?",
            "Confirmar eliminación",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (res != System.Windows.MessageBoxResult.Yes) return;
        _db.EliminarGastoFijo(vm.Id);
        Cargar();
    }

    private void TogglePago(GastoFijoVm? vm)
    {
        if (vm is null || vm.PagoMensualId == 0) return;

        if (!vm.PagadoEsteMes)
        {
            var metodo = vm.MetodoPagoSeleccionado;
            _metodosMap.TryGetValue(metodo, out var ids);
            _db.MarcarGastoFijoPagado(vm.PagoMensualId, true, metodo, _uid, ids.CuentaId, ids.TarjetaId);
        }
        else
        {
            _db.MarcarGastoFijoPagado(vm.PagoMensualId, false, string.Empty, _uid);
        }
        Cargar();
    }
}
