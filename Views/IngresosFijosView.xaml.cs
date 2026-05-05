using System.Windows.Controls;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Views;

public partial class IngresosFijosView : UserControl
{
    public IngresosFijosViewModel ViewModel => (IngresosFijosViewModel)DataContext;

    public IngresosFijosView(IngresosFijosViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    public void Actualizar() => ViewModel.Actualizar();
}
