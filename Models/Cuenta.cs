using System.Windows.Media;

namespace FinanzasPersonales.Models;

public class Cuenta
{
    public int     Id           { get; set; }
    public string  Nombre       { get; set; } = string.Empty;
    public string  Tipo         { get; set; } = "Cuenta corriente";
    public string  Banco        { get; set; } = string.Empty;
    public bool    Activa       { get; set; } = true;

    /// <summary>Saldo de apertura: lo que había en la cuenta antes de la primera transacción.</summary>
    public decimal SaldoInicial { get; set; }

    /// <summary>
    /// Calculado por <c>AppDatabase.ObtenerCuentas</c> como
    /// <c>SaldoInicial + Σ transacciones</c>. No se almacena: asignarlo no persiste nada.
    /// </summary>
    public decimal SaldoActual  { get; set; }

    public string EstadoTexto  => Activa ? "Activa" : "Inactiva";
    public string BancoDisplay => string.IsNullOrWhiteSpace(Banco) ? "—" : Banco;
    public string SaldoDisplay => $"₡{SaldoActual:N0}";

    private static readonly Brush _verde = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
    private static readonly Brush _rojo  = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
    public Brush SaldoBrush => SaldoActual >= 0 ? _verde : _rojo;
}
