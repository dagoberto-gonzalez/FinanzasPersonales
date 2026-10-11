using System.Collections.ObjectModel;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;

namespace FinanzasPersonales.ViewModels;

public class ReportesViewModel : BaseViewModel
{
    private readonly AppDatabase _db;
    private readonly int         _uid;

    // ── Barra anual ───────────────────────────────────────────────────────────
    public ObservableCollection<BarMonth> DatosMensuales { get; } = [];

    // ── Resumen del año ───────────────────────────────────────────────────────
    private int     _anioSeleccionado = DateTime.Today.Year;
    private decimal _ingresosAnio;
    private decimal _gastosAnio;

    public int AnioSeleccionado
    {
        get => _anioSeleccionado;
        set { _anioSeleccionado = value; OnPropertyChanged(); CalcularResumenAnual(); }
    }

    public string IngresosAnioTexto  => $"₡{_ingresosAnio:N0}";
    public string GastosAnioTexto    => $"₡{_gastosAnio:N0}";
    public string BalanceAnioTexto   => $"₡{(_ingresosAnio - _gastosAnio):N0}";

    public IReadOnlyList<int> AniosDisponibles { get; private set; } = [];

    // ── Comparación de meses (SelectedIndex, 0-based) ─────────────────────────
    private int _mesIndexA = DateTime.Today.Month - 1;
    private int _mesIndexB = DateTime.Today.Month == 1 ? 11 : DateTime.Today.Month - 2;
    private int _anioA     = DateTime.Today.Year;
    private int _anioB     = DateTime.Today.Month == 1 ? DateTime.Today.Year - 1 : DateTime.Today.Year;

    public int MesIndexA { get => _mesIndexA; set { _mesIndexA = value; OnPropertyChanged(); CompararMeses(); } }
    public int MesIndexB { get => _mesIndexB; set { _mesIndexB = value; OnPropertyChanged(); CompararMeses(); } }
    public int AnioA     { get => _anioA;     set { _anioA = value;     OnPropertyChanged(); CompararMeses(); } }
    public int AnioB     { get => _anioB;     set { _anioB = value;     OnPropertyChanged(); CompararMeses(); } }

    private decimal _ingresosA, _gastosA, _ingresosB, _gastosB;
    public string IngresosATexto => $"₡{_ingresosA:N0}";
    public string GastosATexto   => $"₡{_gastosA:N0}";
    public string IngresosBTexto => $"₡{_ingresosB:N0}";
    public string GastosBTexto   => $"₡{_gastosB:N0}";

    public IReadOnlyList<string> NombresMeses { get; } =
        ["Enero","Febrero","Marzo","Abril","Mayo","Junio",
         "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre"];

    // ── Historial de cierres mensuales ────────────────────────────────────────
    public ObservableCollection<ResumenMensual> ResumenesMensuales { get; } = [];

    public System.Windows.Visibility SinResumenesVisibility =>
        ResumenesMensuales.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility ConResumenesVisibility =>
        ResumenesMensuales.Count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    // ── Exportar PDF ──────────────────────────────────────────────────────────
    public ICommand ExportarPdfCommand { get; }

    private string _estadoExportar = string.Empty;
    public string EstadoExportar
    {
        get => _estadoExportar;
        set { _estadoExportar = value; OnPropertyChanged(); }
    }

    public ReportesViewModel(AppDatabase db, int usuarioId)
    {
        _db               = db;
        _uid              = usuarioId;
        ExportarPdfCommand = new RelayCommand(ExportarPdf);
        Cargar();
    }

    public void Actualizar() => Cargar();

    private void Cargar()
    {
        DatosMensuales.Clear();
        foreach (var d in _db.ObtenerDatosMensuales(_uid, 1))
            DatosMensuales.Add(d);

        var todas = _db.ObtenerTransacciones(_uid);
        var anios = todas.Select(t => t.Fecha.Year).Distinct().OrderDescending().ToList();
        if (!anios.Contains(DateTime.Today.Year))
            anios.Insert(0, DateTime.Today.Year);
        AniosDisponibles = anios;
        OnPropertyChanged(nameof(AniosDisponibles));

        ResumenesMensuales.Clear();
        foreach (var r in _db.ObtenerResumenesMensuales(_uid))
            ResumenesMensuales.Add(r);
        OnPropertyChanged(nameof(SinResumenesVisibility));
        OnPropertyChanged(nameof(ConResumenesVisibility));

        CalcularResumenAnual();
        CompararMeses();
    }

    private void CalcularResumenAnual()
    {
        var del_anio = _db.ObtenerTransacciones(_uid)
            .Where(t => t.Fecha.Year == _anioSeleccionado && t.AfectaPresupuesto).ToList();

        _ingresosAnio = del_anio.Where(t => t.Tipo == "Ingreso").Sum(t => t.Monto);
        _gastosAnio   = del_anio.Where(t => t.Tipo == "Gasto").Sum(t => t.Monto);

        OnPropertyChanged(nameof(IngresosAnioTexto));
        OnPropertyChanged(nameof(GastosAnioTexto));
        OnPropertyChanged(nameof(BalanceAnioTexto));
    }

    private void CompararMeses()
    {
        var todas  = _db.ObtenerTransacciones(_uid);
        int mesA   = _mesIndexA + 1;
        int mesB   = _mesIndexB + 1;

        var transA = todas.Where(t => t.Fecha.Year == _anioA && t.Fecha.Month == mesA && t.AfectaPresupuesto).ToList();
        var transB = todas.Where(t => t.Fecha.Year == _anioB && t.Fecha.Month == mesB && t.AfectaPresupuesto).ToList();

        _ingresosA = transA.Where(t => t.Tipo == "Ingreso").Sum(t => t.Monto);
        _gastosA   = transA.Where(t => t.Tipo == "Gasto").Sum(t => t.Monto);
        _ingresosB = transB.Where(t => t.Tipo == "Ingreso").Sum(t => t.Monto);
        _gastosB   = transB.Where(t => t.Tipo == "Gasto").Sum(t => t.Monto);

        OnPropertyChanged(nameof(IngresosATexto));
        OnPropertyChanged(nameof(GastosATexto));
        OnPropertyChanged(nameof(IngresosBTexto));
        OnPropertyChanged(nameof(GastosBTexto));
    }

    private void ExportarPdf()
    {
        try
        {
            EstadoExportar = "Generando PDF...";
            var transacciones = _db.ObtenerTransacciones(_uid);
            var gastosFijos   = _db.ObtenerGastosFijosConEstado(DateTime.Today.Year, DateTime.Today.Month, _uid);
            var tarjetas      = _db.ObtenerTarjetas(_uid);

            var ruta = PdfExportService.Exportar(transacciones, gastosFijos, tarjetas, _anioSeleccionado);
            EstadoExportar = $"Guardado: {ruta}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            EstadoExportar = $"Error: {ex.Message}";
        }
    }
}
