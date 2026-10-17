using System.Collections.ObjectModel;
using FinanzasPersonales.Services;
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
            SincronizarCategoriasForm();
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
            SincronizarCategoriasForm();
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
        set { _categoria = value ?? string.Empty; OnPropertyChanged(); }
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

    // ── Edición ───────────────────────────────────────────────────────────────
    // Antes una transacción equivocada sólo se podía borrar y volver a crear. El formulario de
    // alta sirve para editar: se rellena con la transacción y Guardar la actualiza.
    private Transaccion? _editando;

    public bool       EstaEditando        => _editando is not null;
    public Visibility EditandoVisibility  => EstaEditando ? Visibility.Visible : Visibility.Collapsed;
    public string     TextoGuardar        => EstaEditando ? "Guardar cambios" : "Guardar";
    public string     TituloEdicion       => _editando is null
        ? ""
        : $"Editando: {_editando.EtiquetaDisplay} · {_editando.Fecha:dd/MM/yyyy}";

    private void NotificarEdicion()
    {
        OnPropertyChanged(nameof(EstaEditando));
        OnPropertyChanged(nameof(EditandoVisibility));
        OnPropertyChanged(nameof(TextoGuardar));
        OnPropertyChanged(nameof(TituloEdicion));
    }

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
    public ICommand EditarCommand   { get; }
    public ICommand CancelarEdicionCommand { get; }
    public ICommand FiltrarCommand  { get; }
    public ICommand LimpiarCommand  { get; }

    private readonly int _uid;

    public TransaccionesViewModel(AppDatabase db, int usuarioId)
    {
        _db             = db;
        _uid            = usuarioId;
        GuardarCommand  = new RelayCommand(Guardar);
        EliminarCommand = new RelayCommand<Transaccion>(Eliminar);
        EditarCommand   = new RelayCommand<Transaccion>(Editar);
        CancelarEdicionCommand = new RelayCommand(LimpiarFormulario);
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

    // Todas las categorías del usuario, en el orden de la base. El formulario muestra las que
    // sirven para el tipo elegido; el filtro, todas.
    private List<Models.Categoria> _categorias = [];

    private void CargarCategorias()
    {
        _categorias = _db.ObtenerCategoriasDetalle(_uid);

        SincronizarCategoriasForm();

        CategoriasFiltro.Clear();
        CategoriasFiltro.Add("Todas");
        foreach (var c in _categorias) CategoriasFiltro.Add(c.Nombre);
    }

    /// <summary>
    /// Deja en el formulario sólo las categorías del tipo elegido (Gasto o Ingreso) y las que
    /// sirven para ambos. Antes un ingreso ofrecía "Alimentación", "Transporte"…
    /// <para>
    /// «Tarjeta» nunca está: marca los pagos de tarjeta, que se registran en la pantalla
    /// Tarjetas para que descuenten del saldo. Registrar uno aquí lo contaría como un gasto nuevo
    /// además de la compra. Sí está en el filtro, para consultar los pagos hechos.
    /// </para>
    /// <para>
    /// La lista se ajusta en el sitio y en este orden —añadir, mover la selección, quitar— para
    /// no quitar NUNCA el elemento seleccionado. Es la misma trampa del método de pago: si el
    /// ComboBox pierde su elemento seleccionado anula la selección y deja de leer el binding, y
    /// el campo se queda en blanco aunque el ViewModel tenga un valor.
    /// </para>
    /// </summary>
    private void SincronizarCategoriasForm()
    {
        var tipo     = _esIngreso ? Models.Categoria.TipoIngreso : Models.Categoria.TipoGasto;
        var objetivo = _categorias
            .Where(c => !AppDatabase.EsCategoriaAutomatica(c.Nombre) && c.SirvePara(tipo))
            .Select(c => c.Nombre)
            .ToList();
        var orden = _categorias.Select((c, i) => (c.Nombre, i))
                               .GroupBy(x => x.Nombre).ToDictionary(g => g.Key, g => g.First().i);
        int Pos(string n) => orden.TryGetValue(n, out var p) ? p : int.MaxValue;

        // 1. Añadir las que faltan, cada una en su sitio.
        foreach (var nombre in objetivo.Where(n => !CategoriasForm.Contains(n)))
            CategoriasForm.Insert(CategoriasForm.Count(x => Pos(x) < Pos(nombre)), nombre);

        // 2. Si la elegida no vale para este tipo, elegir otra que ya esté en la lista.
        if (!objetivo.Contains(_categoria))
            Categoria = objetivo.FirstOrDefault() ?? string.Empty;

        // 3. Quitar las que sobran. La seleccionada ya no está entre ellas.
        foreach (var sobra in CategoriasForm.Where(n => !objetivo.Contains(n)).ToList())
            CategoriasForm.Remove(sobra);
    }

    private void Guardar()
    {
        MensajeError = string.Empty;

        // Dinero y no decimal.TryParse: al editar, el monto llega como "15.000", que el parseo
        // anterior leía como 15. Y quien escribe "1.500,50" o "1500.50" quiere decir lo mismo.
        if (!Dinero.TryParsePositivo(_monto, out var montoDecimal))
        {
            MensajeError = "Ingresa un monto válido mayor a 0.";
            return;
        }

        // Puede quedar vacía si ninguna categoría sirve para este tipo de movimiento.
        if (string.IsNullOrWhiteSpace(_categoria))
        {
            MensajeError = $"Elige una categoría. Si no hay ninguna de {(_esIngreso ? "ingresos" : "gastos")}, " +
                           "créala en Categorías.";
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

        var t = new Transaccion
        {
            Id               = _editando?.Id ?? 0,
            Tipo             = _esIngreso ? "Ingreso" : "Gasto",
            Monto            = montoDecimal,
            Categoria        = _categoria,
            Descripcion      = _descripcion.Trim(),
            Notas            = _notas.Trim(),
            Fecha            = _fecha,
            CuentaNombre     = cuenta,
            TarjetaCreditoId = tarjetaId,
            CuentaId         = cuentaId
        };

        try
        {
            if (_editando is null) _db.InsertarTransaccion(t, _uid);
            else                   _db.ActualizarTransaccion(t, _uid);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Las reglas viven en AppDatabase; aquí sólo se muestran.
            MensajeError = ex.Message;
            return;
        }

        LimpiarFormulario();
        CargarTransacciones();
    }

    /// <summary>Vuelve el formulario a "nueva transacción", vacío.</summary>
    private void LimpiarFormulario()
    {
        _editando    = null;
        NotificarEdicion();
        MensajeError = string.Empty;
        Monto        = string.Empty;
        Descripcion  = string.Empty;
        Notas        = string.Empty;
        Fecha        = DateTime.Today;
        EsIngreso    = false;
        MetodoPago   = "Efectivo";
    }

    /// <summary>
    /// Carga una transacción en el formulario para editarla. El orden importa: primero el tipo
    /// (decide si "Tarjeta" está en la lista de métodos), luego el método (llena la lista de
    /// cuentas o tarjetas) y por último la cuenta o tarjeta concreta.
    /// </summary>
    private void Editar(Transaccion? t)
    {
        if (t is null) return;

        if (_db.MotivoNoEditable(t.Id) is { } motivo)
        {
            MessageBox.Show(motivo, "No se edita aquí", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        EsIngreso = t.Tipo == "Ingreso";

        // No se guarda si fue SINPE o transferencia, sólo la cuenta: para el saldo da igual.
        string? subItem = null;
        if (t.TarjetaCreditoId is { } tarjetaId)
        {
            MetodoPago = MetodoTarjeta;
            subItem    = _db.ObtenerTarjetas(_uid).FirstOrDefault(x => x.Id == tarjetaId)?.Nombre;
        }
        else if (t.CuentaId is { } cuentaId)
        {
            MetodoPago = "Transferencia";
            subItem    = _db.ObtenerCuentas(_uid).FirstOrDefault(x => x.Id == cuentaId)?.Nombre;
        }
        else
        {
            MetodoPago = "Efectivo";
        }

        if (subItem is not null)
        {
            // Una cuenta desactivada no está en la lista, pero la transacción sigue siendo suya.
            if (!SubItems.Any(i => !i.EsEncabezado && i.Nombre == subItem))
                SubItems.Add(new SubItem(subItem));
            SelectedSubItem = SubItems.First(i => !i.EsEncabezado && i.Nombre == subItem);
        }

        // Una categoría borrada después de usarse tampoco está en la lista.
        if (!CategoriasForm.Contains(t.Categoria)) CategoriasForm.Add(t.Categoria);
        Categoria   = t.Categoria;

        Monto       = t.Monto.ToString("N2");
        Descripcion = t.Descripcion;
        Notas       = t.Notas;
        Fecha       = t.Fecha;

        MensajeError = string.Empty;
        _editando    = t;
        NotificarEdicion();
    }

    private void Eliminar(Transaccion? t)
    {
        if (t is null) return;
        var res = System.Windows.MessageBox.Show(
            $"¿Eliminar la transacción \"{t.EtiquetaDisplay}\" por ₡{t.Monto:N2}?" +
            (t.EsTransferencia ? "\n\nEs una transferencia: se borran la salida y la entrada." : ""),
            "Confirmar eliminación",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (res != System.Windows.MessageBoxResult.Yes) return;
        _db.EliminarTransaccion(t.Id);
        // Si era la que se estaba editando, el formulario ya no tiene qué guardar.
        if (_editando?.Id == t.Id) LimpiarFormulario();
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
