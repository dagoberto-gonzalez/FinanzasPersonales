using System.Windows;
using System.Windows.Controls;
using FinanzasPersonales.Data;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Views;

public partial class TransaccionesView : UserControl
{
    private readonly TransaccionesViewModel _vm;

    public TransaccionesView(AppDatabase db, int usuarioId)
    {
        InitializeComponent();
        _vm = new TransaccionesViewModel(db, usuarioId);
        DataContext = _vm;
    }

    public void Actualizar() => _vm.Actualizar();

    private void BtnLimpiar_Click(object sender, RoutedEventArgs e) =>
        _vm.LimpiarFiltros();
}
