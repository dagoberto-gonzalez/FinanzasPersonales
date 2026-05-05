using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;

namespace FinanzasPersonales.ViewModels;

// ── Wrapper por meta ──────────────────────────────────────────────────────────
public class MetaAhorroVm : BaseViewModel
{
    private static readonly Brush BrushVerde  = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
    private static readonly Brush BrushAccent = new SolidColorBrush(Color.FromRgb(0x9F, 0x67, 0xFA));

    private readonly MetasViewModel _parent;

    public MetaAhorro Meta { get; }

    private string _montoAgregar = string.Empty;
    public string MontoAgregar
    {
        get => _montoAgregar;
        set { _montoAgregar = value; OnPropertyChanged(); }
    }

    public int      Id             => Meta.Id;
    public string   Nombre         => Meta.Nombre;
    public decimal  MontoObjetivo  => Meta.MontoObjetivo;
    public decimal  MontoActual    => Meta.MontoActual;
    public DateTime FechaLimite    => Meta.FechaLimite;
    public double   ProgresoPercent => Meta.ProgresoPercent;
    public string   ProgresoTexto  => Meta.ProgresoTexto;

    // Barra verde cuando se alcanza el 100 %
    public Brush ProgresBrush => Meta.ProgresoPercent >= 100 ? BrushVerde : BrushAccent;

    public ICommand AgregarDineroCommand { get; }
    public ICommand EliminarCommand      { get; }

    public MetaAhorroVm(MetaAhorro meta, MetasViewModel parent)
    {
        Meta    = meta;
        _parent = parent;

        AgregarDineroCommand = new RelayCommand(AgregarDinero);
        EliminarCommand      = new RelayCommand(() => _parent.EliminarMeta(this));
    }

    private void AgregarDinero()
    {
        if (!decimal.TryParse(_montoAgregar.Replace(",", "."),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var monto)
            || monto <= 0)
        {
            _parent.MensajeError = "Ingresa un monto válido mayor a 0.";
            return;
        }

        _parent.AgregarDineroAMeta(Meta, monto);
        MontoAgregar = string.Empty;
    }
}

// ── ViewModel principal ───────────────────────────────────────────────────────
public class MetasViewModel : BaseViewModel
{
    private readonly AppDatabase _db;
    private readonly int         _uid;

    private string   _nombre        = string.Empty;
    private string   _montoObjetivo = string.Empty;
    private DateTime _fechaLimite   = DateTime.Today.AddMonths(3);
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
    public string MensajeError
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeErrorVisibility)); }
    }
    public Visibility MensajeErrorVisibility =>
        string.IsNullOrEmpty(_error) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SinMetasVisibility =>
        Metas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<MetaAhorroVm> Metas { get; } = [];

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

        if (!decimal.TryParse(_montoObjetivo.Replace(",", "."),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var objetivo)
            || objetivo <= 0)
        {
            MensajeError = "Ingresa un monto objetivo válido.";
            return;
        }

        _db.InsertarMeta(new MetaAhorro
        {
            Nombre        = _nombre.Trim(),
            MontoObjetivo = objetivo,
            MontoActual   = 0,
            FechaLimite   = _fechaLimite
        }, _uid);

        Nombre        = string.Empty;
        MontoObjetivo = string.Empty;
        FechaLimite   = DateTime.Today.AddMonths(3);

        CargarMetas();
    }

    public void AgregarDineroAMeta(MetaAhorro meta, decimal monto)
    {
        MensajeError = string.Empty;
        _db.ActualizarMontoMeta(meta.Id, meta.MontoActual + monto);
        CargarMetas();
    }

    public void EliminarMeta(MetaAhorroVm vm)
    {
        _db.EliminarMeta(vm.Id);
        CargarMetas();
    }

    private void CargarMetas()
    {
        Metas.Clear();
        foreach (var m in _db.ObtenerMetas(_uid))
            Metas.Add(new MetaAhorroVm(m, this));
        OnPropertyChanged(nameof(SinMetasVisibility));
    }
}
