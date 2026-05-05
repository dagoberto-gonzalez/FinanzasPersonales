using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FinanzasPersonales.Models;

namespace FinanzasPersonales.Windows;

public partial class IngresoLaboralWindow : Window
{
    public DateTime   FechaConfirmada      { get; private set; }
    public decimal    MontoConfirmado      { get; private set; }
    public string     DescripcionConfirmada { get; private set; } = "";
    public Cuenta?    CuentaSeleccionada   => CboCuentas.SelectedItem as Cuenta;

    public IngresoLaboralWindow(decimal montoPrelleno, string descripcionPrellena,
                                IEnumerable<Cuenta> cuentas)
    {
        InitializeComponent();
        TxtFecha.Text       = DateTime.Today.ToString("dd/MM/yyyy");
        TxtMonto.Text       = montoPrelleno > 0 ? montoPrelleno.ToString("F0") : "";
        TxtDescripcion.Text = descripcionPrellena;
        CboCuentas.ItemsSource = cuentas;
    }

    private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!DateTime.TryParseExact(TxtFecha.Text.Trim(), "dd/MM/yyyy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            MostrarError("Fecha inválida. Use el formato dd/MM/yyyy.");
            return;
        }

        if (!decimal.TryParse(TxtMonto.Text.Replace(",", "."), NumberStyles.Any,
            CultureInfo.InvariantCulture, out var monto) || monto <= 0)
        {
            MostrarError("El monto debe ser un número mayor a cero.");
            return;
        }

        FechaConfirmada       = fecha;
        MontoConfirmado       = monto;
        DescripcionConfirmada = TxtDescripcion.Text.Trim();
        DialogResult          = true;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void MostrarError(string mensaje)
    {
        TxtError.Text       = mensaje;
        TxtError.Visibility = Visibility.Visible;
    }
}
