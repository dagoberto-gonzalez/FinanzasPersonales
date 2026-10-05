using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;

namespace FinanzasPersonales.ViewModels;

public class ControlLaboralViewModel : BaseViewModel
{
    private const double TasaCCSS = 0.1067;
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
    private decimal _salXHora;
    private decimal _jornadaSemanal;
    private string  _modoPago  = "Mensual";
    private int     _diaPago;
    private int     _diaPago2;
    private int     _diaSemana = 5;
    private decimal _viaticos;

    // ── Configuración — propiedades de UI ────────────────────────────────────
    private string _cfgSalarioXHora = "";
    private string _cfgJornada      = "48";
    private string _cfgModoPago     = "Mensual";
    private string _cfgDiaPago      = "15";
    private string _cfgDiaPago2     = "30";
    private string _cfgDiaSemana    = "Viernes";
    private string _cfgViaticos     = "0";
    private string _cfgMensaje      = "";
    private bool   _cfgMensajeOk;

    public string ConfigSalarioXHora
    {
        get => _cfgSalarioXHora;
        set { _cfgSalarioXHora = value; OnPropertyChanged(); }
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
    /// Muestra a cuánto sale la hora diaria con lo configurado. Es el número del que dependen
    /// el valor de un feriado y el de una ausencia, y antes salía de un 6 invisible.
    /// </summary>
    public string HorasDiariasTexto =>
        _cfg.HorasDiarias > 0
            ? $"Un día son {_cfg.HorasDiarias:0.##} h · es lo que vale un feriado o una ausencia completa"
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
    public string     ConfigMensajeColor      => _cfgMensajeOk ? "#A6E3A1" : "#F38BA8";
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
    private bool   _regEsFeriado;
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
    public bool RegEsFeriado
    {
        get => _regEsFeriado;
        set { _regEsFeriado = value; OnPropertyChanged(); }
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
    public string ResumenFeriados      { get; private set; } = "0 días  →  ₡0";
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

    // ── Formulario: Editar cierre ─────────────────────────────────────────────
    private bool             _editFormVisible;
    private HistorialLaboralItem? _editTarget;
    private string           _editBruto       = "";
    private string           _editDeducciones = "";
    private string           _editNeto        = "";
    private string           _editNotas       = "";
    private string           _editError       = "";

    public bool EditFormVisible
    {
        get => _editFormVisible;
        set { _editFormVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(EditFormVisibility)); }
    }
    public Visibility EditFormVisibility =>
        _editFormVisible ? Visibility.Visible : Visibility.Collapsed;

    public string EditBruto
    {
        get => _editBruto;
        set { _editBruto = value; OnPropertyChanged(); }
    }
    public string EditDeducciones
    {
        get => _editDeducciones;
        set { _editDeducciones = value; OnPropertyChanged(); }
    }
    public string EditNeto
    {
        get => _editNeto;
        set { _editNeto = value; OnPropertyChanged(); }
    }
    public string EditNotas
    {
        get => _editNotas;
        set { _editNotas = value; OnPropertyChanged(); }
    }
    public string EditError
    {
        get => _editError;
        set { _editError = value; OnPropertyChanged(); OnPropertyChanged(nameof(EditErrorVisibility)); }
    }
    public Visibility EditErrorVisibility =>
        string.IsNullOrEmpty(_editError) ? Visibility.Collapsed : Visibility.Visible;

    public string EditTituloLabel =>
        _editTarget is not null ? $"Editando: {_editTarget.Label}" : "Editar cierre";

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

    // Historial — editar cierre
    public RelayCommand<HistorialLaboralItem> EditarCierreCommand   { get; }
    public RelayCommand                       GuardarEdicionCommand  { get; }
    public RelayCommand                       CancelarEdicionCommand { get; }

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

        EditarCierreCommand   = new RelayCommand<HistorialLaboralItem>(AbrirEdicion);
        GuardarEdicionCommand  = new RelayCommand(GuardarEdicion);
        CancelarEdicionCommand = new RelayCommand(() =>
        {
            EditFormVisible = false;
            EditError = "";
            _editTarget = null;
        });

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
    }

    public void Cargar()
    {
        _cfg = _db.ObtenerConfiguracionLaboral(_uid);
        _salXHora       = _cfg.SalarioPorHora;
        _jornadaSemanal = _cfg.JornadaSemanal;
        _modoPago       = _cfg.ModoPago;
        _diaPago        = _cfg.DiaPago;
        _diaPago2       = _cfg.DiaPago2;
        _diaSemana      = _cfg.DiaSemana;
        _viaticos       = _cfg.Viaticos;

        ConfigSalarioXHora = _salXHora.ToString("F0");
        ConfigJornada      = _jornadaSemanal.ToString("G", CultureInfo.InvariantCulture);
        ConfigModoPago     = _modoPago;
        ConfigDiaPago      = _diaPago  > 0 ? _diaPago.ToString()  : "15";
        ConfigDiaPago2     = _diaPago2 > 0 ? _diaPago2.ToString() : "30";
        ConfigDiaSemana    = DiaSemanaToString(_diaSemana);
        ConfigViaticos     = _viaticos.ToString("F0");
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

    private void ActualizarResumen()
    {
        // La base se calcula sobre los MISMOS días que se suman abajo. Antes era una constante
        // por modo de pago —en quincenal, la base de quince días— mientras las horas extra se
        // sumaban del mes entero.
        decimal periodoBase = _cfg.SalarioBaseDe(_inicio, _fin);
        ResumenSalarioBase = _salXHora > 0 ? $"₡{periodoBase:N0}" : "Configura el salario";
        OnPropertyChanged(nameof(ResumenSalarioBase));

        if (_salXHora <= 0)
        {
            ResumenSalarioBruto  = "—";
            ResumenDeduccionCCSS = "—";
            ResumenSalarioNeto   = "—";
            NotifyResumen();
            return;
        }

        // Sale de los días que se trabajan, no de un 6 clavado: con semana de 5 días el valor
        // de la hora diaria salía un 20 % bajo, y con él cada feriado y cada ausencia.
        decimal horasDiarias = _cfg.HorasDiarias;

        double totalExtraD  = Registros.Sum(r => r.HorasExtraDiurnas);
        double totalExtraN  = Registros.Sum(r => r.HorasExtraNocturnas);
        double totalDobles  = Registros.Sum(r => r.HorasDobles);
        int    feriadosDias = Registros.Count(r => r.EsFeriado && !r.EsAusencia);

        // La ausencia se mide en horas: faltar tres horas no se podía registrar.
        double horasSinGoce = Registros.Where(r => !r.TieneGoceSalario).Sum(r => r.HorasAusencia);
        double horasConGoce = Registros.Where(r =>  r.TieneGoceSalario).Sum(r => r.HorasAusencia);

        decimal montoExtraD   = Math.Round((decimal)totalExtraD * _salXHora * 1.5m,  0);
        decimal montoExtraN   = Math.Round((decimal)totalExtraN * _salXHora * 1.75m, 0);
        decimal montoDobles   = Math.Round((decimal)totalDobles * _salXHora * 2m,    0);
        decimal montoFeriados = Math.Round(feriadosDias * horasDiarias * _salXHora, 0);
        decimal descAusencias = Math.Round((decimal)horasSinGoce * _salXHora, 0);

        decimal salarioBruto = periodoBase
            + montoExtraD + montoExtraN + montoDobles
            + montoFeriados + _viaticos
            - descAusencias;
        if (salarioBruto < 0) salarioBruto = 0;

        // Porcentaje configurable: estaba clavado como 10,67 % y cambia por ley.
        decimal deduccionCCSS = Math.Round(salarioBruto * _cfg.PorcentajeDeduccion / 100m, 0);
        decimal salarioNeto   = salarioBruto - deduccionCCSS;

        decimal aguinaldo  = Math.Round(salarioBruto / 12m, 0);
        decimal vacaciones = Math.Round(salarioBruto / 12m * (10m / 22m), 0);

        _salarioBrutoCalc = salarioBruto;
        _deduccionesCalc  = deduccionCCSS;
        _salarioNetoCalc  = salarioNeto;

        ResumenExtraD        = $"{totalExtraD:N1} h  →  ₡{montoExtraD:N0}";
        ResumenExtraN        = $"{totalExtraN:N1} h  →  ₡{montoExtraN:N0}";
        ResumenDobles        = $"{totalDobles:N1} h  →  ₡{montoDobles:N0}";
        ResumenFeriados      = $"{feriadosDias} día{(feriadosDias == 1 ? "" : "s")}  →  ₡{montoFeriados:N0}";
        ResumenViaticos      = $"₡{_viaticos:N0}";
        ResumenDescAusencias = $"{horasSinGoce:N1} h  →  -₡{descAusencias:N0}";
        ResumenAusenciaGoce  = $"{horasConGoce:N1} h";
        ResumenSalarioBruto  = $"₡{salarioBruto:N0}";
        ResumenDeduccionCCSS = $"-₡{deduccionCCSS:N0}  ({_cfg.PorcentajeDeduccion:0.##}%)";
        ResumenSalarioNeto   = $"₡{salarioNeto:N0}";
        ResumenAguinaldo     = $"₡{aguinaldo:N0}";
        ResumenVacaciones    = $"₡{vacaciones:N0}";

        NotifyResumen();

        var periodos = _db.ObtenerPeriodosLaborales(_uid);
        var actual   = periodos.FirstOrDefault(p => p.FechaInicio.Date == _inicio.Date);
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
        if (!decimal.TryParse(_cfgSalarioXHora.Replace(",", "."), NumberStyles.Any,
            CultureInfo.InvariantCulture, out decimal sph) || sph < 0)
        {
            ConfigMensaje = "Salario por hora inválido."; ConfigMensajeOk = false; return;
        }
        if (!decimal.TryParse(_cfgJornada.Replace(",", "."), NumberStyles.Any,
                CultureInfo.InvariantCulture, out decimal jornada) || jornada < 1m || jornada > 80m)
        {
            ConfigMensaje = "Jornada semanal inválida (1–80 horas)."; ConfigMensajeOk = false; return;
        }
        if (!decimal.TryParse(_cfgViaticos.Replace(",", "."), NumberStyles.Any,
            CultureInfo.InvariantCulture, out decimal viaticos) || viaticos < 0)
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
            SalarioPorHora = sph,
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
            HorasNormales       = 0,
            HorasExtraDiurnas   = Math.Max(0, hed),
            HorasExtraNocturnas = Math.Max(0, hen),
            HorasDobles         = Math.Max(0, hd),
            EsFeriado           = _regEsFeriado,
            HorasAusencia       = Math.Max(0, horasAusencia),
            TieneGoceSalario    = _regEsAusencia && _regTieneGoce,
            Viaticos            = 0
        };

        _db.InsertarOActualizarRegistroDia(reg);

        RegFecha               = fecha.AddDays(1).ToString("dd/MM/yyyy");
        RegHorasExtraDiurnas   = "0";
        RegHorasExtraNocturnas = "0";
        RegHorasDobles         = "0";
        RegEsFeriado           = false;
        RegEsAusencia          = false;
        RegTieneGoce           = true;

        CargarRegistros();
        ActualizarResumen();
    }

    // ── Acciones — cierre de período ──────────────────────────────────────────

    private void CerrarPeriodo()
    {
        if (_salXHora <= 0)
        {
            MessageBox.Show("Configura el salario por hora antes de cerrar el período.",
                "Control Laboral", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var res = MessageBox.Show(
            $"¿Cerrar el período {PeriodoActualLabel}?\n\n" +
            $"Se consolidará el resumen con salario neto de {ResumenSalarioNeto}.\n" +
            "No se registrará ningún ingreso automáticamente.",
            "Cerrar período", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (res != MessageBoxResult.Yes) return;

        _db.ConsolidarPeriodoLaboral(_uid, _inicio, _fin,
            _salarioBrutoCalc, _deduccionesCalc, _salarioNetoCalc);

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

    // ── Acciones — editar cierre ──────────────────────────────────────────────

    private void AbrirEdicion(HistorialLaboralItem? item)
    {
        if (item is null || !item.EsCierre) return;
        _editTarget    = item;
        EditBruto      = item.Bruto.ToString("F0");
        EditDeducciones = item.Deducciones.ToString("F0");
        EditNeto       = item.Monto.ToString("F0");
        EditNotas      = item.Notas;
        EditError      = "";
        EditFormVisible = true;
        OnPropertyChanged(nameof(EditTituloLabel));
    }

    private void GuardarEdicion()
    {
        EditError = "";
        if (_editTarget is null) return;

        if (!decimal.TryParse(_editBruto.Replace(",", "."), NumberStyles.Any,
            CultureInfo.InvariantCulture, out decimal bruto) || bruto < 0)
        {
            EditError = "Ingreso bruto inválido."; return;
        }
        if (!decimal.TryParse(_editDeducciones.Replace(",", "."), NumberStyles.Any,
            CultureInfo.InvariantCulture, out decimal ded) || ded < 0)
        {
            EditError = "Deducciones inválidas."; return;
        }
        if (!decimal.TryParse(_editNeto.Replace(",", "."), NumberStyles.Any,
            CultureInfo.InvariantCulture, out decimal neto) || neto < 0)
        {
            EditError = "Salario neto inválido."; return;
        }

        _db.ActualizarPeriodoLaboral(_editTarget.SourceId, bruto, ded, neto, _editNotas.Trim());

        EditFormVisible = false;
        _editTarget     = null;
        EditError       = "";

        CargarHistorial();
        ActualizarResumen();
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

        // Si se estaba editando el mismo item, cerrar el form
        if (_editTarget?.SourceId == item.SourceId && _editTarget?.EsCierre == item.EsCierre)
        {
            EditFormVisible = false;
            _editTarget     = null;
        }

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
                _salXHora,
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
