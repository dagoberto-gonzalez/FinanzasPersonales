using System.Windows.Controls;
using FinanzasPersonales.Data;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Views;

public partial class MetasView : UserControl
{
    private readonly MetasViewModel _vm;

    public MetasView(AppDatabase db, int usuarioId)
    {
        InitializeComponent();
        _vm = new MetasViewModel(db, usuarioId);
        DataContext = _vm;
    }

    public void Actualizar() => _vm.Actualizar();
}
