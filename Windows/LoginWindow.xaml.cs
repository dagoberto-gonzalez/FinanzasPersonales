using System.Windows;
using System.Windows.Input;
using FinanzasPersonales.Data;
using FinanzasPersonales.ViewModels;

namespace FinanzasPersonales.Windows;

public partial class LoginWindow : Window
{
    private readonly AppDatabase    _db;
    private readonly LoginViewModel _vm;

    public LoginWindow(AppDatabase db)
    {
        InitializeComponent();
        _db = db;
        _vm = new LoginViewModel(db);
        DataContext = _vm;

        _vm.LoginExitoso += () =>
        {
            DialogResult = true;
            Close();
        };
    }

    private void PwdPassword_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.Password = PwdPassword.Password;

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _vm.IniciarSesionCommand.Execute(null);
    }

    private void BtnCrearCuenta_Click(object sender, RoutedEventArgs e)
    {
        var regWin = new RegistroWindow(_db);
        regWin.ShowDialog();
        // Si se registró exitosamente, prellenar el usuario
    }
}
