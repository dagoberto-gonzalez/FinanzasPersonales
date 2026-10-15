using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;
using System.Windows.Input;

namespace FinanzasPersonales.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private static Brush BrushVerde => Temas.Pincel(Temas.Actual.Green);
    private static Brush BrushRojo  => Temas.Pincel(Temas.Actual.Red);

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

    private decimal _enCuentas;
    private decimal _enEfectivo;

    /// <summary>De dónde sale el disponible, para poder cuadrarlo con Cuentas.</summary>
    public string DesgloseDisponibleTexto => $"En cuentas ₡{_enCuentas:N0}  ·  En efectivo ₡{_enEfectivo:N0}";

    private decimal _apartadoMetas;
    public string     ApartadoMetasTexto      => $"₡{_apartadoMetas:N0}";
    public Visibility ApartadoMetasVisibility => _apartadoMetas > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Brush  BalanceBrush      => _balanceTotal >= 0 ? BrushVerde : BrushRojo;

    // ── Deuda de tarjetas ─────────────────────────────────────────────────────
    // Va aparte del disponible a propósito: son dos cosas distintas y juntarlas en un
    // único número es lo que hacía que el Dashboard no significara nada preciso.
    private decimal _deudaTarjetas;

    public decimal DeudaTarjetas
    {
        get => _deudaTarjetas;
        private set
        {
            _deudaTarjetas = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DeudaTarjetasTexto));
            OnPropertyChanged(nameof(DeudaTarjetasVisibility));
        }
    }

    public string DeudaTarjetasTexto => $"₡{_deudaTarjetas:N0}";

    public Visibility DeudaTarjetasVisibility =>
        _deudaTarjetas > 0 ? Visibility.Visible : Visibility.Collapsed;

    public decimal IngresosMes
    {
        get => _ingresosMes;
        private set { _ingresosMes = value; OnPropertyChanged(); OnPropertyChanged(nameof(IngresosMesTexto)); }
    }
    public string IngresosMesTexto => $"₡{_ingresosMes:N0}";

    public decimal GastosMes
    {
        get => _gastosMes;
        private set { _gastosMes = value; OnPropertyChanged(); OnPropertyChanged(nameof(GastosMesTexto)); NotificarPresupuesto(); }
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
    // Es de cada usuario y se edita aquí mismo. Antes era una clave global fija en ₡500.000 que
    // no se podía cambiar, y sólo avisaba cuando ya se había superado.
    private decimal _presupuesto;

    /// <summary>Fracción gastada desde la que la barra avisa en amarillo.</summary>
    public const decimal UmbralAviso = 0.8m;

    public bool TienePresupuesto    => _presupuesto > 0;
    public bool PresupuestoExcedido => TienePresupuesto && _gastosMes > _presupuesto;
    public Visibility BannerPresupuestoVisibility =>
        PresupuestoExcedido ? Visibility.Visible : Visibility.Collapsed;
    public string     PresupuestoTexto       => TienePresupuesto
        ? $"⚠ Gastos del mes (₡{_gastosMes:N0}) superan el presupuesto (₡{_presupuesto:N0})"
        : string.Empty;

    /// <summary>Gastado / presupuesto. Puede pasar de 1.</summary>
    public decimal PresupuestoFraccion => TienePresupuesto ? _gastosMes / _presupuesto : 0m;

    public string PresupuestoUsoTexto => $"₡{_gastosMes:N0} de ₡{_presupuesto:N0}  ·  {PresupuestoFraccion:P0}";

    public string PresupuestoRestanteTexto => _gastosMes <= _presupuesto
        ? $"Te quedan ₡{_presupuesto - _gastosMes:N0}"
        : $"Te pasaste por ₡{_gastosMes - _presupuesto:N0}";

    /// <summary>Verde, amarillo desde el 80 %, rojo al pasarse.</summary>
    public string PresupuestoColor =>
        PresupuestoExcedido                   ? Temas.Actual.Red :
        PresupuestoFraccion >= UmbralAviso    ? Temas.Actual.Yellow : Temas.Actual.Green;

    // La barra son dos columnas en estrella: lo gastado y lo que queda.
    public GridLength BarraLlena => new((double)Math.Min(PresupuestoFraccion, 1m), GridUnitType.Star);
    public GridLength BarraVacia => new((double)Math.Max(1m - PresupuestoFraccion, 0m), GridUnitType.Star);

    public Visibility ConPresupuestoVisibility => TienePresupuesto && !_editandoPresupuesto ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SinPresupuestoVisibility => !TienePresupuesto && !_editandoPresupuesto ? Visibility.Visible : Visibility.Collapsed;

    // Edición en la propia tarjeta
    private bool   _editandoPresupuesto;
    private string _presupuestoEditado = "";
    private string _presupuestoError   = "";

    public Visibility EdicionPresupuestoVisibility => _editandoPresupuesto ? Visibility.Visible : Visibility.Collapsed;

    public string PresupuestoEditado
    {
        get => _presupuestoEditado;
        set { _presupuestoEditado = value; OnPropertyChanged(); }
    }
    public string PresupuestoError
    {
        get => _presupuestoError;
        set { _presupuestoError = value; OnPropertyChanged(); OnPropertyChanged(nameof(PresupuestoErrorVisibility)); }
    }
    public Visibility PresupuestoErrorVisibility =>
        string.IsNullOrEmpty(_presupuestoError) ? Visibility.Collapsed : Visibility.Visible;

    public ICommand EditarPresupuestoCommand   { get; }
    public ICommand GuardarPresupuestoCommand  { get; }
    public ICommand CancelarPresupuestoCommand { get; }

    private void EditarPresupuesto()
    {
        PresupuestoEditado   = TienePresupuesto ? _presupuesto.ToString("N0") : "";
        PresupuestoError     = "";
        _editandoPresupuesto = true;
        NotificarPresupuesto();
    }

    private void GuardarPresupuesto()
    {
        decimal monto = 0m;   // vacío = sin presupuesto
        if (!string.IsNullOrWhiteSpace(_presupuestoEditado)
            && (!Dinero.TryParse(_presupuestoEditado, out monto) || monto < 0))
        {
            PresupuestoError = "Monto inválido. Déjalo vacío o en 0 para no tener presupuesto.";
            return;
        }

        _db.GuardarPresupuesto(_uid, monto);
        _presupuesto         = monto;
        _editandoPresupuesto = false;
        PresupuestoError     = "";
        NotificarPresupuesto();
    }

    private void CancelarPresupuesto()
    {
        _editandoPresupuesto = false;
        PresupuestoError     = "";
        NotificarPresupuesto();
    }

    /// <summary>
    /// Todo lo que depende del presupuesto, en un solo sitio. Antes se notificaban dos
    /// propiedades y no BannerPresupuestoVisibility —la que enlaza la vista—, así que el aviso
    /// de "te pasaste" podía no aparecer o no irse al volver al Dashboard.
    /// </summary>
    private void NotificarPresupuesto()
    {
        foreach (var p in new[]
                 {
                     nameof(TienePresupuesto), nameof(PresupuestoExcedido), nameof(BannerPresupuestoVisibility),
                     nameof(PresupuestoTexto), nameof(PresupuestoFraccion), nameof(PresupuestoUsoTexto),
                     nameof(PresupuestoRestanteTexto), nameof(PresupuestoColor), nameof(BarraLlena),
                     nameof(BarraVacia), nameof(ConPresupuestoVisibility), nameof(SinPresupuestoVisibility),
                     nameof(EdicionPresupuestoVisibility)
                 })
            OnPropertyChanged(p);
    }

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
        EditarPresupuestoCommand   = new RelayCommand(EditarPresupuesto);
        GuardarPresupuestoCommand  = new RelayCommand(GuardarPresupuesto);
        CancelarPresupuestoCommand = new RelayCommand(CancelarPresupuesto);
        Cargar();
    }

    public void Cargar()
    {
        var ahora  = DateTime.Now;

        // ── Meses sin cerrar: se ofrece el más antiguo; al cerrarlo aparece el siguiente ──
        var pendientes = _db.MesesSinCerrar(_uid, ahora);
        if (pendientes.Count > 0)
        {
            var (anio, mes) = pendientes[0];
            var label = new DateTime(anio, mes, 1)
                .ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-CR"));
            _mesACerrar              = (anio, mes);
            BannerCierreMesTexto     = pendientes.Count == 1
                ? $"El mes de {label} aún no ha sido cerrado. ¿Deseas cerrarlo ahora?"
                : $"Tienes {pendientes.Count} meses sin cerrar. El más antiguo es {label}. ¿Deseas cerrarlo ahora?";
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

        // Los dos ejes de la decisión D2, que antes se mezclaban en un solo número:
        //  · efectivo    → cuánto dinero hay realmente disponible
        //  · presupuesto → en qué se gastó, aunque todavía no haya salido de la cuenta
        var efectivoTodas   = todas.Where(t => t.AfectaEfectivo).ToList();
        var presupuestoMes  = estesMes.Where(t => t.AfectaPresupuesto).ToList();

        // Disponible = lo que hay en las cuentas + el efectivo (movimientos sin cuenta).
        // Antes era sólo la suma de las transacciones del eje efectivo, que deja fuera el saldo
        // inicial de cada cuenta: con ₡2.700.000 de saldos iniciales el Dashboard decía ₡162.000.
        // Tomando el saldo de cada cuenta tal como lo calcula ObtenerCuentas, el Dashboard y la
        // pantalla Cuentas no pueden dar cifras distintas.
        var cuentas = _db.ObtenerCuentas(_uid);
        _enCuentas  = cuentas.Sum(c => c.SaldoActual);
        _enEfectivo = _db.ObtenerSaldoEfectivo(_uid);   // la misma cifra que la fila Efectivo de Cuentas
        BalanceTotal = _enCuentas + _enEfectivo;
        OnPropertyChanged(nameof(DesgloseDisponibleTexto));

        IngresosMes  = presupuestoMes.Where(t => t.Tipo == "Ingreso").Sum(t => t.Monto);
        GastosMes    = presupuestoMes.Where(t => t.Tipo == "Gasto").Sum(t => t.Monto);

        // Deuda de tarjetas: compras a crédito que todavía no se han pagado.
        DeudaTarjetas = _db.ObtenerTarjetas(_uid).Sum(x => x.SaldoUsado);

        // D5: dinero que sigue en las cuentas pero reservado para metas. Se muestra aparte, sin
        // restarlo del disponible: es tuyo y está ahí, sólo que ya tiene destino.
        _apartadoMetas = cuentas.Sum(c => c.Apartado);
        OnPropertyChanged(nameof(ApartadoMetasTexto));
        OnPropertyChanged(nameof(ApartadoMetasVisibility));

        // Presupuesto mensual: de este usuario, no global.
        _presupuesto = _db.ObtenerPresupuesto(_uid);
        NotificarPresupuesto();

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
        // El gráfico de categorías usa el eje presupuesto: lo interesante es en qué gastaste,
        // no cuándo salió el dinero de la cuenta.
        var grupos = presupuestoMes
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
                ColorBrush = Temas.Pincel(Temas.Actual.Grafico[i % Temas.Actual.Grafico.Length])
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
