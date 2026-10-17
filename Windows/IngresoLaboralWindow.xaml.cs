using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;

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
        // Con céntimos: la planilla los tiene (₡216.818,38) y redondear aquí registraba otro monto.
        TxtMonto.Text       = montoPrelleno > 0 ? montoPrelleno.ToString("N2") : "";
        TxtDescripcion.Text = descripcionPrellena;
        var lista = cuentas.ToList();
        CboCuentas.ItemsSource = lista;
        if (lista.Count == 0)
        {
            CboCuentas.Visibility      = Visibility.Collapsed;
            AvisoSinCuentas.Visibility = Visibility.Visible;
        }
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

        // Dinero entiende "216.818,38" (como se rellena) y "216818.38"; el parser de antes cambiaba
        // la coma por punto y leía el primero como 216,81838.
        if (!Dinero.TryParse(TxtMonto.Text, out var monto) || monto <= 0)
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
