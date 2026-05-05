namespace FinanzasPersonales.ViewModels;

public class GrupoDia<T>
{
    public int     Dia    { get; }
    public string  Titulo => $"Día {Dia}";
    public List<T> Items  { get; }

    public GrupoDia(int dia, IEnumerable<T> items)
    {
        Dia   = dia;
        Items = [..items];
    }
}
