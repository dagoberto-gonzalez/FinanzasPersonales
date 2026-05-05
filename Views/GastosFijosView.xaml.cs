using System.Windows.Controls;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Views;

public partial class GastosFijosView : UserControl
{
    public GastosFijosViewModel ViewModel => (GastosFijosViewModel)DataContext;

    public GastosFijosView(GastosFijosViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    public void Actualizar() => ViewModel.Actualizar();
}
