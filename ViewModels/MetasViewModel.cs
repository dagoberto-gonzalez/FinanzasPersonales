using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;

namespace FinanzasPersonales.ViewModels;

// ── Una meta ──────────────────────────────────────────────────────────────────

/// <summary>
/// Una meta en pantalla: un apartado dentro de una cuenta (D5), con sus tres acciones —apartar,
/// usar, liberar— y sus propios mensajes, para que el error aparezca junto a la meta y no arriba
/// de la pantalla, lejos de donde se pulsó.
/// </summary>
public class MetaAhorroVm : BaseViewModel
{
    private static readonly Brush BrushVerde  = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
    private static readonly Brush BrushAccent = new SolidColorBrush(Color.FromRgb(0x9F, 0x67, 0xFA));

    private readonly MetasViewModel _parent;

    public MetaAhorro Meta { get; }

    public int      Id              => Meta.Id;
    public string   Nombre          => Meta.Nombre;
    public decimal  MontoObjetivo   => Meta.MontoObjetivo;
    public decimal  Apartado        => Meta.Apartado;
    public DateTime FechaLimite     => Meta.FechaLimite;
    public double   ProgresoPercent => Meta.ProgresoPercent;
    public string   ProgresoTexto   => Meta.ProgresoTexto;
    public Brush    ProgresBrush    => Meta.ProgresoPercent >= 100 ? BrushVerde : BrushAccent;

    public string CuentaTexto => Meta.TieneCuenta ? $"En {Meta.CuentaNombre}" : "Sin cuenta";

    /// <summary>Cuánto se puede apartar todavía: lo libre de la cuenta.</summary>
    public string LibreTexto => _parent.LibreDe(Meta.CuentaId) is { } libre
        ? $"Libre en {Meta.CuentaNombre}: ₡{libre:N0}"
        : "";

    public Visibility ConCuentaVisibility => Meta.TieneCuenta ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SinCuentaVisibility => Meta.TieneCuenta ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Metas de antes de D5: lo que tenía su contador se apartará al elegir la cuenta.</summary>
    public string SinCuentaTexto => Meta.MontoAnterior > 0
        ? $"Elige la cuenta donde está este dinero. Los ₡{Meta.MontoAnterior:N0} que tenía registrados se apartarán en ella."
        : "Elige la cuenta donde está este dinero.";

    public ObservableCollection<Cuenta> Cuentas         => _parent.Cuentas;
    public ObservableCollection<string> CategoriasGasto => _parent.CategoriasGasto;

    private Cuenta? _cuentaParaAsignar;
    public Cuenta? CuentaParaAsignar
    {
        get => _cuentaParaAsignar;
        set { _cuentaParaAsignar = value; OnPropertyChanged(); }
    }

    private string _monto = string.Empty;
    public string Monto
    {
        get => _monto;
        set { _monto = value; OnPropertyChanged(); }
    }

    // ── Usar: pide categoría y descripción, porque se registra como un gasto real ──
    private bool   _usando;
    private string _categoriaUso   = "Otros";
    private string _descripcionUso = string.Empty;

    public Visibility UsoVisibility     => _usando ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AccionesVisibility => _usando || !Meta.TieneCuenta ? Visibility.Collapsed : Visibility.Visible;

    public string CategoriaUso
    {
        get => _categoriaUso;
        set { _categoriaUso = value ?? "Otros"; OnPropertyChanged(); }
    }
    public string DescripcionUso
    {
        get => _descripcionUso;
        set { _descripcionUso = value; OnPropertyChanged(); }
    }

    private string _error = string.Empty;
    public string MensajeError
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeErrorVisibility)); }
    }
    public Visibility MensajeErrorVisibility =>
        string.IsNullOrEmpty(_error) ? Visibility.Collapsed : Visibility.Visible;

    public ICommand ApartarCommand      { get; }
    public ICommand LiberarCommand      { get; }
    public ICommand EmpezarUsoCommand   { get; }
    public ICommand ConfirmarUsoCommand { get; }
    public ICommand CancelarUsoCommand  { get; }
    public ICommand AsignarCuentaCommand { get; }
    public ICommand EliminarCommand     { get; }

    public MetaAhorroVm(MetaAhorro meta, MetasViewModel parent)
    {
        Meta    = meta;
        _parent = parent;

        ApartarCommand       = new RelayCommand(() => ConMonto(m => _parent.Apartar(this, m)));
        LiberarCommand       = new RelayCommand(() => ConMonto(m => _parent.Liberar(this, m)));
        EmpezarUsoCommand    = new RelayCommand(EmpezarUso);
        ConfirmarUsoCommand  = new RelayCommand(() => ConMonto(m => _parent.Usar(this, m, _categoriaUso, _descripcionUso)));
        CancelarUsoCommand   = new RelayCommand(() => { _usando = false; NotificarUso(); });
        AsignarCuentaCommand = new RelayCommand(() => _parent.AsignarCuenta(this, _cuentaParaAsignar));
        EliminarCommand      = new RelayCommand(() => _parent.EliminarMeta(this));
    }

    /// <summary>Lee el monto con Dinero (antes era decimal.TryParse) y ejecuta la acción.</summary>
    private void ConMonto(Action<decimal> accion)
    {
        MensajeError = string.Empty;
        if (!Dinero.TryParsePositivo(_monto, out var monto))
        {
            MensajeError = "Escribe un monto mayor que cero.";
            return;
        }
        accion(monto);
    }

    private void EmpezarUso()
    {
        MensajeError   = string.Empty;
        DescripcionUso = Meta.Nombre;
        if (!CategoriasGasto.Contains(_categoriaUso))
            CategoriaUso = CategoriasGasto.FirstOrDefault() ?? "Otros";
        _usando = true;
        NotificarUso();
    }

    private void NotificarUso()
    {
        OnPropertyChanged(nameof(UsoVisibility));
        OnPropertyChanged(nameof(AccionesVisibility));
    }
}

// ── ViewModel principal ───────────────────────────────────────────────────────

public class MetasViewModel : BaseViewModel
{
    private readonly AppDatabase _db;
    private readonly int         _uid;

    // ── Formulario nueva meta ─────────────────────────────────────────────────
    private string   _nombre        = string.Empty;
    private string   _montoObjetivo = string.Empty;
    private DateTime _fechaLimite   = DateTime.Today.AddMonths(3);
    private Cuenta?  _cuentaNueva;
    private string   _error         = string.Empty;

    public string Nombre
    {
        get => _nombre;
        set { _nombre = value; OnPropertyChanged(); }
    }
    public string MontoObjetivo
    {
        get => _montoObjetivo;
        set { _montoObjetivo = value; OnPropertyChanged(); }
    }
    public DateTime FechaLimite
    {
        get => _fechaLimite;
        set { _fechaLimite = value; OnPropertyChanged(); }
    }

    /// <summary>La cuenta donde está el dinero de la meta. Obligatoria desde D5.</summary>
    public Cuenta? CuentaNueva
    {
        get => _cuentaNueva;
        set { _cuentaNueva = value; OnPropertyChanged(); }
    }

    public string MensajeError
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeErrorVisibility)); }
    }
    public Visibility MensajeErrorVisibility =>
        string.IsNullOrEmpty(_error) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SinMetasVisibility =>
        Metas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<MetaAhorroVm> Metas           { get; } = [];
    public ObservableCollection<Cuenta>       Cuentas         { get; } = [];
    public ObservableCollection<string>       CategoriasGasto { get; } = [];

    private Dictionary<int, decimal> _libres = [];
    internal decimal? LibreDe(int? cuentaId) =>
        cuentaId is { } id && _libres.TryGetValue(id, out var libre) ? libre : null;

    public ICommand CrearMetaCommand { get; }

    public MetasViewModel(AppDatabase db, int usuarioId)
    {
        _db              = db;
        _uid             = usuarioId;
        CrearMetaCommand = new RelayCommand(CrearMeta);
        CargarMetas();
    }

    public void Actualizar() => CargarMetas();

    private void CrearMeta()
    {
        MensajeError = string.Empty;

        if (string.IsNullOrWhiteSpace(_nombre))
        {
            MensajeError = "Escribe un nombre para la meta.";
            return;
        }
        if (!Dinero.TryParsePositivo(_montoObjetivo, out var objetivo))
        {
            MensajeError = "Ingresa un monto objetivo válido.";
            return;
        }
        if (_cuentaNueva is null)
        {
            MensajeError = "Elige la cuenta donde está (o va a estar) el dinero de la meta.";
            return;
        }

        Intentar(null, () => _db.InsertarMeta(new MetaAhorro
        {
            Nombre        = _nombre.Trim(),
            MontoObjetivo = objetivo,
            FechaLimite   = _fechaLimite,
            CuentaId      = _cuentaNueva.Id
        }, _uid));

        if (!string.IsNullOrEmpty(MensajeError)) return;
        Nombre        = string.Empty;
        MontoObjetivo = string.Empty;
        FechaLimite   = DateTime.Today.AddMonths(3);
    }

    internal void Apartar(MetaAhorroVm vm, decimal monto) =>
        Intentar(vm, () => _db.ApartarEnMeta(vm.Id, monto, _uid));

    internal void Liberar(MetaAhorroVm vm, decimal monto) =>
        Intentar(vm, () => _db.LiberarDeMeta(vm.Id, monto, _uid));

    internal void Usar(MetaAhorroVm vm, decimal monto, string categoria, string descripcion) =>
        Intentar(vm, () => _db.UsarDeMeta(vm.Id, monto, categoria, descripcion, DateTime.Today, _uid));

    internal void AsignarCuenta(MetaAhorroVm vm, Cuenta? cuenta)
    {
        if (cuenta is null)
        {
            vm.MensajeError = "Elige una cuenta.";
            return;
        }
        Intentar(vm, () => _db.AsignarCuentaAMeta(vm.Id, cuenta.Id, _uid));
    }

    public void EliminarMeta(MetaAhorroVm vm)
    {
        var res = MessageBox.Show(
            $"¿Eliminar la meta \"{vm.Nombre}\"?\n\n" +
            (vm.Apartado > 0 ? $"Los ₡{vm.Apartado:N0} apartados vuelven a quedar libres en la cuenta. " : "") +
            "Lo que ya se usó de ella se conserva como gasto.",
            "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;

        _db.EliminarMeta(vm.Id);
        CargarMetas();
    }

    /// <summary>
    /// Las reglas viven en AppDatabase (no apartar más de lo libre, no usar más de lo apartado…);
    /// aquí sólo se muestran, junto a la meta si hay una.
    /// </summary>
    private void Intentar(MetaAhorroVm? vm, Action accion)
    {
        try
        {
            accion();
            CargarMetas();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            if (vm is not null) vm.MensajeError = ex.Message;
            else                MensajeError    = ex.Message;
        }
    }

    private void CargarMetas()
    {
        var cuentas = _db.ObtenerCuentas(_uid);
        _libres = cuentas.ToDictionary(c => c.Id, c => c.Libre);

        var previa = _cuentaNueva?.Id;
        Cuentas.Clear();
        foreach (var c in cuentas.Where(c => c.Activa)) Cuentas.Add(c);
        CuentaNueva = Cuentas.FirstOrDefault(c => c.Id == previa) ?? Cuentas.FirstOrDefault();

        // Las tarjetas se recrean abajo, así que vaciar la lista no deja ningún ComboBox
        // sin su elemento seleccionado.
        Metas.Clear();
        CategoriasGasto.Clear();
        foreach (var c in _db.ObtenerCategorias(_uid, Categoria.TipoGasto).Where(c => !AppDatabase.EsCategoriaAutomatica(c)))
            CategoriasGasto.Add(c);

        foreach (var m in _db.ObtenerMetas(_uid))
            Metas.Add(new MetaAhorroVm(m, this));
        OnPropertyChanged(nameof(SinMetasVisibility));
    }
}
