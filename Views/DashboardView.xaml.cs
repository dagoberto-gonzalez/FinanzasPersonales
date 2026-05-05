using System.Windows.Controls;
using FinanzasPersonales.Data;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Views;

public partial class DashboardView : UserControl
{
    private readonly DashboardViewModel _vm;

    public DashboardView(AppDatabase db, int usuarioId)
    {
        InitializeComponent();
        _vm = new DashboardViewModel(db, usuarioId);
        DataContext = _vm;
    }

    public void Actualizar() => _vm.Cargar();
}
