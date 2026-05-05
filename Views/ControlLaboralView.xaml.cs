using System.Windows;
using System.Windows.Controls;
using FinanzasPersonales.Data;
using FinanzasPersonales.ViewModels;
using FinanzasPersonales.Windows;

namespace FinanzasPersonales.Views;

public partial class ControlLaboralView : UserControl
{
    private readonly ControlLaboralViewModel _vm;

    public ControlLaboralView(AppDatabase db, int usuarioId)
    {
        InitializeComponent();
        _vm = new ControlLaboralViewModel(db, usuarioId);
        DataContext = _vm;
    }

    public void Actualizar() => _vm.Cargar();

    public void BtnRegistrarIngreso_Click(object sender, RoutedEventArgs e)
    {
        var win = new IngresoLaboralWindow(
            _vm.SalarioNetoCalculado,
            _vm.PeriodoActualLabel,
            _vm.Cuentas)
        {
            Owner = Window.GetWindow(this)
        };

        if (win.ShowDialog() == true)
            _vm.EjecutarRegistroIngreso(
                win.FechaConfirmada,
                win.MontoConfirmado,
                win.DescripcionConfirmada,
                win.CuentaSeleccionada?.Id);
    }
}
