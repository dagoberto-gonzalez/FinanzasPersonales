using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;

namespace FinanzasPersonales.ViewModels;

public class CategoriasViewModel : BaseViewModel
{
    private readonly AppDatabase _db;
    private readonly int         _uid;
    private readonly bool        _esAdmin;

    // ── Formulario nueva ─────────────────────────────────────────────────────
    private string _nuevaCategoria = string.Empty;
    private bool   _nuevaEsGlobal  = false;
    private string _error          = string.Empty;

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
        _esAdmin ? Visibility.Visible : Visibility.Collapsed;

    public string MensajeError
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(MensajeErrorVisibility)); }
    }
    public Visibility MensajeErrorVisibility =>
        string.IsNullOrEmpty(_error) ? Visibility.Collapsed : Visibility.Visible;

    // ── Edición inline ────────────────────────────────────────────────────────
    private Categoria? _seleccionada;
    private string     _editNombre = string.Empty;

    public Categoria? Seleccionada
    {
        get => _seleccionada;
        set
        {
            _seleccionada = value;
            _editNombre   = value?.Nombre ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EditNombre));
            OnPropertyChanged(nameof(HaySeleccion));
            OnPropertyChanged(nameof(PuedeEditar));
        }
    }
    public string EditNombre
    {
        get => _editNombre;
        set { _editNombre = value; OnPropertyChanged(); }
    }
    public bool HaySeleccion => _seleccionada is not null;

    /// <summary>Puede editar si es admin, o si la categoría es propia (no global).</summary>
    public bool PuedeEditar => _seleccionada is not null && (_esAdmin || !_seleccionada.EsGlobal);

    // ── Datos ─────────────────────────────────────────────────────────────────
    public ObservableCollection<Categoria> Categorias { get; } = [];

    // ── Comandos ──────────────────────────────────────────────────────────────
    public ICommand AgregarCommand     { get; }
    public ICommand EliminarCommand    { get; }
    public ICommand GuardarEditCommand { get; }
    public ICommand SeleccionarCommand { get; }

    public CategoriasViewModel(AppDatabase db, int usuarioId)
    {
        _db               = db;
        _uid              = usuarioId;
        _esAdmin          = SessionService.EsAdmin;
        AgregarCommand    = new RelayCommand(Agregar);
        EliminarCommand   = new RelayCommand<Categoria>(Eliminar);
        GuardarEditCommand = new RelayCommand(GuardarEdit);
        SeleccionarCommand = new RelayCommand<Categoria>(c => Seleccionada = c);
        Cargar();
    }

    public void Actualizar() => Cargar();

    private void Cargar()
    {
        var prevId = _seleccionada?.Id;
        Categorias.Clear();
        foreach (var c in _db.ObtenerCategoriasDetalle(_uid))
            Categorias.Add(c);
        Seleccionada = Categorias.FirstOrDefault(c => c.Id == prevId);
    }

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

        bool esGlobal = _esAdmin && _nuevaEsGlobal;
        _db.InsertarCategoria(nombre, _uid, esGlobal);
        NuevaCategoria = string.Empty;
        NuevaEsGlobal  = false;
        Cargar();
    }

    private void Eliminar(Categoria? cat)
    {
        if (cat is null) return;
        if (AppDatabase.EsCategoriaSistema(cat.Nombre))
        {
            MensajeError = $"«{cat.Nombre}» es una categoría del sistema: la aplicación la usa para las transacciones automáticas y no se puede eliminar.";
            return;
        }
        if (!_esAdmin && cat.EsGlobal)
        {
            MensajeError = "No puedes eliminar categorías globales.";
            return;
        }
        var res = System.Windows.MessageBox.Show(
            $"¿Eliminar la categoría \"{cat.Nombre}\"?",
            "Confirmar eliminación",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (res != System.Windows.MessageBoxResult.Yes) return;
        _db.EliminarCategoria(cat.Id, _uid, _esAdmin);
        if (_seleccionada?.Id == cat.Id) Seleccionada = null;
        Cargar();
    }

    private void GuardarEdit()
    {
        if (_seleccionada is null) return;
        if (AppDatabase.EsCategoriaSistema(_seleccionada.Nombre))
        {
            MensajeError = $"«{_seleccionada.Nombre}» es una categoría del sistema y no se puede renombrar.";
            return;
        }
        if (!_esAdmin && _seleccionada.EsGlobal)
        {
            MensajeError = "No puedes editar categorías globales.";
            return;
        }
        MensajeError = string.Empty;
        if (string.IsNullOrWhiteSpace(_editNombre))
        {
            MensajeError = "El nombre no puede estar vacío.";
            return;
        }
        var nombre = _editNombre.Trim();
        if (nombre == _seleccionada.Nombre) return;
        if (Categorias.Any(c => c.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase)))
        {
            MensajeError = "Esa categoría ya existe.";
            return;
        }
        _db.ActualizarCategoria(_seleccionada.Id, nombre, _uid, _esAdmin);
        Seleccionada = null;
        Cargar();
    }
}
