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
    private int _anio;
    private int _mes;

    public string PeriodoActualLabel =>
        new DateTime(_anio, _mes, 1).ToString("MMMM yyyy", CrCulture);

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

    // Historial — editar cierre
    public RelayCommand<HistorialLaboralItem> EditarCierreCommand   { get; }
    public RelayCommand                       GuardarEdicionCommand  { get; }
    public RelayCommand                       CancelarEdicionCommand { get; }

    // Historial — eliminar
    public RelayCommand<HistorialLaboralItem> EliminarHistorialItemCommand { get; }

    public ControlLaboralViewModel(AppDatabase db, int usuarioId)
    {
        _db  = db;
        _uid = usuarioId;

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

        var ahora = DateTime.Now;
        _anio = ahora.Year;
        _mes  = ahora.Month;

        Cargar();
    }

    public void Cargar()
    {
        var cfg = _db.ObtenerConfiguracionLaboral(_uid);
        _salXHora       = cfg.SalarioPorHora;
        _jornadaSemanal = cfg.JornadaSemanal;
        _modoPago       = cfg.ModoPago;
        _diaPago        = cfg.DiaPago;
        _diaPago2       = cfg.DiaPago2;
        _diaSemana      = cfg.DiaSemana;
        _viaticos       = cfg.Viaticos;

        ConfigSalarioXHora = _salXHora.ToString("F0");
        ConfigJornada      = _jornadaSemanal.ToString("G", CultureInfo.InvariantCulture);
        ConfigModoPago     = _modoPago;
        ConfigDiaPago      = _diaPago  > 0 ? _diaPago.ToString()  : "15";
        ConfigDiaPago2     = _diaPago2 > 0 ? _diaPago2.ToString() : "30";
        ConfigDiaSemana    = DiaSemanaToString(_diaSemana);
        ConfigViaticos     = _viaticos.ToString("F0");

        // Cargar cuentas para el ComboBox de la ventana de ingreso
        Cuentas.Clear();
        foreach (var c in _db.ObtenerCuentas(_uid).Where(c => c.Activa))
            Cuentas.Add(c);

        CargarRegistros();
        CargarHistorial();
        ActualizarResumen();
        ActualizarAlertas();
    }

    private void CargarRegistros()
    {
        Registros.Clear();
        foreach (var r in _db.ObtenerRegistrosDia(_uid, _anio, _mes))
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
                Fecha         = new DateTime(p.Anio, p.Mes, DateTime.DaysInMonth(p.Anio, p.Mes)),
                Label         = p.MesLabel,
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
        decimal periodoBase = 0;
        if (_salXHora > 0)
        {
            periodoBase = _modoPago switch
            {
                "Semanal"   => Math.Round(_salXHora * _jornadaSemanal, 0),
                "Quincenal" => Math.Round(_salXHora * _jornadaSemanal * 52m / 24m, 0),
                _           => Math.Round(_salXHora * _jornadaSemanal * 52m / 12m, 0)
            };
        }
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

        decimal horasDiarias = _jornadaSemanal / 6m;

        double totalExtraD     = Registros.Sum(r => r.HorasExtraDiurnas);
        double totalExtraN     = Registros.Sum(r => r.HorasExtraNocturnas);
        double totalDobles     = Registros.Sum(r => r.HorasDobles);
        int    feriadosDias    = Registros.Count(r => r.EsFeriado && !r.EsAusencia);
        int    ausenciaSinGoce = Registros.Count(r => r.EsAusencia && !r.TieneGoceSalario);
        int    ausenciaGoce    = Registros.Count(r => r.EsAusencia && r.TieneGoceSalario);

        decimal montoExtraD   = Math.Round((decimal)totalExtraD * _salXHora * 1.5m,  0);
        decimal montoExtraN   = Math.Round((decimal)totalExtraN * _salXHora * 1.75m, 0);
        decimal montoDobles   = Math.Round((decimal)totalDobles * _salXHora * 2m,    0);
        decimal montoFeriados = Math.Round(feriadosDias * horasDiarias * _salXHora, 0);
        decimal descAusencias = Math.Round(ausenciaSinGoce * horasDiarias * _salXHora, 0);

        decimal salarioBruto = periodoBase
            + montoExtraD + montoExtraN + montoDobles
            + montoFeriados + _viaticos
            - descAusencias;
        if (salarioBruto < 0) salarioBruto = 0;

        decimal deduccionCCSS = Math.Round(salarioBruto * (decimal)TasaCCSS, 0);
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
        ResumenDescAusencias = $"{ausenciaSinGoce} día{(ausenciaSinGoce == 1 ? "" : "s")}  →  -₡{descAusencias:N0}";
        ResumenAusenciaGoce  = $"{ausenciaGoce} día{(ausenciaGoce == 1 ? "" : "s")}";
        ResumenSalarioBruto  = $"₡{salarioBruto:N0}";
        ResumenDeduccionCCSS = $"-₡{deduccionCCSS:N0}  (10.67%)";
        ResumenSalarioNeto   = $"₡{salarioNeto:N0}";
        ResumenAguinaldo     = $"₡{aguinaldo:N0}";
        ResumenVacaciones    = $"₡{vacaciones:N0}";

        NotifyResumen();

        var periodos = _db.ObtenerPeriodosLaborales(_uid);
        var actual   = periodos.FirstOrDefault(p => p.Anio == _anio && p.Mes == _mes);
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
        var hoy     = DateTime.Today;
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
            Viaticos       = viaticos
        });

        ConfigMensaje   = "Configuración guardada.";
        ConfigMensajeOk = true;
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

        var reg = new RegistroDiaLaboral
        {
            UsuarioId           = _uid,
            Fecha               = fecha,
            HorasNormales       = 0,
            HorasExtraDiurnas   = Math.Max(0, hed),
            HorasExtraNocturnas = Math.Max(0, hen),
            HorasDobles         = Math.Max(0, hd),
            EsFeriado           = _regEsFeriado,
            EsAusencia          = _regEsAusencia,
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

        _db.ConsolidarPeriodoLaboral(_uid, _anio, _mes,
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
