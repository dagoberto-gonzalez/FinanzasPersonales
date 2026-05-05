using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;

namespace FinanzasPersonales.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private static readonly Color[] PieColors =
    {
        Color.FromRgb(0xF3, 0x8B, 0xA8),
        Color.FromRgb(0xFA, 0xB3, 0x87),
        Color.FromRgb(0xF9, 0xE2, 0xAF),
        Color.FromRgb(0xA6, 0xE3, 0xA1),
        Color.FromRgb(0x89, 0xB4, 0xFA),
        Color.FromRgb(0xCB, 0xA6, 0xF7),
    };

    private static readonly Brush BrushVerde = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
    private static readonly Brush BrushRojo  = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));

    private readonly AppDatabase _db;
    private readonly int         _uid;

    private (int Anio, int Mes) _mesACerrar;

    private decimal              _balanceTotal;
    private decimal              _ingresosMes;
    private decimal              _gastosMes;
    private IEnumerable<PieSlice> _gastosSeries = [];
    private IEnumerable<BarMonth> _barrasMes    = [];

    public decimal BalanceTotal
    {
        get => _balanceTotal;
        private set { _balanceTotal = value; OnPropertyChanged(); OnPropertyChanged(nameof(BalanceTotalTexto)); OnPropertyChanged(nameof(BalanceBrush)); }
    }

    public string BalanceTotalTexto => $"₡{_balanceTotal:N0}";
    public Brush  BalanceBrush      => _balanceTotal >= 0 ? BrushVerde : BrushRojo;

    public decimal IngresosMes
    {
        get => _ingresosMes;
        private set { _ingresosMes = value; OnPropertyChanged(); OnPropertyChanged(nameof(IngresosMesTexto)); }
    }
    public string IngresosMesTexto => $"₡{_ingresosMes:N0}";

    public decimal GastosMes
    {
        get => _gastosMes;
        private set { _gastosMes = value; OnPropertyChanged(); OnPropertyChanged(nameof(GastosMesTexto)); OnPropertyChanged(nameof(PresupuestoExcedido)); OnPropertyChanged(nameof(PresupuestoTexto)); }
    }
    public string GastosMesTexto => $"₡{_gastosMes:N0}";

    public IEnumerable<PieSlice> GastosSeries
    {
        get => _gastosSeries;
        private set { _gastosSeries = value; OnPropertyChanged(); }
    }

    public IEnumerable<BarMonth> BarrasMes
    {
        get => _barrasMes;
        private set { _barrasMes = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Transaccion>    UltimasTransacciones { get; } = [];
    public ObservableCollection<GastoFijo>      GastosUrgentes        { get; } = [];
    public ObservableCollection<TarjetaCredito> TarjetasProximasPago  { get; } = [];
    public ObservableCollection<Cuenta>         CuentasSaldo          { get; } = [];

    public Visibility SinTransaccionesVisibility =>
        UltimasTransacciones.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AlertasTarjetasVisibility =>
        TarjetasProximasPago.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    // ── Presupuesto ───────────────────────────────────────────────────────────
    private decimal _presupuesto;

    public bool       PresupuestoExcedido    => _presupuesto > 0 && _gastosMes > _presupuesto;
    public Visibility BannerPresupuestoVisibility =>
        PresupuestoExcedido ? Visibility.Visible : Visibility.Collapsed;
    public string     PresupuestoTexto       => _presupuesto > 0
        ? $"⚠ Gastos del mes (₡{_gastosMes:N0}) superan el presupuesto (₡{_presupuesto:N0})"
        : string.Empty;

    // ── Alertas gastos urgentes ────────────────────────────────────────────────
    public Visibility AlertasVisibility =>
        GastosUrgentes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    // ── Banner cierre de mes ───────────────────────────────────────────────────
    private Visibility _bannerCierreMesVisibility = Visibility.Collapsed;
    private string     _bannerCierreMesTexto       = string.Empty;

    public Visibility BannerCierreMesVisibility
    {
        get => _bannerCierreMesVisibility;
        private set { _bannerCierreMesVisibility = value; OnPropertyChanged(); }
    }

    public string BannerCierreMesTexto
    {
        get => _bannerCierreMesTexto;
        private set { _bannerCierreMesTexto = value; OnPropertyChanged(); }
    }

    public RelayCommand CerrarMesCommand { get; }

    public DashboardViewModel(AppDatabase db, int usuarioId)
    {
        _db             = db;
        _uid            = usuarioId;
        CerrarMesCommand = new RelayCommand(EjecutarCierreMes);
        Cargar();
    }

    public void Cargar()
    {
        var ahora  = DateTime.Now;

        // ── Detectar mes anterior sin cerrar ─────────────────────────────────
        var mesAnterior = ahora.AddMonths(-1);
        if (!_db.MesCerrado(_uid, mesAnterior.Year, mesAnterior.Month))
        {
            var label = new DateTime(mesAnterior.Year, mesAnterior.Month, 1)
                .ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-CR"));
            _mesACerrar              = (mesAnterior.Year, mesAnterior.Month);
            BannerCierreMesTexto     = $"El mes de {label} aún no ha sido cerrado. ¿Deseas cerrarlo ahora?";
            BannerCierreMesVisibility = Visibility.Visible;
        }
        else
        {
            BannerCierreMesVisibility = Visibility.Collapsed;
        }

        var inicio = new DateTime(ahora.Year, ahora.Month, 1);
        var fin    = inicio.AddMonths(1).AddDays(-1);

        var todas    = _db.ObtenerTransacciones(_uid);
        var estesMes = todas.Where(t => t.Fecha >= inicio && t.Fecha <= fin).ToList();

        // Excluir cargos a tarjeta de crédito: aún no son dinero real gastado
        var todasEfectivas    = todas.Where(t => t.TarjetaCreditoId == null).ToList();
        var estesMesEfectivos = estesMes.Where(t => t.TarjetaCreditoId == null).ToList();

        BalanceTotal = todasEfectivas.Sum(t => t.Tipo == "Ingreso" ? t.Monto : -t.Monto);
        IngresosMes  = estesMesEfectivos.Where(t => t.Tipo == "Ingreso").Sum(t => t.Monto);
        GastosMes    = estesMesEfectivos.Where(t => t.Tipo == "Gasto").Sum(t => t.Monto);

        // Presupuesto mensual
        if (decimal.TryParse(_db.ObtenerConfig("presupuesto_mensual", "0"), out var pres))
            _presupuesto = pres;

        UltimasTransacciones.Clear();
        foreach (var t in todas.Take(8))
            UltimasTransacciones.Add(t);
        OnPropertyChanged(nameof(SinTransaccionesVisibility));

        // Gastos urgentes (vencen en 3 días o menos, sin pagar)
        GastosUrgentes.Clear();
        var gastos = _db.ObtenerGastosFijosConEstado(ahora.Year, ahora.Month, _uid);
        foreach (var g in gastos.Where(g => g.EsUrgente || g.EsVencido))
            GastosUrgentes.Add(g);
        OnPropertyChanged(nameof(AlertasVisibility));

        // Tarjetas con fecha de pago próxima (≤3 días)
        TarjetasProximasPago.Clear();
        foreach (var tarj in _db.ObtenerTarjetas(_uid))
        {
            if (tarj.DiasHastaPago >= 0 && tarj.DiasHastaPago <= 7)
                TarjetasProximasPago.Add(tarj);
        }
        OnPropertyChanged(nameof(AlertasTarjetasVisibility));

        // Saldo por cuenta
        CuentasSaldo.Clear();
        foreach (var c in _db.ObtenerCuentas(_uid).Where(c => c.Activa))
            CuentasSaldo.Add(c);

        // Gráfica de barras ingresos vs gastos (mes actual)
        BarrasMes = new List<BarMonth>
        {
            new BarMonth
            {
                MesLabel = ahora.ToString("MMM yyyy"),
                Ingresos = (double)IngresosMes,
                Gastos   = (double)GastosMes
            }
        };

        // Gráfica de torta
        var grupos = estesMesEfectivos
            .Where(t => t.Tipo == "Gasto")
            .GroupBy(t => t.Categoria)
            .ToDictionary(g => g.Key, g => (double)g.Sum(t => t.Monto));

        var total = grupos.Values.Sum();

        GastosSeries = grupos
            .Select((kvp, i) => new PieSlice
            {
                Label      = kvp.Key,
                Value      = kvp.Value,
                Percentage = total > 0 ? kvp.Value / total * 100 : 0,
                ColorBrush = new SolidColorBrush(PieColors[i % PieColors.Length])
            })
            .ToList();
    }

    private void EjecutarCierreMes()
    {
        var (anio, mes) = _mesACerrar;
        var totales     = _db.ObtenerTotalesMes(_uid, anio, mes);
        var balance     = totales.Ingresos - totales.Gastos;
        var label       = new DateTime(anio, mes, 1)
            .ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-CR"));

        var msg = $"Cierre de {label}\n\n" +
                  $"Ingresos:       ₡{totales.Ingresos:N0}\n" +
                  $"Gastos:         ₡{totales.Gastos:N0}\n" +
                  $"Balance del mes: ₡{balance:N0}\n" +
                  $"Transacciones:  {totales.NumTrans}\n\n" +
                  "¿Confirmar cierre?";

        var result = System.Windows.MessageBox.Show(
            msg, "Confirmar cierre de mes",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        _db.CerrarMes(_uid, anio, mes);
        Cargar();
    }
}
