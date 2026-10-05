namespace FinanzasPersonales.Models;

public class RegistroDiaLaboral
{
    public int      Id                  { get; set; }
    public int      UsuarioId           { get; set; }
    public DateTime Fecha               { get; set; }
    /// <summary>
    /// Horas trabajadas en un feriado. Se pagan a 1× como "Horas Normales" en la planilla, sobre
    /// el salario fijo que ya cubre el día. Antes el feriado era una casilla que valía siempre
    /// la jornada diaria (8,5 h), y el comprobante de agosto de 2026 paga las 9 trabajadas.
    /// </summary>
    public double   HorasNormales       { get; set; }
    public double   HorasExtraDiurnas   { get; set; }
    public double   HorasExtraNocturnas { get; set; }
    public double   HorasDobles         { get; set; }
    public bool     EsFeriado           { get; set; }
    public bool     TieneGoceSalario    { get; set; }
    public decimal  Viaticos            { get; set; }

    /// <summary>
    /// Horas que no se trabajaron ese día. Antes la ausencia era un booleano de día completo,
    /// así que faltar tres horas no se podía registrar.
    /// </summary>
    public double HorasAusencia { get; set; }

    public bool EsAusencia => HorasAusencia > 0;

    // Computed display
    public string FechaDisplay => Fecha.ToString("dd/MM/yyyy");
    public string TipoDisplay
    {
        get
        {
            if (EsAusencia)
            {
                var goce = TieneGoceSalario ? "c/goce" : "s/goce";
                return $"Ausencia {HorasAusencia:0.#} h {goce}";
            }
            if (HorasNormales > 0) return $"Feriado {HorasNormales:0.#} h";
            return "Normal";
        }
    }
    public double TotalHoras =>
        HorasNormales + HorasExtraDiurnas + HorasExtraNocturnas + HorasDobles;
}
