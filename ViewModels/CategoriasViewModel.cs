using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;

namespace FinanzasPersonales.ViewModels;

// ── Wrapper por fila ──────────────────────────────────────────────────────────

/// <summary>
/// Una fila de la lista de categorías, con su propio estado de edición.
/// <para>
/// Antes la edición vivía en un panel al final de la pantalla: con catorce categorías quedaba
/// muy por debajo del pliegue y no había forma de saber que existía. Editar ocurre ahora en la
/// misma fila donde se pulsa.
/// </para>
/// </summary>
public class CategoriaVm : BaseViewModel
{
    private readonly CategoriasViewModel _parent;

    public Categoria Datos { get; }

    public int    Id       => Datos.Id;
    public string Nombre   => Datos.Nombre;
    public bool   EsGlobal => Datos.EsGlobal;

    /// <summary>Las de sistema las escribe la aplicación por nombre: no se tocan.</summary>
    public bool EsDeSistema => AppDatabase.EsCategoriaSistema(Datos.Nombre);

    /// <summary>Un usuario normal no edita las globales; nadie edita las de sistema.</summary>
    public bool PuedeEditarse => !EsDeSistema && (_parent.EsAdmin || !EsGlobal);

    public Visibility AccionesVisibility =>
        PuedeEditarse ? Visibility.Visible : Visibility.Collapsed;

    private bool _enEdicion;
    public bool EnEdicion
    {
        get => _enEdicion;
        set
        {
            _enEdicion = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VistaVisibility));
            OnPropertyChanged(nameof(EdicionVisibility));
        }
    }

    public Visibility VistaVisibility   => _enEdicion ? Visibility.Collapsed : Visibility.Visible;
    public Visibility EdicionVisibility => _enEdicion ? Visibility.Visible   : Visibility.Collapsed;

    private string _nombreEditado = string.Empty;
    public string NombreEditado
    {
        get => _nombreEditado;
        set { _nombreEditado = value; OnPropertyChanged(); }
    }

    private string _error = string.Empty;
    public string MensajeError
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeErrorVisibility)); }
    }
    public Visibility MensajeErrorVisibility =>
        string.IsNullOrEmpty(_error) ? Visibility.Collapsed : Visibility.Visible;

    public ICommand EditarCommand   { get; }
    public ICommand GuardarCommand  { get; }
    public ICommand CancelarCommand { get; }
    public ICommand EliminarCommand { get; }

    public CategoriaVm(Categoria datos, CategoriasViewModel parent)
    {
        Datos   = datos;
        _parent = parent;

        EditarCommand   = new RelayCommand(() => _parent.Editar(this));
        GuardarCommand  = new RelayCommand(() => _parent.GuardarEdicion(this));
        CancelarCommand = new RelayCommand(Cancelar);
        EliminarCommand = new RelayCommand(() => _parent.Eliminar(this));
    }

    public void Empezar()
    {
        NombreEditado = Nombre;
        MensajeError  = string.Empty;
        EnEdicion     = true;
    }

    public void Cancelar()
    {
        EnEdicion    = false;
        MensajeError = string.Empty;
    }
}

// ── ViewModel principal ───────────────────────────────────────────────────────

public class CategoriasViewModel : BaseViewModel
{
    private readonly AppDatabase _db;
    private readonly int         _uid;

    internal bool EsAdmin { get; }

    // ── Formulario nueva ─────────────────────────────────────────────────────
    private string _nuevaCategoria = string.Empty;
    private bool   _nuevaEsGlobal;

    public string NuevaCategoria
    {
        get => _nuevaCategoria;
        set { _nuevaCategoria = value; OnPropertyChanged(); }
    }

    public bool NuevaEsGlobal
    {
        get => _nuevaEsGlobal;
        set { _nuevaEsGlobal = value; OnPropertyChanged(); }
    }

    public Visibility CheckGlobalVisibility =>
        EsAdmin ? Visibility.Visible : Visibility.Collapsed;

    // ── Mensajes generales (altas y bajas; los de edición van en cada fila) ──
    private string _error = string.Empty;
    private string _ok    = string.Empty;

    public string MensajeError
    {
        get => _error;
        set
        {
            _error = value;
            if (!string.IsNullOrEmpty(value)) _ok = string.Empty;
            NotificarMensajes();
        }
    }

    /// <summary>Sin esto, una operación correcta no se distinguía de un botón que no responde.</summary>
    public string MensajeOk
    {
        get => _ok;
        set
        {
            _ok = value;
            if (!string.IsNullOrEmpty(value)) _error = string.Empty;
            NotificarMensajes();
        }
    }

    private void NotificarMensajes()
    {
        OnPropertyChanged(nameof(MensajeError));
        OnPropertyChanged(nameof(MensajeErrorVisibility));
        OnPropertyChanged(nameof(MensajeOk));
        OnPropertyChanged(nameof(MensajeOkVisibility));
    }

    public Visibility MensajeErrorVisibility =>
        string.IsNullOrEmpty(_error) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility MensajeOkVisibility =>
        string.IsNullOrEmpty(_ok) ? Visibility.Collapsed : Visibility.Visible;

    // ── Datos ─────────────────────────────────────────────────────────────────
    public ObservableCollection<CategoriaVm> Categorias { get; } = [];

    public ICommand AgregarCommand { get; }

    public CategoriasViewModel(AppDatabase db, int usuarioId)
    {
        _db            = db;
        _uid           = usuarioId;
        EsAdmin        = SessionService.EsAdmin;
        AgregarCommand = new RelayCommand(Agregar);
        Cargar();
    }

    public void Actualizar() => Cargar();

    private void Cargar()
    {
        Categorias.Clear();
        foreach (var c in _db.ObtenerCategoriasDetalle(_uid))
            Categorias.Add(new CategoriaVm(c, this));
    }

    // ── Alta ──────────────────────────────────────────────────────────────────

    private void Agregar()
    {
        MensajeError = string.Empty;

        if (string.IsNullOrWhiteSpace(_nuevaCategoria))
        {
            MensajeError = "Escribe un nombre de categoría.";
            return;
        }

        var nombre = _nuevaCategoria.Trim();
        if (Categorias.Any(c => c.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase)))
        {
            MensajeError = "Esa categoría ya existe.";
            return;
        }

        _db.InsertarCategoria(nombre, _uid, EsAdmin && _nuevaEsGlobal);
        NuevaCategoria = string.Empty;
        NuevaEsGlobal  = false;
        Cargar();
        MensajeOk = $"Categoría «{nombre}» creada.";
    }

    // ── Edición, en la propia fila ────────────────────────────────────────────

    /// <summary>Sólo una fila en edición a la vez: dos cajas abiertas confunden más que ayudan.</summary>
    internal void Editar(CategoriaVm fila)
    {
        foreach (var otra in Categorias.Where(c => c != fila))
            otra.Cancelar();

        MensajeError = string.Empty;
        MensajeOk    = string.Empty;
        fila.Empezar();
    }

    internal void GuardarEdicion(CategoriaVm fila)
    {
        fila.MensajeError = string.Empty;

        var nombre = (fila.NombreEditado ?? string.Empty).Trim();
        if (nombre.Length == 0)
        {
            fila.MensajeError = "El nombre no puede estar vacío.";
            return;
        }

        if (nombre == fila.Nombre)
        {
            fila.Cancelar();
            return;
        }

        // Excluyendo la fila que se edita: si no, cambiar sólo las mayúsculas
        // («mascotas» → «Mascotas») se rechazaba como si ya existiera.
        if (Categorias.Any(c => c.Id != fila.Id &&
                                c.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase)))
        {
            fila.MensajeError = "Ya existe otra categoría con ese nombre.";
            return;
        }

        var anterior = fila.Nombre;
        _db.ActualizarCategoria(fila.Id, nombre, _uid, EsAdmin);
        Cargar();
        MensajeOk = $"«{anterior}» ahora se llama «{nombre}». Las transacciones anteriores también.";
    }

    // ── Baja ──────────────────────────────────────────────────────────────────

    internal void Eliminar(CategoriaVm fila)
    {
        MensajeError = string.Empty;
        MensajeOk    = string.Empty;

        if (fila.EsDeSistema)
        {
            MensajeError = $"«{fila.Nombre}» es una categoría del sistema: la aplicación la usa " +
                           "para las transacciones automáticas y no se puede eliminar.";
            return;
        }

        if (!EsAdmin && fila.EsGlobal)
        {
            MensajeError = "No puedes eliminar categorías globales.";
            return;
        }

        // Las transacciones guardan el NOMBRE: borrar una en uso las dejaría con una etiqueta
        // que ya no aparece en ningún filtro.
        var enUso = _db.ContarTransaccionesEnCategoria(fila.Nombre, _uid);
        if (enUso > 0)
        {
            MensajeError = $"«{fila.Nombre}» se usa en {enUso} transacción{(enUso == 1 ? "" : "es")}. " +
                           "Reasígnalas a otra categoría antes de eliminarla.";
            return;
        }

        var res = MessageBox.Show(
            $"¿Eliminar la categoría \"{fila.Nombre}\"?",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            _db.EliminarCategoria(fila.Id, _uid, EsAdmin);
            Cargar();
            MensajeOk = $"Categoría «{fila.Nombre}» eliminada.";
        }
        catch (InvalidOperationException ex)
        {
            MensajeError = ex.Message;
        }
    }
}
