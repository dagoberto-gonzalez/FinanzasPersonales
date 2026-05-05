namespace FinanzasPersonales.Services;

public static class SessionService
{
    public static bool   EstaAutenticado { get; private set; }
    public static int    UsuarioId       { get; private set; }
    public static string NombreUsuario   { get; private set; } = string.Empty;
    public static string Rol             { get; private set; } = "Normal";

    public static bool EsAdmin => Rol == "Admin";

    public static void IniciarSesion(int usuarioId, string usuario, string rol)
    {
        EstaAutenticado = true;
        UsuarioId       = usuarioId;
        NombreUsuario   = usuario;
        Rol             = rol;
    }

    public static void CerrarSesion()
    {
        EstaAutenticado = false;
        UsuarioId       = 0;
        NombreUsuario   = string.Empty;
        Rol             = "Normal";
    }
}
