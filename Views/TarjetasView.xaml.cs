using System.Windows.Controls;
using System.Windows.Input;
using FinanzasPersonales.Models;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Views;

public partial class TarjetasView : UserControl
{
    public TarjetasViewModel ViewModel => (TarjetasViewModel)DataContext;

    public TarjetasView(TarjetasViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    public void Actualizar() => ViewModel.Actualizar();

    // Deseleccionar al hacer clic en el ítem ya seleccionado.
    // Usa PreviewMouseLeftButtonDown (tunneling) para capturar el evento
    // antes de que ListBoxItem.OnMouseLeftButtonDown lo marque como Handled.
    private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && item.IsSelected)
        {
            ViewModel.Seleccionada = null;
            e.Handled = true;
        }
    }
}
