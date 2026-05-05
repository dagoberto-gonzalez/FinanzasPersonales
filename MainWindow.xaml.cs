using System.Windows;
using System.Windows.Controls;
using FinanzasPersonales.Data;
using FinanzasPersonales.Services;
using FinanzasPersonales.ViewModels;
using FinanzasPersonales.Views;

namespace FinanzasPersonales;

public partial class MainWindow : Window
{
    private readonly AppDatabase _db;
    private readonly int         _uid;

    // ── Vistas cacheadas ──────────────────────────────────────────────────────
    private DashboardView?       _dashView;
    private TransaccionesView?   _transView;
    private MetasView?           _metasView;
    private GastosFijosView?     _gastosFijosView;
    private IngresosFijosView?   _ingresosFijosView;
    private TarjetasView?        _tarjetasView;
    private CuentasView?         _cuentasView;
    private ReportesView?        _reportesView;
    private CambiarPasswordView? _cambiarPwdView;
    private CategoriasView?      _categoriasView;
    private AdminView?           _adminView;
    private ControlLaboralView?  _controlLaboralView;

    public MainWindow(AppDatabase db)
    {
        InitializeComponent();
        _db  = db;
        _uid = SessionService.UsuarioId;

        TxtUsuario.Text = SessionService.NombreUsuario;

        if (SessionService.EsAdmin)
        {
            BadgeAdmin.Visibility = Visibility.Visible;
            BtnAdmin.Visibility   = Visibility.Visible;
        }

        MostrarDashboard();
    }

    // ── Fábricas de vistas (lazy) ─────────────────────────────────────────────

    private DashboardView ObtenerDashboard()
    {
        _dashView ??= new DashboardView(_db, _uid);
        return _dashView;
    }

    private TransaccionesView ObtenerTransacciones()
    {
        _transView ??= new TransaccionesView(_db, _uid);
        return _transView;
    }

    private MetasView ObtenerMetas()
    {
        _metasView ??= new MetasView(_db, _uid);
        return _metasView;
    }

    private GastosFijosView ObtenerGastosFijos()
    {
        if (_gastosFijosView is null)
        {
            var vm = new GastosFijosViewModel(_db, _uid);
            _gastosFijosView = new GastosFijosView(vm);
        }
        return _gastosFijosView;
    }

    private IngresosFijosView ObtenerIngresosFijos()
    {
        if (_ingresosFijosView is null)
        {
            var vm = new IngresosFijosViewModel(_db, _uid);
            _ingresosFijosView = new IngresosFijosView(vm);
        }
        return _ingresosFijosView;
    }

    private TarjetasView ObtenerTarjetas()
    {
        if (_tarjetasView is null)
        {
            var vm = new TarjetasViewModel(_db, _uid);
            _tarjetasView = new TarjetasView(vm);
        }
        return _tarjetasView;
    }

    private CuentasView ObtenerCuentas()
    {
        _cuentasView ??= new CuentasView(_db, _uid);
        return _cuentasView;
    }

    private ReportesView ObtenerReportes()
    {
        if (_reportesView is null)
        {
            var vm = new ReportesViewModel(_db, _uid);
            _reportesView = new ReportesView(vm);
        }
        return _reportesView;
    }

    private CambiarPasswordView ObtenerCambiarPassword()
    {
        if (_cambiarPwdView is null)
        {
            var vm = new CambiarPasswordViewModel(_db);
            _cambiarPwdView = new CambiarPasswordView(vm);
        }
        return _cambiarPwdView;
    }

    private CategoriasView ObtenerCategorias()
    {
        _categoriasView ??= new CategoriasView(_db, _uid);
        return _categoriasView;
    }

    private AdminView ObtenerAdmin()
    {
        _adminView ??= new AdminView(_db);
        return _adminView;
    }

    private ControlLaboralView ObtenerControlLaboral()
    {
        _controlLaboralView ??= new ControlLaboralView(_db, _uid);
        return _controlLaboralView;
    }

    // ── Navegación ────────────────────────────────────────────────────────────

    private void ExpandirGrupo(Expander grupo) => grupo.IsExpanded = true;

    private void DesactivarTodos()
    {
        BtnDashboard.IsEnabled      = true;
        BtnTransacciones.IsEnabled  = true;
        BtnMetas.IsEnabled          = true;
        BtnCategorias.IsEnabled     = true;
        BtnGastosFijos.IsEnabled    = true;
        BtnIngresosFijos.IsEnabled  = true;
        BtnTarjetas.IsEnabled       = true;
        BtnCuentas.IsEnabled        = true;
        BtnReportes.IsEnabled       = true;
        BtnCambiarPwd.IsEnabled     = true;
        BtnAdmin.IsEnabled          = true;
        BtnControlLaboral.IsEnabled = true;
    }

    private void MostrarDashboard()
    {
        var v = ObtenerDashboard();
        v.Actualizar();
        MainContent.Content = v;
        DesactivarTodos();
        BtnDashboard.IsEnabled = false;
        ExpandirGrupo(ExpGeneral);
    }

    private void MostrarTransacciones()
    {
        var v = ObtenerTransacciones();
        v.Actualizar();
        MainContent.Content = v;
        DesactivarTodos();
        BtnTransacciones.IsEnabled = false;
        ExpandirGrupo(ExpGeneral);
    }

    private void MostrarMetas()
    {
        var v = ObtenerMetas();
        v.Actualizar();
        MainContent.Content = v;
        DesactivarTodos();
        BtnMetas.IsEnabled = false;
        ExpandirGrupo(ExpGeneral);
    }

    private void MostrarGastosFijos()
    {
        var v = ObtenerGastosFijos();
        v.Actualizar();
        MainContent.Content = v;
        DesactivarTodos();
        BtnGastosFijos.IsEnabled = false;
        ExpandirGrupo(ExpRecurrentes);
    }

    private void MostrarIngresosFijos()
    {
        var v = ObtenerIngresosFijos();
        v.Actualizar();
        MainContent.Content = v;
        DesactivarTodos();
        BtnIngresosFijos.IsEnabled = false;
        ExpandirGrupo(ExpRecurrentes);
    }

    private void MostrarTarjetas()
    {
        var v = ObtenerTarjetas();
        v.Actualizar();
        MainContent.Content = v;
        DesactivarTodos();
        BtnTarjetas.IsEnabled = false;
        ExpandirGrupo(ExpFinanzas);
    }

    private void MostrarCuentas()
    {
        var v = ObtenerCuentas();
        v.Actualizar();
        MainContent.Content = v;
        DesactivarTodos();
        BtnCuentas.IsEnabled = false;
        ExpandirGrupo(ExpFinanzas);
    }

    private void MostrarReportes()
    {
        var v = ObtenerReportes();
        v.Actualizar();
        MainContent.Content = v;
        DesactivarTodos();
        BtnReportes.IsEnabled = false;
        ExpandirGrupo(ExpFinanzas);
    }

    private void MostrarCambiarPassword()
    {
        var v = ObtenerCambiarPassword();
        MainContent.Content = v;
        DesactivarTodos();
        BtnCambiarPwd.IsEnabled = false;
        ExpandirGrupo(ExpCuenta);
    }

    private void MostrarCategorias()
    {
        var v = ObtenerCategorias();
        v.Actualizar();
        MainContent.Content = v;
        DesactivarTodos();
        BtnCategorias.IsEnabled = false;
        ExpandirGrupo(ExpGeneral);
    }

    private void MostrarAdmin()
    {
        if (!SessionService.EsAdmin) return;
        var v = ObtenerAdmin();
        v.Actualizar();
        MainContent.Content = v;
        DesactivarTodos();
        BtnAdmin.IsEnabled = false;
        ExpandirGrupo(ExpCuenta);
    }

    private void MostrarControlLaboral()
    {
        var v = ObtenerControlLaboral();
        v.Actualizar();
        MainContent.Content = v;
        DesactivarTodos();
        BtnControlLaboral.IsEnabled = false;
        ExpandirGrupo(ExpLaboral);
    }

    private void CerrarSesion()
    {
        SessionService.CerrarSesion();
        Close();
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void BtnDashboard_Click(object s, RoutedEventArgs e)      => MostrarDashboard();
    private void BtnTransacciones_Click(object s, RoutedEventArgs e)  => MostrarTransacciones();
    private void BtnMetas_Click(object s, RoutedEventArgs e)          => MostrarMetas();
    private void BtnCategorias_Click(object s, RoutedEventArgs e)     => MostrarCategorias();
    private void BtnGastosFijos_Click(object s, RoutedEventArgs e)    => MostrarGastosFijos();
    private void BtnIngresosFijos_Click(object s, RoutedEventArgs e)  => MostrarIngresosFijos();
    private void BtnTarjetas_Click(object s, RoutedEventArgs e)       => MostrarTarjetas();
    private void BtnCuentas_Click(object s, RoutedEventArgs e)        => MostrarCuentas();
    private void BtnReportes_Click(object s, RoutedEventArgs e)       => MostrarReportes();
    private void BtnCambiarPwd_Click(object s, RoutedEventArgs e)     => MostrarCambiarPassword();
    private void BtnAdmin_Click(object s, RoutedEventArgs e)           => MostrarAdmin();
    private void BtnControlLaboral_Click(object s, RoutedEventArgs e) => MostrarControlLaboral();
    private void BtnCerrarSesion_Click(object s, RoutedEventArgs e)   => CerrarSesion();
}
