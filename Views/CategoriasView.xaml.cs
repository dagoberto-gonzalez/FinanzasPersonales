using System.Windows.Controls;
using FinanzasPersonales.Data;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Views;

public partial class CategoriasView : UserControl
{
    private readonly CategoriasViewModel _vm;

    public CategoriasView(AppDatabase db, int usuarioId)
    {
        InitializeComponent();
        _vm = new CategoriasViewModel(db, usuarioId);
        DataContext = _vm;
    }

    public void Actualizar() => _vm.Actualizar();
}
