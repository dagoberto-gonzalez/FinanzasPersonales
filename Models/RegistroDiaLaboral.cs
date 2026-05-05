namespace FinanzasPersonales.Models;

public class RegistroDiaLaboral
{
    public int      Id                  { get; set; }
    public int      UsuarioId           { get; set; }
    public DateTime Fecha               { get; set; }
    public double   HorasNormales       { get; set; }
    public double   HorasExtraDiurnas   { get; set; }
    public double   HorasExtraNocturnas { get; set; }
    public double   HorasDobles         { get; set; }
    public bool     EsFeriado           { get; set; }
    public bool     EsAusencia          { get; set; }
    public bool     TieneGoceSalario    { get; set; }
    public decimal  Viaticos            { get; set; }

    // Computed display
    public string FechaDisplay => Fecha.ToString("dd/MM/yyyy");
    public string TipoDisplay
    {
        get
        {
            if (EsAusencia) return TieneGoceSalario ? "Ausencia c/goce" : "Ausencia s/goce";
            if (EsFeriado)  return "Feriado";
            return "Normal";
        }
    }
    public double TotalHoras =>
        HorasNormales + HorasExtraDiurnas + HorasExtraNocturnas + HorasDobles;
}
