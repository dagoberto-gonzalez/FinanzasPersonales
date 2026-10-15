using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;

namespace FinanzasPersonales.ViewModels;

public class ControlLaboralViewModel : BaseViewModel
{
    private static readonly CultureInfo CrCulture =
        CultureInfo.GetCultureInfo("es-CR");

    private readonly AppDatabase _db;
    private readonly int         _uid;

    // ── Listas para ComboBoxes ────────────────────────────────────────────────
    public IReadOnlyList<string> ModosPago  { get; } = ["Mensual", "Quincenal", "Semanal"];
    public IReadOnlyList<string> DiasSemana { get; } = ["Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"];

    // ── Estado del período ────────────────────────────────────────────────────
    // El período es un RANGO DE FECHAS, no un mes. Ese era el fallo de fondo del módulo: la
    // base se calculaba por quincena pero las horas se sumaban del mes entero.
    private ConfiguracionLaboral _cfg = new();
    private DateTime _inicio;
    private DateTime _fin;

    // Inyectable para poder probar el paso de un período al siguiente.
    private readonly Func<DateTime> _hoy;

    // Si se está mirando el período de hoy, hay que seguirlo cuando cambie la fecha. Se recuerda
    // al navegar: deducirlo en cada carga comparando con el período de hoy falla justo cuando
    // importa, porque al cambiar de quincena el de hoy ya es otro.
    private bool _siguiendoAlActual = true;

    public string PeriodoActualLabel =>
        new PeriodoLaboral { FechaInicio = _inicio, FechaFin = _fin }.Label;

    public string PeriodoRangoTexto =>
        $"{_inicio:dd/MM/yyyy} — {_fin:dd/MM/yyyy}  ·  {(_fin - _inicio).Days + 1} días";

    /// <summary>Para poder resaltar cuándo se está mirando otro período distinto del de hoy.</summary>
    public bool EsPeriodoActual
    {
        get
        {
            var hoy = _cfg.PeriodoQueContiene(_hoy());
            return hoy.Inicio == _inicio && hoy.Fin == _fin;
        }
    }

    public Visibility VolverAlActualVisibility =>
        EsPeriodoActual ? Visibility.Collapsed : Visibility.Visible;

    // ── Configuración — campos internos ──────────────────────────────────────
    private decimal _salarioMensual;
    private decimal _jornadaSemanal;
    private string  _modoPago  = "Mensual";
    private int     _diaPago;
    private int     _diaPago2;
    private int     _diaSemana = 5;
    private decimal _viaticos;

    // ── Configuración — propiedades de UI ────────────────────────────────────
    private string _cfgSalarioMensual = "";
    private string _cfgJornada      = "48";
    private string _cfgModoPago     = "Mensual";
    private string _cfgDiaPago      = "15";
    private string _cfgDiaPago2     = "30";
    private string _cfgDiaSemana    = "Viernes";
    private string _cfgViaticos     = "0";
    private string _cfgMensaje      = "";
    private bool   _cfgMensajeOk;

    public string ConfigSalarioMensual
    {
        get => _cfgSalarioMensual;
        set { _cfgSalarioMensual = value; OnPropertyChanged(); }
    }
    public string ConfigJornada
    {
        get => _cfgJornada;
        set { _cfgJornada = value; OnPropertyChanged(); }
    }
    public string ConfigModoPago
    {
        get => _cfgModoPago;
        set
        {
            _cfgModoPago = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MensualVisibility));
            OnPropertyChanged(nameof(QuincenalVisibility));
            OnPropertyChanged(nameof(SemanalVisibility));
        }
    }
    public string ConfigDiaPago
    {
        get => _cfgDiaPago;
        set { _cfgDiaPago = value; OnPropertyChanged(); }
    }
    public string ConfigDiaPago2
    {
        get => _cfgDiaPago2;
        set { _cfgDiaPago2 = value; OnPropertyChanged(); }
    }
    public string ConfigDiaSemana
    {
        get => _cfgDiaSemana;
        set { _cfgDiaSemana = value; OnPropertyChanged(); }
    }
    public string ConfigViaticos
    {
        get => _cfgViaticos;
        set { _cfgViaticos = value; OnPropertyChanged(); }
    }

    private string _cfgDiasPorSemana = "5";
    private string _cfgPorcentaje     = "10.83";

    /// <summary>Días trabajados por semana. Estaba clavado en 6 dentro del cálculo.</summary>
    public string ConfigDiasPorSemana
    {
        get => _cfgDiasPorSemana;
        set { _cfgDiasPorSemana = value; OnPropertyChanged(); }
    }

    /// <summary>Porcentaje de deducción sobre el bruto. Cambia por ley.</summary>
    public string ConfigPorcentaje
    {
        get => _cfgPorcentaje;
        set { _cfgPorcentaje = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Muestra a cuánto sale la hora diaria con lo configurado. Es lo que vale una ausencia de
    /// día completo, y antes salía de un 6 invisible. (Los feriados ya no: se anotan en horas.)
    /// </summary>
    public string HorasDiariasTexto =>
        _cfg.HorasDiarias > 0
            ? $"Un día son {_cfg.HorasDiarias:0.##} h · es lo que descuenta una ausencia de día completo"
            : "";

    /// <summary>
    /// La hora como la valora la planilla, para compararla con el "Valor Base" del comprobante:
    /// las horas extra salen a 1,5 veces esto y las dobles al doble.
    /// </summary>
    public string ValorHoraTexto =>
        _cfg.SalarioMensual > 0
            ? $"Hora: ₡{_cfg.SalarioPorHora:N2} (mensual ÷ 240) · extra ₡{CalculoPeriodo.Tarifa(_cfg.SalarioPorHora, CalculoPeriodo.FactorExtra):N2} · doble ₡{CalculoPeriodo.Tarifa(_cfg.SalarioPorHora, CalculoPeriodo.FactorDoble):N2}"
            : "";
    public string ConfigMensaje
    {
        get => _cfgMensaje;
        set { _cfgMensaje = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConfigMensajeVisibility)); }
    }
    public bool ConfigMensajeOk
    {
        get => _cfgMensajeOk;
        set { _cfgMensajeOk = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConfigMensajeColor)); }
    }
    public string     ConfigMensajeColor      => _cfgMensajeOk ? Temas.Actual.Green : Temas.Actual.Red;
    public Visibility ConfigMensajeVisibility =>
        string.IsNullOrEmpty(_cfgMensaje) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility MensualVisibility   => _cfgModoPago == "Mensual"   ? Visibility.Visible : Visibility.Collapsed;
    public Visibility QuincenalVisibility => _cfgModoPago == "Quincenal" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SemanalVisibility   => _cfgModoPago == "Semanal"   ? Visibility.Visible : Visibility.Collapsed;

    // ── Formulario registro diario ────────────────────────────────────────────
    private string _regFecha               = DateTime.Today.ToString("dd/MM/yyyy");
    private string _regHorasExtraDiurnas   = "0";
    private string _regHorasExtraNocturnas = "0";
    private string _regHorasDobles         = "0";
    private string _regHorasFeriado        = "0";
    private bool   _regEsAusencia;
    private bool   _regTieneGoce           = true;
    private string _regError               = "";

    public string RegFecha
    {
        get => _regFecha;
        set { _regFecha = value; OnPropertyChanged(); }
    }
    public string RegHorasExtraDiurnas
    {
        get => _regHorasExtraDiurnas;
        set { _regHorasExtraDiurnas = value; OnPropertyChanged(); }
    }
    public string RegHorasExtraNocturnas
    {
        get => _regHorasExtraNocturnas;
        set { _regHorasExtraNocturnas = value; OnPropertyChanged(); }
    }
    public string RegHorasDobles
    {
        get => _regHorasDobles;
        set { _regHorasDobles = value; OnPropertyChanged(); }
    }
    /// <summary>Horas trabajadas en feriado; antes era una casilla que valía la jornada diaria.</summary>
    public string RegHorasFeriado
    {
        get => _regHorasFeriado;
        set { _regHorasFeriado = value; OnPropertyChanged(); }
    }
    public bool RegEsAusencia
    {
        get => _regEsAusencia;
        set { _regEsAusencia = value; OnPropertyChanged(); OnPropertyChanged(nameof(GoceVisibility)); }
    }
    public bool RegTieneGoce
    {
        get => _regTieneGoce;
        set { _regTieneGoce = value; OnPropertyChanged(); }
    }

    private string _regHorasAusencia = "";

    /// <summary>Horas que se faltaron. Vacío significa el día completo.</summary>
    public string RegHorasAusencia
    {
        get => _regHorasAusencia;
        set { _regHorasAusencia = value; OnPropertyChanged(); }
    }
    public string RegError
    {
        get => _regError;
        set { _regError = value; OnPropertyChanged(); OnPropertyChanged(nameof(RegErrorVisibility)); }
    }
    public Visibility RegErrorVisibility =>
        string.IsNullOrEmpty(_regError) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility GoceVisibility =>
        _regEsAusencia ? Visibility.Visible : Visibility.Collapsed;

    // ── Colecciones ───────────────────────────────────────────────────────────
    public ObservableCollection<RegistroDiaLaboral> Registros { get; } = [];
    public ObservableCollection<HistorialLaboralItem> Historial { get; } = [];
    public ObservableCollection<Cuenta> Cuentas { get; } = [];

    private RegistroDiaLaboral? _seleccionado;
    public RegistroDiaLaboral? Seleccionado
    {
        get => _seleccionado;
        set { _seleccionado = value; OnPropertyChanged(); OnPropertyChanged(nameof(HaySeleccion)); }
    }
    public bool HaySeleccion => _seleccionado is not null;

    // ── Resumen del período ───────────────────────────────────────────────────
    public string ResumenSalarioBase { get; private set; } = "—";
    public string ResumenExtraD        { get; private set; } = "0 h  →  ₡0";
    public string ResumenExtraN        { get; private set; } = "0 h  →  ₡0";
    public string ResumenDobles        { get; private set; } = "0 h  →  ₡0";
    public string ResumenFeriados      { get; private set; } = "0 h  →  ₡0";
    public string ResumenViaticos      { get; private set; } = "₡0";
    public string ResumenDescAusencias { get; private set; } = "0 días  →  ₡0";
    public string ResumenAusenciaGoce  { get; private set; } = "0 días";
    public string ResumenSalarioBruto  { get; private set; } = "₡0";
    public string ResumenDeduccionCCSS { get; private set; } = "₡0";
    public string ResumenSalarioNeto   { get; private set; } = "₡0";
    public string ResumenAguinaldo     { get; private set; } = "₡0";
    public string ResumenVacaciones    { get; private set; } = "₡0";

    private decimal _salarioBrutoCalc;
    private decimal _deduccionesCalc;
    private decimal _salarioNetoCalc;

    // ── Estado período ────────────────────────────────────────────────────────
    public Visibility AlertaPagoVisibility     { get; private set; } = Visibility.Collapsed;
    public string     AlertaPagoTexto          { get; private set; } = "";
    public Visibility PeriodoCerradoVisibility { get; private set; } = Visibility.Collapsed;
    public Visibility PeriodoAbiertVisibility  { get; private set; } = Visibility.Visible;
    public string     PeriodoCerradoTexto      { get; private set; } = "";

    // ── Historial ─────────────────────────────────────────────────────────────
    public string     HistorialTotalAnio     { get; private set; } = "₡0";
    public Visibility SinHistorialVisibility { get; private set; } = Visibility.Visible;
    public Visibility ConHistorialVisibility { get; private set; } = Visibility.Collapsed;

    // ── Propiedades públicas para pre-llenar la ventana modal ────────────────
    public decimal SalarioNetoCalculado => _salarioNetoCalc;

    // ── Formulario: Comprobante de pago ───────────────────────────────────────
    // Sustituye a "Editar cierre", que sobrescribía lo calculado con lo pagado y dejaba sin nada
    // con qué comparar. Lo calculado no se toca; el comprobante se guarda aparte.
    private bool                  _compVisible;
    private HistorialLaboralItem? _compTarget;
    private CalculoPeriodo?       _compCalculo;
    private string _compHorasNormales = "", _compMontoNormales = "";
    private string _compSalarioFijo = "", _compHorasExtra = "", _compMontoExtra = "",
                   _compHorasNoct   = "", _compMontoNoct  = "", _compHorasDobles = "",
                   _compMontoDobles = "", _compOtros      = "", _compViaticos   = "",
                   _compDeducciones = "", _compNotas      = "", _compError      = "";

    public bool ComprobanteVisible
    {
        get => _compVisible;
        set { _compVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(ComprobanteVisibility)); }
    }
    public Visibility ComprobanteVisibility =>
        _compVisible ? Visibility.Visible : Visibility.Collapsed;

    public string ComprobanteTitulo =>
        _compTarget is not null ? $"Comprobante de pago · {_compTarget.Label}" : "Comprobante de pago";

    /// <summary>La comparación se rehace con cada tecla: se ve qué cuadra mientras se copia.</summary>
    public ObservableCollection<LineaComparacion> Comparacion { get; } = [];

    private void SetComp(ref string campo, string value, [System.Runtime.CompilerServices.CallerMemberName] string? prop = null)
    {
        campo = value;
        OnPropertyChanged(prop);
        RecalcularComparacion();
    }

    public string CompSalarioFijo { get => _compSalarioFijo; set => SetComp(ref _compSalarioFijo, value); }
    public string CompHorasNormales { get => _compHorasNormales; set => SetComp(ref _compHorasNormales, value); }
    public string CompMontoNormales { get => _compMontoNormales; set => SetComp(ref _compMontoNormales, value); }
    public string CompHorasExtra  { get => _compHorasExtra;  set => SetComp(ref _compHorasExtra,  value); }
    public string CompMontoExtra  { get => _compMontoExtra;  set => SetComp(ref _compMontoExtra,  value); }
    public string CompHorasNoct   { get => _compHorasNoct;   set => SetComp(ref _compHorasNoct,   value); }
    public string CompMontoNoct   { get => _compMontoNoct;   set => SetComp(ref _compMontoNoct,   value); }
    public string CompHorasDobles { get => _compHorasDobles; set => SetComp(ref _compHorasDobles, value); }
    public string CompMontoDobles { get => _compMontoDobles; set => SetComp(ref _compMontoDobles, value); }
    public string CompOtros       { get => _compOtros;       set => SetComp(ref _compOtros,       value); }
    public string CompViaticos    { get => _compViaticos;    set => SetComp(ref _compViaticos,    value); }
    public string CompDeducciones { get => _compDeducciones; set => SetComp(ref _compDeducciones, value); }
    public string CompNotas
    {
        get => _compNotas;
        set { _compNotas = value; OnPropertyChanged(); }
    }
    public string CompError
    {
        get => _compError;
        set { _compError = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompErrorVisibility)); }
    }
    public Visibility CompErrorVisibility =>
        string.IsNullOrEmpty(_compError) ? Visibility.Collapsed : Visibility.Visible;

    // ── Commands ──────────────────────────────────────────────────────────────
    public RelayCommand GuardarConfigCommand   { get; }
    public RelayCommand AgregarRegistroCommand { get; }
    public RelayCommand<RegistroDiaLaboral> EliminarRegistroCommand { get; }
    public RelayCommand CerrarPeriodoCommand   { get; }
    public RelayCommand ExportarPdfCommand     { get; }

    // Navegación entre períodos: antes la pantalla estaba clavada al mes de arranque.
    public RelayCommand PeriodoAnteriorCommand  { get; }
    public RelayCommand PeriodoSiguienteCommand { get; }
    public RelayCommand PeriodoActualCommand    { get; }

    // Historial — comprobante de pago
    public RelayCommand<HistorialLaboralItem> AbrirComprobanteCommand   { get; }
    public RelayCommand                       GuardarComprobanteCommand { get; }
    public RelayCommand                       CerrarComprobanteCommand  { get; }
    public RelayCommand                       BorrarComprobanteCommand  { get; }

    // Historial — eliminar
    public RelayCommand<HistorialLaboralItem> EliminarHistorialItemCommand { get; }

    public ControlLaboralViewModel(AppDatabase db, int usuarioId, Func<DateTime>? hoy = null)
    {
        _db  = db;
        _uid = usuarioId;
        _hoy = hoy ?? (() => DateTime.Today);

        GuardarConfigCommand   = new RelayCommand(GuardarConfig);
        AgregarRegistroCommand = new RelayCommand(AgregarRegistro);
        EliminarRegistroCommand = new RelayCommand<RegistroDiaLaboral>(r =>
        {
            if (r is null) return;
            _db.EliminarRegistroDia(r.Id);
            Cargar();
        });
        CerrarPeriodoCommand = new RelayCommand(CerrarPeriodo);
        ExportarPdfCommand   = new RelayCommand(ExportarPdf);

        AbrirComprobanteCommand   = new RelayCommand<HistorialLaboralItem>(AbrirComprobante);
        GuardarComprobanteCommand = new RelayCommand(GuardarComprobante);
        CerrarComprobanteCommand  = new RelayCommand(CerrarComprobante);
        BorrarComprobanteCommand  = new RelayCommand(BorrarComprobante);

        EliminarHistorialItemCommand = new RelayCommand<HistorialLaboralItem>(EliminarHistorialItem);

        PeriodoAnteriorCommand  = new RelayCommand(() => IrAlPeriodo(_cfg.PeriodoAnterior(_inicio)));
        PeriodoSiguienteCommand = new RelayCommand(() => IrAlPeriodo(_cfg.PeriodoSiguiente(_fin)));
        PeriodoActualCommand    = new RelayCommand(() => IrAlPeriodo(_cfg.PeriodoQueContiene(_hoy())));

        Cargar();
    }

    /// <summary>
    /// Cambia el período mostrado. Antes no existía: el mes se fijaba al construir la vista, que
    /// se cachea, así que la pantalla seguía mostrando el mes viejo tras pasar la medianoche del
    /// último día — y no había forma de consultar un período anterior.
    /// </summary>
    private void IrAlPeriodo((DateTime Inicio, DateTime Fin) periodo)
    {
        _inicio = periodo.Inicio;
        _fin    = periodo.Fin;
        _siguiendoAlActual = EsPeriodoActual;
        CargarRegistros();
        ActualizarResumen();
        NotificarPeriodo();
    }

    private void NotificarPeriodo()
    {
        OnPropertyChanged(nameof(PeriodoActualLabel));
        OnPropertyChanged(nameof(PeriodoRangoTexto));
        OnPropertyChanged(nameof(EsPeriodoActual));
        // La vista enlaza esta, no EsPeriodoActual: sin notificarla, el botón de volver
        // al período actual nunca aparecía al navegar.
        OnPropertyChanged(nameof(VolverAlActualVisibility));
        OnPropertyChanged(nameof(HorasDiariasTexto));
        OnPropertyChanged(nameof(ValorHoraTexto));
    }

    public void Cargar()
    {
        _cfg = _db.ObtenerConfiguracionLaboral(_uid);
        _salarioMensual = _cfg.SalarioMensual;
        _jornadaSemanal = _cfg.JornadaSemanal;
        _modoPago       = _cfg.ModoPago;
        _diaPago        = _cfg.DiaPago;
        _diaPago2       = _cfg.DiaPago2;
        _diaSemana      = _cfg.DiaSemana;
        _viaticos       = _cfg.Viaticos;

        ConfigSalarioMensual = _salarioMensual.ToString("N2");
        ConfigJornada      = _jornadaSemanal.ToString("G", CultureInfo.InvariantCulture);
        ConfigModoPago     = _modoPago;
        ConfigDiaPago      = _diaPago  > 0 ? _diaPago.ToString()  : "15";
        ConfigDiaPago2     = _diaPago2 > 0 ? _diaPago2.ToString() : "30";
        ConfigDiaSemana    = DiaSemanaToString(_diaSemana);
        ConfigViaticos     = _viaticos.ToString("N0");
        ConfigDiasPorSemana = _cfg.DiasPorSemana.ToString();
        ConfigPorcentaje    = _cfg.PorcentajeDeduccion.ToString("0.##", CultureInfo.InvariantCulture);

        // El período se recalcula en cada carga: la vista se cachea, así que si se fijara sólo
        // al construirla seguiría mostrando el período viejo tras cambiar de quincena.
        if (_siguiendoAlActual || _inicio == default)
            (_inicio, _fin) = _cfg.PeriodoQueContiene(_hoy());

        // Cargar cuentas para el ComboBox de la ventana de ingreso
        Cuentas.Clear();
        foreach (var c in _db.ObtenerCuentas(_uid).Where(c => c.Activa))
            Cuentas.Add(c);

        CargarRegistros();
        CargarHistorial();
        ActualizarResumen();
        ActualizarAlertas();
        NotificarPeriodo();
    }

    private void CargarRegistros()
    {
        Registros.Clear();
        // Del RANGO del período, no del mes natural: ahí estaba el desajuste.
        foreach (var r in _db.ObtenerRegistrosDia(_uid, _inicio, _fin))
            Registros.Add(r);
    }

    private void CargarHistorial()
    {
        Historial.Clear();

        var periodos = _db.ObtenerPeriodosLaborales(_uid);
        var directos = _db.ObtenerIngresosLaboralesDirectos(_uid);
        var anio     = DateTime.Now.Year;

        var items = new List<HistorialLaboralItem>();

        foreach (var p in periodos)
        {
            items.Add(new HistorialLaboralItem
            {
                EsCierre      = true,
                Comprobante   = EstadoComprobante(p),
                Fecha         = p.FechaFin,
                Label         = p.Label,
                Bruto         = p.SalarioBruto,
                Deducciones   = p.Deducciones,
                Monto         = p.SalarioNeto,
                Notas         = p.Notas,
                SourceId      = p.Id,
                TransaccionId = p.TransaccionId
            });
        }

        foreach (var d in directos)
        {
            var label = d.Fecha.ToString("dd/MM/yyyy", CrCulture);
            if (!string.IsNullOrWhiteSpace(d.Descripcion))
                label += $" — {d.Descripcion}";

            items.Add(new HistorialLaboralItem
            {
                EsCierre      = false,
                Fecha         = d.Fecha,
                Label         = label,
                Bruto         = 0,
                Deducciones   = 0,
                Monto         = d.Monto,
                Notas         = d.Descripcion,
                SourceId      = d.Id,
                TransaccionId = d.TransaccionId
            });
        }

        // Ordenar por fecha ascendente para calcular balance acumulado
        items.Sort((a, b) => a.Fecha.CompareTo(b.Fecha));

        decimal acum = 0;
        foreach (var item in items)
        {
            acum += item.Monto;
            item.BalanceAcumulado = acum;
        }

        // Invertir a descendente para mostrar lo más reciente primero
        items.Reverse();

        foreach (var item in items)
            Historial.Add(item);

        var totalAnio = items.Where(i => i.Fecha.Year == anio).Sum(i => i.Monto);
        HistorialTotalAnio = $"₡{totalAnio:N0}";
        OnPropertyChanged(nameof(HistorialTotalAnio));

        SinHistorialVisibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ConHistorialVisibility = items.Count > 0  ? Visibility.Visible : Visibility.Collapsed;
        OnPropertyChanged(nameof(SinHistorialVisibility));
        OnPropertyChanged(nameof(ConHistorialVisibility));
    }

    /// <summary>Para la columna del historial: sin anotar, cuadra, o cuánto difiere el neto.</summary>
    private string EstadoComprobante(PeriodoLaboral p)
    {
        var comp = _db.ObtenerComprobante(p.Id);
        if (comp is null) return "Sin anotar";

        var lineas = LineaComparacion.Comparar(CalculoDe(p), comp);
        if (lineas.All(l => l.Cuadra)) return "✓ Cuadra";

        var neto = lineas.Single(l => l.Concepto == "Monto neto");
        return Math.Abs(neto.Diferencia) < LineaComparacion.Tolerancia
            ? "⚠ Horas distintas"
            : $"⚠ {neto.DiferenciaTexto}";
    }

    private void ActualizarResumen()
    {
        // Un período cerrado se calcula con los parámetros que tenía al cerrarse: si después
        // suben los viáticos o el salario, lo ya pagado no debe cambiar.
        var periodos = _db.ObtenerPeriodosLaborales(_uid);
        var actual   = periodos.FirstOrDefault(p => p.FechaInicio.Date == _inicio.Date);
        var cfg      = actual?.Cerrado == true ? actual.ConfiguracionAlCierre(_cfg) : _cfg;

        if (cfg.SalarioMensual <= 0)
        {
            ResumenSalarioBase   = "Configura el salario";
            ResumenSalarioBruto  = "—";
            ResumenDeduccionCCSS = "—";
            ResumenSalarioNeto   = "—";
            OnPropertyChanged(nameof(ResumenSalarioBase));
            NotifyResumen();
            ActualizarEstadoPeriodo(actual);
            return;
        }

        // El mismo cálculo que el cierre y que la comparación con el comprobante.
        var c = CalculoPeriodo.De(cfg, Registros);

        decimal aguinaldo  = Math.Round(c.Bruto / 12m, 0);
        decimal vacaciones = Math.Round(c.Bruto / 12m * (10m / 22m), 0);

        _salarioBrutoCalc = c.Bruto;
        _deduccionesCalc  = c.Deducciones;
        _salarioNetoCalc  = c.Neto;

        ResumenSalarioBase   = $"₡{c.SalarioFijo:N2}";
        ResumenExtraD        = $"{c.HorasExtra:N1} h  →  ₡{c.MontoExtra:N2}";
        ResumenExtraN        = $"{c.HorasNocturnas:N1} h  →  ₡{c.MontoNocturnas:N2}";
        ResumenDobles        = $"{c.HorasDobles:N1} h  →  ₡{c.MontoDobles:N2}";
        ResumenFeriados      = $"{c.HorasNormales:N1} h  →  ₡{c.MontoNormales:N2}";
        ResumenViaticos      = $"+₡{c.Viaticos:N2}";
        ResumenDescAusencias = $"{c.HorasAusenciaSinGoce:N1} h  →  -₡{c.DescuentoAusencias:N2}";
        ResumenAusenciaGoce  = $"{c.HorasAusenciaConGoce:N1} h";
        ResumenSalarioBruto  = $"₡{c.Bruto:N2}";
        ResumenDeduccionCCSS = $"-₡{c.Deducciones:N2}  ({c.PorcentajeDeduccion:0.##}%)";
        ResumenSalarioNeto   = $"₡{c.Neto:N2}";
        ResumenAguinaldo     = $"₡{aguinaldo:N0}";
        ResumenVacaciones    = $"₡{vacaciones:N0}";
        OnPropertyChanged(nameof(ResumenSalarioBase));

        NotifyResumen();
        ActualizarEstadoPeriodo(actual);
    }

    private void ActualizarEstadoPeriodo(PeriodoLaboral? actual)
    {
        if (actual?.Cerrado == true)
        {
            PeriodoCerradoVisibility = Visibility.Visible;
            PeriodoAbiertVisibility  = Visibility.Collapsed;
            PeriodoCerradoTexto      = $"Período cerrado — Neto registrado: ₡{actual.SalarioNeto:N0}";
        }
        else
        {
            PeriodoCerradoVisibility = Visibility.Collapsed;
            PeriodoAbiertVisibility  = Visibility.Visible;
            PeriodoCerradoTexto      = "";
        }
        OnPropertyChanged(nameof(PeriodoCerradoVisibility));
        OnPropertyChanged(nameof(PeriodoAbiertVisibility));
        OnPropertyChanged(nameof(PeriodoCerradoTexto));
    }

    private void NotifyResumen()
    {
        OnPropertyChanged(nameof(ResumenExtraD));
        OnPropertyChanged(nameof(ResumenExtraN));
        OnPropertyChanged(nameof(ResumenDobles));
        OnPropertyChanged(nameof(ResumenFeriados));
        OnPropertyChanged(nameof(ResumenViaticos));
        OnPropertyChanged(nameof(ResumenDescAusencias));
        OnPropertyChanged(nameof(ResumenAusenciaGoce));
        OnPropertyChanged(nameof(ResumenSalarioBruto));
        OnPropertyChanged(nameof(ResumenDeduccionCCSS));
        OnPropertyChanged(nameof(ResumenSalarioNeto));
        OnPropertyChanged(nameof(ResumenAguinaldo));
        OnPropertyChanged(nameof(ResumenVacaciones));
    }

    private void ActualizarAlertas()
    {
        var hoy     = _hoy();
        int diasRest = DiasHastaPago(hoy);

        if (diasRest >= 0 && diasRest <= 3)
        {
            AlertaPagoVisibility = Visibility.Visible;
            AlertaPagoTexto      = diasRest == 0
                ? "¡Hoy es día de pago!"
                : $"Faltan {diasRest} día{(diasRest == 1 ? "" : "s")} para el próximo pago ({DescripcionPago()})";
        }
        else
        {
            AlertaPagoVisibility = Visibility.Collapsed;
            AlertaPagoTexto      = "";
        }
        OnPropertyChanged(nameof(AlertaPagoVisibility));
        OnPropertyChanged(nameof(AlertaPagoTexto));
        OnPropertyChanged(nameof(PeriodoActualLabel));
    }

    private int DiasHastaPago(DateTime hoy) => _modoPago switch
    {
        "Semanal"   => DiasHastaDiaSemana(hoy),
        "Quincenal" => Math.Min(
            DiasHastaDiaMes(hoy, _diaPago),
            DiasHastaDiaMes(hoy, _diaPago2 > 0 ? _diaPago2 : _diaPago)),
        _ => DiasHastaDiaMes(hoy, _diaPago)
    };

    private static int DiasHastaDiaMes(DateTime hoy, int dia)
    {
        if (dia < 1 || dia > 31) return 99;
        try
        {
            var target = new DateTime(hoy.Year, hoy.Month,
                Math.Min(dia, DateTime.DaysInMonth(hoy.Year, hoy.Month)));
            if (target < hoy)
            {
                var next = hoy.AddMonths(1);
                target = new DateTime(next.Year, next.Month,
                    Math.Min(dia, DateTime.DaysInMonth(next.Year, next.Month)));
            }
            return (target - hoy).Days;
        }
        catch { return 99; }
    }

    private int DiasHastaDiaSemana(DateTime hoy)
    {
        var targetDow = (DayOfWeek)(_diaSemana % 7);
        return ((int)targetDow - (int)hoy.DayOfWeek + 7) % 7;
    }

    private string DescripcionPago() => _modoPago switch
    {
        "Semanal"   => $"día {DiaSemanaToString(_diaSemana)}",
        "Quincenal" => $"días {_diaPago}/{_diaPago2}",
        _           => $"día {_diaPago} del mes"
    };

    private static string DiaSemanaToString(int ds) => ds switch
    {
        1 => "Lunes", 2 => "Martes", 3 => "Miércoles",
        4 => "Jueves", 5 => "Viernes", 6 => "Sábado", _ => "Domingo"
    };

    private static int StringToDiaSemana(string s) => s switch
    {
        "Lunes" => 1, "Martes" => 2, "Miércoles" => 3,
        "Jueves" => 4, "Viernes" => 5, "Sábado" => 6, _ => 7
    };

    // ── Acciones — configuración ──────────────────────────────────────────────

    private void GuardarConfig()
    {
        if (!Dinero.TryParse(_cfgSalarioMensual, out decimal mensual) || mensual < 0)
        {
            ConfigMensaje = "Salario mensual inválido."; ConfigMensajeOk = false; return;
        }
        if (!decimal.TryParse(_cfgJornada.Replace(",", "."), NumberStyles.Any,
                CultureInfo.InvariantCulture, out decimal jornada) || jornada < 1m || jornada > 80m)
        {
            ConfigMensaje = "Jornada semanal inválida (1–80 horas)."; ConfigMensajeOk = false; return;
        }
        // Vacío = sin viáticos. Dinero y no decimal.TryParse: el campo se rellena con separador
        // de miles y "25.000" se habría leído como 25.
        decimal viaticos = 0m;
        if (!string.IsNullOrWhiteSpace(_cfgViaticos) && (!Dinero.TryParse(_cfgViaticos, out viaticos) || viaticos < 0))
        {
            ConfigMensaje = "Viáticos inválidos."; ConfigMensajeOk = false; return;
        }
        if (!int.TryParse(_cfgDiasPorSemana, out int diasPorSemana) || diasPorSemana is < 1 or > 7)
        {
            ConfigMensaje = "Días por semana inválidos (1–7)."; ConfigMensajeOk = false; return;
        }
        if (!decimal.TryParse(_cfgPorcentaje.Replace(",", "."), NumberStyles.Any,
            CultureInfo.InvariantCulture, out decimal porcentaje) || porcentaje is < 0m or > 100m)
        {
            ConfigMensaje = "Porcentaje de deducción inválido (0–100)."; ConfigMensajeOk = false; return;
        }

        int diaPago = 0, diaPago2 = 0, diaSemana = 5;
        switch (_cfgModoPago)
        {
            case "Mensual":
            case "Quincenal":
                if (!int.TryParse(_cfgDiaPago, out diaPago) || diaPago < 1 || diaPago > 31)
                {
                    ConfigMensaje = "Día de pago inválido (1–31)."; ConfigMensajeOk = false; return;
                }
                if (_cfgModoPago == "Quincenal")
                {
                    if (!int.TryParse(_cfgDiaPago2, out diaPago2) || diaPago2 < 1 || diaPago2 > 31)
                    {
                        ConfigMensaje = "Segundo día de pago inválido (1–31)."; ConfigMensajeOk = false; return;
                    }
                }
                break;
            case "Semanal":
                diaSemana = StringToDiaSemana(_cfgDiaSemana);
                break;
        }

        _db.GuardarConfiguracionLaboral(new ConfiguracionLaboral
        {
            UsuarioId      = _uid,
            SalarioMensual = mensual,
            JornadaSemanal = jornada,
            ModoPago       = _cfgModoPago,
            DiaPago        = diaPago,
            DiaPago2       = diaPago2,
            DiaSemana      = diaSemana,
            Viaticos       = viaticos,
            DiasPorSemana       = diasPorSemana,
            PorcentajeDeduccion = porcentaje
        });

        ConfigMensaje   = "Configuración guardada.";
        ConfigMensajeOk = true;
        // Con otro modo de pago o días de corte, el rango que se estaba mirando ya no es un
        // período válido: se vuelve al de hoy.
        _siguiendoAlActual = true;
        Cargar();
    }

    // ── Acciones — registro diario ────────────────────────────────────────────

    private void AgregarRegistro()
    {
        RegError = "";

        if (!DateTime.TryParseExact(_regFecha, "dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime fecha))
        {
            RegError = "Fecha inválida. Use dd/MM/yyyy.";
            return;
        }

        if (!double.TryParse(_regHorasExtraDiurnas.Replace(",", "."),   NumberStyles.Any, CultureInfo.InvariantCulture, out double hed)) hed = 0;
        if (!double.TryParse(_regHorasExtraNocturnas.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double hen)) hen = 0;
        if (!double.TryParse(_regHorasDobles.Replace(",", "."),         NumberStyles.Any, CultureInfo.InvariantCulture, out double hd))  hd  = 0;
        if (!double.TryParse(_regHorasFeriado.Replace(",", "."),        NumberStyles.Any, CultureInfo.InvariantCulture, out double hfe)) hfe = 0;

        // La ausencia se mide en horas. Si se marca sin indicar cuántas, se entiende el día
        // completo, que es lo que significaba el booleano anterior.
        double horasAusencia = 0;
        if (_regEsAusencia)
        {
            if (!double.TryParse(_regHorasAusencia.Replace(",", "."), NumberStyles.Any,
                                 CultureInfo.InvariantCulture, out horasAusencia) || horasAusencia <= 0)
                horasAusencia = (double)_cfg.HorasDiarias;
        }

        var reg = new RegistroDiaLaboral
        {
            UsuarioId           = _uid,
            Fecha               = fecha,
            HorasNormales       = Math.Max(0, hfe),
            HorasExtraDiurnas   = Math.Max(0, hed),
            HorasExtraNocturnas = Math.Max(0, hen),
            HorasDobles         = Math.Max(0, hd),
            EsFeriado           = hfe > 0,
            HorasAusencia       = Math.Max(0, horasAusencia),
            TieneGoceSalario    = _regEsAusencia && _regTieneGoce,
            Viaticos            = 0
        };

        _db.InsertarOActualizarRegistroDia(reg);

        RegFecha               = fecha.AddDays(1).ToString("dd/MM/yyyy");
        RegHorasExtraDiurnas   = "0";
        RegHorasExtraNocturnas = "0";
        RegHorasDobles         = "0";
        RegHorasFeriado        = "0";
        RegEsAusencia          = false;
        RegTieneGoce           = true;

        CargarRegistros();
        ActualizarResumen();
    }

    // ── Acciones — cierre de período ──────────────────────────────────────────

    private void CerrarPeriodo()
    {
        if (_cfg.SalarioMensual <= 0)
        {
            MessageBox.Show("Configura el salario mensual antes de cerrar el período.",
                "Control Laboral", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var res = MessageBox.Show(
            $"¿Cerrar el período {PeriodoActualLabel}?\n\n" +
            $"Se consolidará el resumen con salario neto de {ResumenSalarioNeto}.\n" +
            "No se registrará ningún ingreso automáticamente.",
            "Cerrar período", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (res != MessageBoxResult.Yes) return;

        _db.ConsolidarPeriodoLaboral(_uid, _inicio, _fin);

        CargarHistorial();
        ActualizarResumen();

        MessageBox.Show("Período cerrado. El resumen quedó registrado en el historial.",
            "Control Laboral", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── Acciones — ingreso directo ────────────────────────────────────────────

    public void EjecutarRegistroIngreso(DateTime fecha, decimal monto, string descripcion, int? cuentaId)
    {
        _db.InsertarIngresoLaboralDirecto(_uid, fecha, monto, descripcion, cuentaId);
        // Antes aquí se llamaba a ReiniciarRegistrosDia(), que borraba TODOS los registros
        // diarios del mes sin avisar: registrar un pago quincenal se llevaba por delante las
        // horas extra ya cargadas. Registrar un ingreso no debe destruir la bitácora.
        CargarRegistros();
        CargarHistorial();
        ActualizarResumen();
    }

    // ── Acciones — comprobante de pago ────────────────────────────────────────

    /// <summary>
    /// Lo que debía pagarse en un período cerrado: sus registros con los parámetros congelados
    /// al cerrarlo. Es contra esto que se compara el comprobante.
    /// </summary>
    private CalculoPeriodo CalculoDe(PeriodoLaboral p) =>
        CalculoPeriodo.De(p.ConfiguracionAlCierre(_cfg), _db.ObtenerRegistrosDia(_uid, p.FechaInicio, p.FechaFin));

    private void AbrirComprobante(HistorialLaboralItem? item)
    {
        if (item is null || !item.EsCierre) return;
        var periodo = _db.ObtenerPeriodosLaborales(_uid).FirstOrDefault(p => p.Id == item.SourceId);
        if (periodo is null) return;

        _compTarget  = item;
        _compCalculo = CalculoDe(periodo);

        // Vacío si aún no se anotó. NO se rellena con lo calculado: copiar el número que la app
        // ya espera es la manera de no ver una diferencia.
        var c = _db.ObtenerComprobante(periodo.Id);
        string M(decimal v) => c is null ? "" : v.ToString("N2");
        string H(decimal v) => c is null ? "" : v.ToString("0.##");
        _compSalarioFijo = M(c?.SalarioFijo ?? 0);
        _compHorasNormales = H(c?.HorasNormales ?? 0);
        _compMontoNormales = M(c?.MontoNormales ?? 0);
        _compHorasExtra  = H(c?.HorasExtra ?? 0);
        _compMontoExtra  = M(c?.MontoExtra ?? 0);
        _compHorasNoct   = H(c?.HorasNocturnas ?? 0);
        _compMontoNoct   = M(c?.MontoNocturnas ?? 0);
        _compHorasDobles = H(c?.HorasDobles ?? 0);
        _compMontoDobles = M(c?.MontoDobles ?? 0);
        _compOtros       = M(c?.OtrosAjustes ?? 0);
        _compViaticos    = M(c?.Viaticos ?? 0);
        _compDeducciones = M(c?.Deducciones ?? 0);
        _compNotas       = c?.Notas ?? "";
        foreach (var p in new[] { nameof(CompSalarioFijo), nameof(CompHorasNormales), nameof(CompMontoNormales), nameof(CompHorasExtra), nameof(CompMontoExtra),
                                  nameof(CompHorasNoct), nameof(CompMontoNoct), nameof(CompHorasDobles),
                                  nameof(CompMontoDobles), nameof(CompOtros), nameof(CompViaticos),
                                  nameof(CompDeducciones), nameof(CompNotas), nameof(ComprobanteTitulo) })
            OnPropertyChanged(p);

        CompError          = "";
        ComprobanteVisible = true;
        RecalcularComparacion();
    }

    /// <summary>
    /// Lee los campos. Vacío cuenta como cero —la mayoría de los comprobantes no traen horas
    /// nocturnas—; lo que no se entiende se nombra en <paramref name="invalidos"/>.
    /// </summary>
    private ComprobantePago LeerComprobante(List<string> invalidos)
    {
        decimal Leer(string texto, string concepto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return 0m;
            if (Dinero.TryParse(texto, out var v)) return v;
            invalidos.Add(concepto);
            return 0m;
        }

        return new ComprobantePago
        {
            PeriodoId      = _compTarget?.SourceId ?? 0,
            SalarioFijo    = Leer(_compSalarioFijo, "salario fijo"),
            HorasNormales  = Leer(_compHorasNormales, "horas normales"),
            MontoNormales  = Leer(_compMontoNormales, "monto de horas normales"),
            HorasExtra     = Leer(_compHorasExtra,  "horas extra"),
            MontoExtra     = Leer(_compMontoExtra,  "monto de horas extra"),
            HorasNocturnas = Leer(_compHorasNoct,   "horas nocturnas"),
            MontoNocturnas = Leer(_compMontoNoct,   "monto de horas nocturnas"),
            HorasDobles    = Leer(_compHorasDobles, "horas dobles"),
            MontoDobles    = Leer(_compMontoDobles, "monto de horas dobles"),
            OtrosAjustes   = Leer(_compOtros,       "otros ajustes"),
            Viaticos       = Leer(_compViaticos,    "viáticos"),
            // En el comprobante la C.C.S.S. aparece en negativo; da igual cómo se copie.
            Deducciones    = Math.Abs(Leer(_compDeducciones, "C.C.S.S.")),
            Notas          = _compNotas.Trim()
        };
    }

    private void RecalcularComparacion()
    {
        if (_compCalculo is null) return;
        var invalidos = new List<string>();
        var comp      = LeerComprobante(invalidos);

        // Con todo vacío no hay nada que comparar todavía: sólo se muestra lo calculado.
        bool pendiente = new[] { _compSalarioFijo, _compHorasNormales, _compMontoNormales, _compHorasExtra, _compMontoExtra, _compHorasNoct,
                                 _compMontoNoct, _compHorasDobles, _compMontoDobles, _compOtros,
                                 _compViaticos, _compDeducciones }.All(string.IsNullOrWhiteSpace);

        Comparacion.Clear();
        foreach (var linea in LineaComparacion.Comparar(_compCalculo, comp, pendiente))
            Comparacion.Add(linea);

        CompError = invalidos.Count > 0 ? $"No se entiende: {string.Join(", ", invalidos)}." : "";
    }

    private void GuardarComprobante()
    {
        if (_compTarget is null) return;
        var invalidos = new List<string>();
        var comp      = LeerComprobante(invalidos);
        if (invalidos.Count > 0)
        {
            CompError = $"No se entiende: {string.Join(", ", invalidos)}.";
            return;
        }

        _db.GuardarComprobante(comp);
        CerrarComprobante();
        CargarHistorial();
    }

    private void BorrarComprobante()
    {
        if (_compTarget is null) return;
        _db.EliminarComprobante(_compTarget.SourceId);
        CerrarComprobante();
        CargarHistorial();
    }

    private void CerrarComprobante()
    {
        ComprobanteVisible = false;
        CompError          = "";
        _compTarget        = null;
        _compCalculo       = null;
        Comparacion.Clear();
    }

    // ── Acciones — eliminar historial ─────────────────────────────────────────

    private void EliminarHistorialItem(HistorialLaboralItem? item)
    {
        if (item is null) return;

        var tipo    = item.EsCierre ? "cierre de período" : "ingreso directo";
        var detalle = item.EsCierre
            ? $"Período: {item.Label}\nSalario neto: ₡{item.Monto:N0}"
            : $"Fecha: {item.Label}\nMonto: ₡{item.Monto:N0}";

        var res = MessageBox.Show(
            $"¿Eliminar este {tipo}?\n\n{detalle}\n\n" +
            "Esta acción también eliminará la transacción vinculada.",
            "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (res != MessageBoxResult.Yes) return;

        if (item.EsCierre)
            _db.EliminarPeriodoLaboral(item.SourceId);
        else
            _db.EliminarIngresoLaboralDirecto(item.SourceId);

        // Si estaba abierto su comprobante, cerrarlo: el período (y el comprobante) ya no existen.
        if (_compTarget?.SourceId == item.SourceId && _compTarget?.EsCierre == item.EsCierre)
            CerrarComprobante();

        CargarHistorial();
        ActualizarResumen();
    }

    // ── PDF ───────────────────────────────────────────────────────────────────

    private void ExportarPdf()
    {
        try
        {
            var ruta = PdfLaboralService.Exportar(
                PeriodoActualLabel,
                _cfg.SalarioPorHora,
                _jornadaSemanal,
                [.. Registros],
                _salarioBrutoCalc,
                _deduccionesCalc,
                _salarioNetoCalc);
            MessageBox.Show($"PDF exportado:\n{ruta}",
                "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al exportar PDF:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
