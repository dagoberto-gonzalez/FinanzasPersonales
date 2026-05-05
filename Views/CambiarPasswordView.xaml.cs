using System.Windows;
using System.Windows.Controls;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Views;

public partial class CambiarPasswordView : UserControl
{
    private CambiarPasswordViewModel _vm = null!;

    public CambiarPasswordView(CambiarPasswordViewModel vm)
    {
        InitializeComponent();
        _vm         = vm;
        DataContext = vm;
    }

    private void PwdActual_Changed(object sender, RoutedEventArgs e)
        => _vm.PasswordActual = PwdActual.Password;

    private void PwdNuevo_Changed(object sender, RoutedEventArgs e)
        => _vm.PasswordNuevo = PwdNuevo.Password;

    private void PwdConfirm_Changed(object sender, RoutedEventArgs e)
        => _vm.PasswordConfirm = PwdConfirm.Password;
}
