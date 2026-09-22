using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;

namespace FinanzasPersonales.ViewModels;

public class SubItem
{
    public string Nombre       { get; }
    public bool   EsEncabezado { get; }

    public SubItem(string nombre, bool esEncabezado = false)
    {
        Nombre       = nombre;
        EsEncabezado = esEncabezado;
    }

    public override string ToString() => Nombre;
}

public class TransaccionesViewModel : BaseViewModel
{
    private readonly AppDatabase _db;

    // ── Formulario ────────────────────────────────────────────────────────────
    private bool     _esIngreso   = false;
    private string   _monto       = string.Empty;
    private string   _categoria   = string.Empty;
    private string   _descripcion = string.Empty;
    private string   _notas       = string.Empty;
    private DateTime _fecha       = DateTime.Today;
    private string   _metodoPago  = "Efectivo";
    private SubItem? _selectedSubItem;
    private string   _error       = string.Empty;

    public bool EsIngreso
    {
        get => _esIngreso;
        set
        {
            _esIngreso = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EsGasto));
            OnPropertyChanged(nameof(EsTarjeta));
            RefrescarMetodosPago();
        }
    }

    public bool EsGasto
    {
        get => !_esIngreso;
        set
        {
            _esIngreso = !value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EsIngreso));
            OnPropertyChanged(nameof(EsTarjeta));
            RefrescarMetodosPago();
        }
    }

    private const string MetodoTarjeta = "Tarjeta";

    /// <summary>
    /// "Tarjeta" sólo tiene sentido en un gasto: una tarjeta de crédito no origina ingresos.
    /// Antes la opción estaba siempre disponible y el ingreso resultante desaparecía de todos
    /// los totales sin avisar.
    /// <para>
    /// Se quita y se pone esa única entrada en lugar de reconstruir la lista. Vaciarla dejaba
    /// el ComboBox sin selección: WPF anula el SelectedItem al quedarse sin elementos y no
    /// vuelve a evaluar el binding cuando se repuebla, así que el campo quedaba en blanco
    /// aunque el ViewModel dijera "Efectivo".
    /// </para>
    /// </summary>
    private void RefrescarMetodosPago()
    {
        if (_esIngreso)
        {
            if (!MetodosPago.Contains(MetodoTarjeta)) return;

            // El orden es lo que importa: primero se mueve la selección, después se quita el
            // elemento. Si se quita el elemento que está seleccionado, el ComboBox anula su
            // selección por su cuenta y ya no vuelve a leer el binding — el ViewModel queda
            // en "Efectivo" y el campo se ve en blanco.
            if (_metodoPago == MetodoTarjeta) MetodoPago = "Efectivo";
            MetodosPago.Remove(MetodoTarjeta);
        }
        else if (!MetodosPago.Contains(MetodoTarjeta))
        {
            MetodosPago.Add(MetodoTarjeta);
        }
    }

    public string Monto
    {
        get => _monto;
        set { _monto = value; OnPropertyChanged(); }
    }
    public string Categoria
    {
        get => _categoria;
        set { _categoria = value; OnPropertyChanged(); }
    }
    public string Descripcion
    {
        get => _descripcion;
        set { _descripcion = value; OnPropertyChanged(); }
    }
    public string Notas
    {
        get => _notas;
        set { _notas = value; OnPropertyChanged(); }
    }
    public DateTime Fecha
    {
        get => _fecha;
        set { _fecha = value; OnPropertyChanged(); }
    }

    /// <summary>Método de pago: Efectivo | Transferencia | SINPE Móvil | Tarjeta</summary>
    public string MetodoPago
    {
        get => _metodoPago;
        set
        {
            // Al reconstruir MetodosPago, WPF empuja null en el SelectedItem mientras la
            // colección está vacía. Sin esta guarda el método quedaría en null.
            _metodoPago = string.IsNullOrEmpty(value) ? "Efectivo" : value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EsTarjeta));
            OnPropertyChanged(nameof(MostrarSubItems));
            CargarSubItems();
        }
    }

    /// <summary>Ítem seleccionado en el segundo dropdown (cuenta o tarjeta)</summary>
    public SubItem? SelectedSubItem
    {
        get => _selectedSubItem;
        set { _selectedSubItem = value; OnPropertyChanged(); }
    }

    public bool EsTarjeta     => EsGasto && _metodoPago == "Tarjeta";
    public bool MostrarSubItems => _metodoPago != "Efectivo";

    public string MensajeError
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeErrorVisibility)); }
    }
    public Visibility MensajeErrorVisibility =>
        string.IsNullOrEmpty(_error) ? Visibility.Collapsed : Visibility.Visible;

    // ── Búsqueda ──────────────────────────────────────────────────────────────
    private string _busqueda = string.Empty;
    public string BusquedaTexto
    {
        get => _busqueda;
        set { _busqueda = value; OnPropertyChanged(); }
    }

    // ── Filtros ───────────────────────────────────────────────────────────────
    private DateTime? _filtroDesde;
    private DateTime? _filtroHasta;
    private string    _filtroCategoria = "Todas";

    public DateTime? FiltroDesde
    {
        get => _filtroDesde;
        set { _filtroDesde = value; OnPropertyChanged(); }
    }
    public DateTime? FiltroHasta
    {
        get => _filtroHasta;
        set { _filtroHasta = value; OnPropertyChanged(); }
    }
    public string FiltroCategoria
    {
        get => _filtroCategoria;
        set { _filtroCategoria = value; OnPropertyChanged(); }
    }

    // ── Colecciones ───────────────────────────────────────────────────────────
    public ObservableCollection<Transaccion> Transacciones    { get; } = [];
    public ObservableCollection<string>      CategoriasForm   { get; } = [];
    public ObservableCollection<string>      CategoriasFiltro { get; } = [];
    public ObservableCollection<SubItem> SubItems { get; } = [];

    /// <summary>Se reconstruye según el tipo: ver <c>RefrescarMetodosPago</c>.</summary>
    public ObservableCollection<string> MetodosPago { get; } =
        ["Efectivo", "Transferencia", "SINPE Móvil", "Tarjeta"];

    // ── Comandos ──────────────────────────────────────────────────────────────
    public ICommand GuardarCommand  { get; }
    public ICommand EliminarCommand { get; }
    public ICommand FiltrarCommand  { get; }
    public ICommand LimpiarCommand  { get; }

    private readonly int _uid;

    public TransaccionesViewModel(AppDatabase db, int usuarioId)
    {
        _db             = db;
        _uid            = usuarioId;
        GuardarCommand  = new RelayCommand(Guardar);
        EliminarCommand = new RelayCommand<Transaccion>(Eliminar);
        FiltrarCommand  = new RelayCommand(CargarTransacciones);
        LimpiarCommand  = new RelayCommand(LimpiarFiltros);
        CargarCategorias();
        CargarSubItems();
        CargarTransacciones();
    }

    public void Actualizar()
    {
        CargarCategorias();
        CargarSubItems();
        CargarTransacciones();
    }

    private void CargarSubItems()
    {
        SubItems.Clear();

        if (_metodoPago == "Efectivo")
        {
            SelectedSubItem = null;
            return;
        }

        if (_metodoPago == "Tarjeta")
        {
            var tarjetas = _db.ObtenerTarjetas(_uid);
            var cuentas  = _db.ObtenerCuentas(_uid).Where(c => c.Activa).ToList();

            SubItems.Add(new SubItem("── Tarjetas de Crédito ──", esEncabezado: true));
            if (tarjetas.Count == 0)
                SubItems.Add(new SubItem("No hay tarjetas registradas"));
            else
                foreach (var t in tarjetas)
                    SubItems.Add(new SubItem(t.Nombre));

            SubItems.Add(new SubItem("── Cuentas ──", esEncabezado: true));
            if (cuentas.Count == 0)
                SubItems.Add(new SubItem("No hay cuentas registradas"));
            else
                foreach (var c in cuentas)
                    SubItems.Add(new SubItem(c.Nombre));
        }
        else // Transferencia | SINPE Móvil
        {
            var cuentas = _db.ObtenerCuentas(_uid).Where(c => c.Activa).ToList();
            if (cuentas.Count == 0)
                SubItems.Add(new SubItem("No hay cuentas registradas"));
            else
                foreach (var c in cuentas)
                    SubItems.Add(new SubItem(c.Nombre));
        }

        SelectedSubItem = SubItems.FirstOrDefault(i => !i.EsEncabezado);
    }

    private void CargarCategorias()
    {
        var cats = _db.ObtenerCategorias(_uid);

        CategoriasForm.Clear();
        foreach (var c in cats) CategoriasForm.Add(c);
        if (string.IsNullOrEmpty(_categoria) || !cats.Contains(_categoria))
            Categoria = cats.FirstOrDefault() ?? string.Empty;

        CategoriasFiltro.Clear();
        CategoriasFiltro.Add("Todas");
        foreach (var c in cats) CategoriasFiltro.Add(c);
    }

    private void Guardar()
    {
        MensajeError = string.Empty;

        if (!decimal.TryParse(_monto.Replace(",", "."),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var montoDecimal)
            || montoDecimal <= 0)
        {
            MensajeError = "Ingresa un monto válido mayor a 0.";
            return;
        }

        // Resolver cuenta, tarjetaId y cuentaId desde MetodoPago/SelectedSubItem
        string cuenta    = _metodoPago;
        int?   tarjetaId = null;
        int?   cuentaId  = null;
        bool   haySubItem = _selectedSubItem is { EsEncabezado: false } s
                            && s.Nombre is not ("No hay tarjetas registradas" or "No hay cuentas registradas");

        if (haySubItem)
        {
            cuenta = _selectedSubItem!.Nombre;
            // La red de seguridad: aunque la UI ya oculta "Tarjeta" en los ingresos, nunca se
            // resuelve una tarjeta para un ingreso (AppDatabase también lo rechaza).
            if (_metodoPago == "Tarjeta" && !_esIngreso)
            {
                // Intentar como tarjeta de crédito primero
                var tarjetas = _db.ObtenerTarjetas(_uid);
                tarjetaId = tarjetas.FirstOrDefault(t => t.Nombre == cuenta)?.Id;
                // Si no es tarjeta, es una cuenta bancaria
                if (!tarjetaId.HasValue)
                {
                    var cuentas = _db.ObtenerCuentas(_uid);
                    cuentaId = cuentas.FirstOrDefault(c => c.Nombre == cuenta)?.Id;
                }
            }
            else // Transferencia, SINPE Móvil
            {
                var cuentas = _db.ObtenerCuentas(_uid);
                cuentaId = cuentas.FirstOrDefault(c => c.Nombre == cuenta)?.Id;
            }
        }

        _db.InsertarTransaccion(new Transaccion
        {
            Tipo             = _esIngreso ? "Ingreso" : "Gasto",
            Monto            = montoDecimal,
            Categoria        = _categoria,
            Descripcion      = _descripcion.Trim(),
            Notas            = _notas.Trim(),
            Fecha            = _fecha,
            CuentaNombre     = cuenta,
            TarjetaCreditoId = tarjetaId,
            CuentaId         = cuentaId
        }, _uid);

        Monto       = string.Empty;
        Descripcion = string.Empty;
        Notas       = string.Empty;
        Fecha       = DateTime.Today;
        MetodoPago  = "Efectivo";

        CargarTransacciones();
    }

    private void Eliminar(Transaccion? t)
    {
        if (t is null) return;
        var res = System.Windows.MessageBox.Show(
            $"¿Eliminar la transacción \"{t.EtiquetaDisplay}\" por ₡{t.Monto:N0}?",
            "Confirmar eliminación",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (res != System.Windows.MessageBoxResult.Yes) return;
        _db.EliminarTransaccion(t.Id);
        CargarTransacciones();
    }

    public void LimpiarFiltros()
    {
        FiltroDesde     = null;
        FiltroHasta     = null;
        FiltroCategoria = "Todas";
        BusquedaTexto   = string.Empty;
        CargarTransacciones();
    }

    private void CargarTransacciones()
    {
        var lista = _db.ObtenerTransacciones(_uid).AsEnumerable();

        if (_filtroDesde.HasValue)
            lista = lista.Where(t => t.Fecha >= _filtroDesde.Value);

        if (_filtroHasta.HasValue)
            lista = lista.Where(t => t.Fecha <= _filtroHasta.Value);

        if (!string.IsNullOrEmpty(_filtroCategoria) && _filtroCategoria != "Todas")
            lista = lista.Where(t => t.Categoria == _filtroCategoria);

        if (!string.IsNullOrWhiteSpace(_busqueda))
        {
            var q = _busqueda.Trim().ToLowerInvariant();
            lista = lista.Where(t =>
                t.Descripcion.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Categoria.Contains(q, StringComparison.OrdinalIgnoreCase)   ||
                t.CuentaNombre.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Notas.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        Transacciones.Clear();
        foreach (var t in lista)
            Transacciones.Add(t);
    }
}
