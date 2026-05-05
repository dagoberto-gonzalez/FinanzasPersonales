using System.Windows.Controls;
using FinanzasPersonales.Data;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Views;

public partial class AdminView : UserControl
{
    private readonly AdminViewModel _vm;

    public AdminView(AppDatabase db)
    {
        InitializeComponent();
        _vm = new AdminViewModel(db);
        DataContext = _vm;
    }

    public void Actualizar() => _vm.Actualizar();
}
