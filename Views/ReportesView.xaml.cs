using System.Windows.Controls;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Views;

public partial class ReportesView : UserControl
{
    public ReportesViewModel ViewModel => (ReportesViewModel)DataContext;

    public ReportesView(ReportesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    public void Actualizar() => ViewModel.Actualizar();
}
