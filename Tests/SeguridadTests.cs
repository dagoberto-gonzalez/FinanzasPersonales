using System.IO;
using System.Security.Cryptography;
using System.Text;
using FinanzasPersonales.Data;
using FinanzasPersonales.Services;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Cuentas y contraseñas. Antes: una instalación nueva traía una cuenta "Admin" con la
/// contraseña admin123 escrita en el código, y las contraseñas se guardaban con SHA-256 sin sal.
/// </summary>
[Collection("Sesion")]
public class SeguridadTests
{
    static SeguridadTests() => Contrasenas.Iteraciones = 1_000;

    private static string Sha256Viejo(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    /// <summary>Una base vacía en una carpeta temporal, sin el usuario que crea BaseDePrueba.</summary>
    private static string RutaNueva()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "fp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        return Path.Combine(carpeta, "nueva.db");
    }

    // ── Sin administrador de fábrica ──

    [Fact]
    public void UnaInstalacionNuevaNoTraeNingunaCuenta()
    {
        var db = new AppDatabase(RutaNueva());

        Assert.Equal(0, db.ContarUsuarios());
        Assert.Null(db.ValidarCredenciales("Admin", "admin123"));
    }

    [Fact]
    public void LaPrimeraCuentaRegistradaEsLaAdministradora()
    {
        var db = new AppDatabase(RutaNueva());

        var primera = new RegistroViewModel(db) { NombreUsuario = "duena", Password = "secreta1", Confirmar = "secreta1" };
        primera.RegistrarCommand.Execute(null);
        var segunda = new RegistroViewModel(db) { NombreUsuario = "otra", Password = "secreta2", Confirmar = "secreta2" };
        segunda.RegistrarCommand.Execute(null);

        Assert.Equal("Admin",  db.ValidarCredenciales("duena", "secreta1")!.Rol);
        Assert.Equal("Normal", db.ValidarCredenciales("otra",  "secreta2")!.Rol);
    }

    [Fact]
    public void ElInicioDeSesionAvisaCuandoNoHayCuentas()
    {
        var db = new AppDatabase(RutaNueva());
        var vm = new LoginViewModel(db);
        Assert.True(vm.SinCuentas);

        db.InsertarUsuario("duena", "secreta1", "Admin");
        vm.ActualizarSinCuentas();

        Assert.False(vm.SinCuentas);
    }

    /// <summary>
    /// La versión mono-usuario guardaba sus credenciales en Configuracion. Esas se siguen
    /// migrando, y su hash viejo se reemplaza al iniciar sesión.
    /// </summary>
    [Fact]
    public void LasCredencialesDeLaVersionMonoUsuarioSeMigran()
    {
        var ruta = RutaNueva();
        new AppDatabase(ruta);
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={ruta}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO Configuracion (Clave, Valor) VALUES ('usuario','dago'), ('password_hash',$h)";
            cmd.Parameters.AddWithValue("$h", Sha256Viejo("vieja123"));
            cmd.ExecuteNonQuery();
        }

        var db = new AppDatabase(ruta);   // vuelve a correr las migraciones

        var u = db.ValidarCredenciales("dago", "vieja123");
        Assert.NotNull(u);
        Assert.Equal("Admin", u!.Rol);
        Assert.StartsWith("pbkdf2-sha256$", db.ObtenerUsuarios().Single().PasswordHash);
    }

    // ── Cómo se guardan las contraseñas ──

    [Fact]
    public void LaMismaContrasenaNoDaElMismoHash()
    {
        // Con SHA-256 sin sal, dos usuarios con la misma contraseña tenían el mismo hash.
        Assert.NotEqual(Contrasenas.Crear("clave123"), Contrasenas.Crear("clave123"));
    }

    [Fact]
    public void SeVerificaLaCorrectaYSeRechazaLaIncorrecta()
    {
        var guardado = Contrasenas.Crear("clave123");

        Assert.True (Contrasenas.Verificar("clave123", guardado, out var actualizar));
        Assert.False(actualizar);
        Assert.False(Contrasenas.Verificar("clave124", guardado, out _));
        Assert.False(Contrasenas.Verificar("clave123", "basura", out _));
    }

    [Fact]
    public void LaContrasenaNoQuedaGuardadaComoSha256()
    {
        using var t = new BaseDePrueba();
        var u = t.Db.InsertarUsuario("alguien", "clave123");

        var guardado = t.Db.ObtenerUsuarios().Single(x => x.Id == u.Id).PasswordHash;

        Assert.StartsWith("pbkdf2-sha256$", guardado);
        Assert.NotEqual(Sha256Viejo("clave123"), guardado);
    }

    /// <summary>
    /// Las cuentas existentes no tienen que volver a crearse: la primera vez que inician sesión
    /// su contraseña se vuelve a guardar en el formato nuevo.
    /// </summary>
    [Fact]
    public void UnaCuentaConElHashViejoSeActualizaAlIniciarSesion()
    {
        using var t = new BaseDePrueba();
        var u = t.Db.InsertarUsuario("veterano", "clave123");
        t.Sql($"UPDATE Usuarios SET PasswordHash='{Sha256Viejo("clave123")}' WHERE Id={u.Id}");

        Assert.Null(t.Db.ValidarCredenciales("veterano", "otra"));   // la incorrecta sigue fallando
        Assert.NotNull(t.Db.ValidarCredenciales("veterano", "clave123"));

        var guardado = t.Db.ObtenerUsuarios().Single(x => x.Id == u.Id).PasswordHash;
        Assert.StartsWith("pbkdf2-sha256$", guardado);
        Assert.NotNull(t.Db.ValidarCredenciales("veterano", "clave123"));   // y sigue entrando
    }

    [Fact]
    public void CambiarLaContrasenaUsaElFormatoNuevo()
    {
        using var t = new BaseDePrueba();
        var u = t.Db.InsertarUsuario("alguien", "clave123");

        t.Db.CambiarPassword(u.Id, "nueva456");

        Assert.Null   (t.Db.ValidarCredenciales("alguien", "clave123"));
        Assert.NotNull(t.Db.ValidarCredenciales("alguien", "nueva456"));
    }
}
