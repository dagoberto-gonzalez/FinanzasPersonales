using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Windows;

public partial class RegistroWindow : Window
{
    private readonly RegistroViewModel _vm;

    public RegistroWindow(AppDatabase db)
    {
        InitializeComponent();
        _vm = new RegistroViewModel(db);
        DataContext = _vm;

        _vm.RegistroExitoso += () =>
        {
            MessageBox.Show(
                "Cuenta creada exitosamente. Ahora puedes iniciar sesión.",
                "Registro exitoso",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
            Close();
        };
    }

    private void PwdPassword_Changed(object sender, RoutedEventArgs e)
        => _vm.Password = PwdPassword.Password;

    private void PwdConfirmar_Changed(object sender, RoutedEventArgs e)
        => _vm.Confirmar = PwdConfirmar.Password;

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _vm.RegistrarCommand.Execute(null);
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
