using System.Windows.Controls;
using FinanzasPersonales.Data;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Views;

public partial class CuentasView : UserControl
{
    private readonly CuentasViewModel _vm;

    public CuentasView(AppDatabase db, int usuarioId)
    {
        InitializeComponent();
        _vm = new CuentasViewModel(db, usuarioId);
        DataContext = _vm;
    }

    public void Actualizar() => _vm.Actualizar();
}
