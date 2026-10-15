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

    /// <summary>
    /// Lo reservado en metas de esta cuenta (D5). Calculado de los movimientos de las metas, como
    /// el saldo: no se almacena. El dinero sigue en la cuenta; sólo tiene destino.
    /// </summary>
    public decimal Apartado { get; set; }

    /// <summary>Lo que no está reservado para ninguna meta. Negativo si se gastó dinero apartado.</summary>
    public decimal Libre => SaldoActual - Apartado;

    public string ApartadoDisplay => Apartado > 0 ? $"₡{Apartado:N0}" : "—";
    public string LibreDisplay    => $"₡{Libre:N0}";

    /// <summary>
    /// La fila "Efectivo" de la pantalla Cuentas: no es una cuenta guardada (Id 0), sino lo que
    /// queda de los movimientos sin cuenta. Se muestra junto a las cuentas para ver todo el dinero
    /// en un sitio, pero no se edita ni se borra.
    /// </summary>
    public bool EsEfectivo { get; private init; }

    public static Cuenta Efectivo(decimal saldo) => new()
    {
        Nombre = "Efectivo", Tipo = "Efectivo", SaldoActual = saldo, EsEfectivo = true
    };

    public string EstadoTexto  => Activa ? "Activa" : "Inactiva";
    public string BancoDisplay => string.IsNullOrWhiteSpace(Banco) ? "—" : Banco;
    public string SaldoDisplay => $"₡{SaldoActual:N0}";

    private static readonly Brush _verde = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
    private static readonly Brush _rojo  = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
    public Brush SaldoBrush => SaldoActual >= 0 ? _verde : _rojo;
}
