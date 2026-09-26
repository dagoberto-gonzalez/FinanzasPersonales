using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using FinanzasPersonales.Models;

namespace FinanzasPersonales.Data;

public class AppDatabase
{
    private readonly string _cs;

    /// <summary>Ruta del archivo SQLite en uso (útil para diagnóstico y pruebas).</summary>
    public string RutaArchivo { get; }

    /// <summary>
    /// Abre (o crea) la base de datos.
    /// </summary>
    /// <param name="dbPath">
    /// Ruta completa del archivo .db. Si es <c>null</c> se usa la ubicación de producción
    /// (<c>%APPDATA%\FinanzasPersonales\finanzas.db</c>). Las pruebas pasan una ruta temporal.
    /// </param>
    public AppDatabase(string? dbPath = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FinanzasPersonales");
            Directory.CreateDirectory(dir);
            dbPath = Path.Combine(dir, "finanzas.db");
        }
        else
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }

        RutaArchivo = dbPath;
        _cs         = $"Data Source={dbPath}";
        Inicializar();
    }

    // ── Utilidades ────────────────────────────────────────────────────────────

    private SqliteConnection Abrir() { var c = new SqliteConnection(_cs); c.Open(); return c; }

    public static string Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    private static HashSet<string> GetColumnas(SqliteConnection conn, string tabla)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tabla})";
        using var r = cmd.ExecuteReader();
        while (r.Read()) cols.Add(r.GetString(1));
        return cols;
    }

    private static bool TablaExiste(SqliteConnection conn, string tabla)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$t";
        cmd.Parameters.AddWithValue("$t", tabla);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    private static void AddColumna(SqliteConnection conn, string tabla, string col, string def)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {tabla} ADD COLUMN {col} {def}";
        cmd.ExecuteNonQuery();
    }

    private static T EjecutarScalar<T>(SqliteConnection conn, string sql, params (string, object?)[] ps)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        var r = cmd.ExecuteScalar();
        return r is T t ? t : (T)Convert.ChangeType(r!, typeof(T));
    }

    private static void EjecutarNonQuery(SqliteConnection conn, string sql, params (string, object?)[] ps)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    // ── Inicialización ────────────────────────────────────────────────────────

    private void Inicializar()
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Configuracion (
                Clave TEXT PRIMARY KEY,
                Valor TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Usuarios (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                NombreUsuario TEXT    NOT NULL UNIQUE,
                PasswordHash  TEXT    NOT NULL,
                Rol           TEXT    NOT NULL DEFAULT 'Normal',
                Activo        INTEGER NOT NULL DEFAULT 1,
                FechaCreacion TEXT    NOT NULL DEFAULT (date('now'))
            );
            CREATE TABLE IF NOT EXISTS Transacciones (
                Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                UsuarioId        INTEGER NOT NULL DEFAULT 1,
                Tipo             TEXT    NOT NULL,
                Monto            REAL    NOT NULL,
                Categoria        TEXT    NOT NULL,
                Descripcion      TEXT    NOT NULL DEFAULT '',
                Fecha            TEXT    NOT NULL,
                Notas            TEXT    NOT NULL DEFAULT '',
                CuentaNombre     TEXT    NOT NULL DEFAULT 'Efectivo',
                TarjetaCreditoId INTEGER
            );
            CREATE TABLE IF NOT EXISTS MetasAhorro (
                Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                UsuarioId      INTEGER NOT NULL DEFAULT 1,
                Nombre         TEXT    NOT NULL,
                MontoObjetivo  REAL    NOT NULL,
                MontoActual    REAL    NOT NULL DEFAULT 0,
                FechaLimite    TEXT    NOT NULL
            );
            CREATE TABLE IF NOT EXISTS GastosFijos (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                UsuarioId       INTEGER NOT NULL DEFAULT 1,
                Nombre          TEXT    NOT NULL,
                Monto           REAL    NOT NULL,
                DiaVencimiento  INTEGER NOT NULL,
                DiaVencimiento2 INTEGER NOT NULL DEFAULT 0,
                Activo          INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS IngresosFijos (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                UsuarioId   INTEGER NOT NULL DEFAULT 1,
                Nombre      TEXT    NOT NULL,
                Monto       REAL    NOT NULL,
                DiaIngreso  INTEGER NOT NULL,
                DiaIngreso2 INTEGER NOT NULL DEFAULT 0,
                Activo      INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS Categorias (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                UsuarioId INTEGER,
                Nombre    TEXT    NOT NULL,
                UNIQUE(UsuarioId, Nombre)
            );
            """;
        cmd.ExecuteNonQuery();

        // Cuentas bancarias / métodos de pago
        InicializarCuentas(conn);
        // TarjetasCredito necesita tabla separada (UNIQUE varía por versión)
        InicializarTarjetasCredito(conn);
        // Migración saldo de cuentas (debe ir antes de PagosMensuales / IngresosFijos)
        MigrarCuentas(conn);
        // PagosMensuales
        MigrarPagosMensuales(conn);
        // Control Laboral
        InicializarControlLaboral(conn);
        // Cierre mensual
        InicializarResumenMensual(conn);
        // Migraciones de columnas
        MigrarTransacciones(conn);
        MigrarMetasAhorro(conn);
        MigrarGastosFijos(conn);
        MigrarIngresosFijos(conn);
        MigrarTarjetas(conn);
        MigrarCategorias(conn);
        // Migración mono→multi usuario
        MigrarAMultiUsuario(conn);
        // Índices (al final: dependen de columnas que crean las migraciones anteriores)
        CrearIndices(conn);
        // Datos globales iniciales
        SeedConfiguracion(conn);
    }

    private static void InicializarCuentas(SqliteConnection conn)
    {
        if (!TablaExiste(conn, "Cuentas"))
        {
            EjecutarNonQuery(conn, """
                CREATE TABLE Cuentas (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    UsuarioId    INTEGER NOT NULL,
                    Nombre       TEXT    NOT NULL,
                    Tipo         TEXT    NOT NULL DEFAULT 'Débito',
                    Banco        TEXT    NOT NULL DEFAULT '',
                    Activa       INTEGER NOT NULL DEFAULT 1,
                    SaldoInicial REAL    NOT NULL DEFAULT 0,
                    UNIQUE(UsuarioId, Nombre)
                )
                """);
        }
    }

    /// <summary>
    /// El saldo de una cuenta dejó de ser un contador que se actualizaba a mano en diez sitios
    /// distintos y pasó a calcularse: <c>SaldoInicial + Σ transacciones de la cuenta</c>.
    /// Esta migración conserva el valor acumulado que hubiera, convirtiéndolo en saldo inicial
    /// sólo cuando no puede derivarse de las transacciones existentes.
    /// </summary>
    private static void MigrarCuentas(SqliteConnection conn)
    {
        var cols = GetColumnas(conn, "Cuentas");

        if (!cols.Contains("SaldoInicial"))
        {
            AddColumna(conn, "Cuentas", "SaldoInicial", "REAL NOT NULL DEFAULT 0");

            // Si venía del esquema anterior, el saldo acumulado que tuviera la cuenta y que NO
            // se explique por sus transacciones es, por definición, su saldo de apertura.
            // Ojo: esto corre antes que MigrarTransacciones, así que CuentaId puede no existir
            // todavía; en ese caso no hay nada que descontar y el acumulado ES el saldo inicial.
            var transCols = TablaExiste(conn, "Transacciones")
                ? GetColumnas(conn, "Transacciones")
                : [];

            if (cols.Contains("SaldoActual") && !transCols.Contains("CuentaId"))
            {
                EjecutarNonQuery(conn, "UPDATE Cuentas SET SaldoInicial = SaldoActual");
            }
            else if (cols.Contains("SaldoActual"))
            {
                EjecutarNonQuery(conn, """
                    UPDATE Cuentas SET SaldoInicial = SaldoActual - COALESCE(
                        (SELECT SUM(CASE WHEN t.Tipo='Ingreso' THEN t.Monto ELSE -t.Monto END)
                         FROM Transacciones t WHERE t.CuentaId = Cuentas.Id), 0)
                    """);
            }
        }

        // SaldoActual ya no se escribe nunca: dejarla sería una columna permanentemente obsoleta
        // que alguien acabaría leyendo. Si el motor no admite DROP COLUMN se ignora sin más.
        if (cols.Contains("SaldoActual"))
        {
            try { EjecutarNonQuery(conn, "ALTER TABLE Cuentas DROP COLUMN SaldoActual"); }
            catch (SqliteException) { /* queda huérfana pero nadie la lee */ }
        }
    }

    /// <summary>
    /// Índices. El esquema no tenía ninguno; al derivar los saldos, cada lectura de cuentas
    /// hace un SUM sobre Transacciones y conviene que no sea un recorrido completo.
    /// </summary>
    private static void CrearIndices(SqliteConnection conn)
    {
        EjecutarNonQuery(conn, """
            CREATE INDEX IF NOT EXISTS IX_Transacciones_CuentaId   ON Transacciones(CuentaId);
            CREATE INDEX IF NOT EXISTS IX_Transacciones_TarjetaId  ON Transacciones(TarjetaCreditoId);
            CREATE INDEX IF NOT EXISTS IX_Transacciones_UsuarioFecha ON Transacciones(UsuarioId, Fecha);
            """);
    }

    private static void InicializarTarjetasCredito(SqliteConnection conn)
    {
        if (!TablaExiste(conn, "TarjetasCredito"))
        {
            EjecutarNonQuery(conn, """
                CREATE TABLE TarjetasCredito (
                    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                    UsuarioId        INTEGER NOT NULL DEFAULT 1,
                    Nombre           TEXT    NOT NULL,
                    LimiteCredito    REAL    NOT NULL DEFAULT 0,
                    SaldoUsado       REAL    NOT NULL DEFAULT 0,
                    DiaCierre        INTEGER NOT NULL DEFAULT 15,
                    DiaPago          INTEGER NOT NULL DEFAULT 5,
                    FechaUltimoCorte TEXT    NOT NULL DEFAULT '',
                    UNIQUE(UsuarioId, Nombre)
                )
                """);
        }
    }

    private static void MigrarTarjetas(SqliteConnection conn)
    {
        var cols = GetColumnas(conn, "TarjetasCredito");

        // Agregar FechaUltimoCorte si no existe (versión vieja)
        if (!cols.Contains("FechaUltimoCorte"))
            AddColumna(conn, "TarjetasCredito", "FechaUltimoCorte", "TEXT NOT NULL DEFAULT ''");

        // Si no tiene UsuarioId necesitamos recrear (para quitar UNIQUE(Nombre) y agregar UsuarioId)
        if (!cols.Contains("UsuarioId"))
        {
            using var tr = conn.BeginTransaction();
            try
            {
                EjecutarNonQuery(conn, """
                    CREATE TABLE TarjetasCredito_v2 (
                        Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                        UsuarioId        INTEGER NOT NULL DEFAULT 1,
                        Nombre           TEXT    NOT NULL,
                        LimiteCredito    REAL    NOT NULL DEFAULT 0,
                        SaldoUsado       REAL    NOT NULL DEFAULT 0,
                        DiaCierre        INTEGER NOT NULL DEFAULT 15,
                        DiaPago          INTEGER NOT NULL DEFAULT 5,
                        FechaUltimoCorte TEXT    NOT NULL DEFAULT '',
                        UNIQUE(UsuarioId, Nombre)
                    )
                    """);
                EjecutarNonQuery(conn, """
                    INSERT INTO TarjetasCredito_v2 (Id, UsuarioId, Nombre, LimiteCredito, SaldoUsado, DiaCierre, DiaPago, FechaUltimoCorte)
                    SELECT Id, 1, Nombre, LimiteCredito, SaldoUsado, DiaCierre, DiaPago, COALESCE(FechaUltimoCorte,'')
                    FROM TarjetasCredito
                    """);
                EjecutarNonQuery(conn, "DROP TABLE TarjetasCredito");
                EjecutarNonQuery(conn, "ALTER TABLE TarjetasCredito_v2 RENAME TO TarjetasCredito");
                tr.Commit();
            }
            catch { tr.Rollback(); throw; }
        }
    }

    private static void MigrarPagosMensuales(SqliteConnection conn)
    {
        var cols = GetColumnas(conn, "PagosMensuales");

        if (!cols.Any())
        {
            EjecutarNonQuery(conn, """
                CREATE TABLE IF NOT EXISTS PagosMensuales (
                    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    UsuarioId     INTEGER NOT NULL DEFAULT 1,
                    TipoFijo      TEXT    NOT NULL,
                    FijoId        INTEGER NOT NULL,
                    Anio          INTEGER NOT NULL,
                    Mes           INTEGER NOT NULL,
                    Dia           INTEGER NOT NULL DEFAULT 1,
                    Pagado        INTEGER NOT NULL DEFAULT 0,
                    MetodoPago    TEXT    NOT NULL DEFAULT '',
                    TransaccionId INTEGER,
                    UNIQUE(UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia)
                )
                """);
            return;
        }

        // Agregar UsuarioId si falta
        if (!cols.Contains("UsuarioId"))
            AddColumna(conn, "PagosMensuales", "UsuarioId", "INTEGER NOT NULL DEFAULT 1");

        if (cols.Contains("Dia")) return; // Ya con Dia → migrado

        // Migrar tabla antigua (sin Dia) a nueva (con Dia)
        using var tr = conn.BeginTransaction();
        try
        {
            EjecutarNonQuery(conn, """
                CREATE TABLE PagosMensuales_v2 (
                    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    UsuarioId     INTEGER NOT NULL DEFAULT 1,
                    TipoFijo      TEXT    NOT NULL,
                    FijoId        INTEGER NOT NULL,
                    Anio          INTEGER NOT NULL,
                    Mes           INTEGER NOT NULL,
                    Dia           INTEGER NOT NULL DEFAULT 1,
                    Pagado        INTEGER NOT NULL DEFAULT 0,
                    MetodoPago    TEXT    NOT NULL DEFAULT '',
                    TransaccionId INTEGER,
                    UNIQUE(UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia)
                )
                """);
            EjecutarNonQuery(conn, """
                INSERT OR IGNORE INTO PagosMensuales_v2 (UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia, Pagado)
                SELECT COALESCE(p.UsuarioId,1), p.TipoFijo, p.FijoId, p.Anio, p.Mes,
                       CASE WHEN p.TipoFijo='Gasto'
                            THEN COALESCE((SELECT DiaVencimiento FROM GastosFijos WHERE Id=p.FijoId LIMIT 1), 1)
                            ELSE COALESCE((SELECT DiaIngreso    FROM IngresosFijos WHERE Id=p.FijoId LIMIT 1), 1)
                       END,
                       p.Pagado
                FROM PagosMensuales p
                """);
            EjecutarNonQuery(conn, "DROP TABLE PagosMensuales");
            EjecutarNonQuery(conn, "ALTER TABLE PagosMensuales_v2 RENAME TO PagosMensuales");
            tr.Commit();
        }
        catch { tr.Rollback(); throw; }
    }

    private static void MigrarTransacciones(SqliteConnection conn)
    {
        var cols = GetColumnas(conn, "Transacciones");
        if (!cols.Contains("Notas"))            AddColumna(conn, "Transacciones", "Notas",            "TEXT NOT NULL DEFAULT ''");
        if (!cols.Contains("CuentaNombre"))     AddColumna(conn, "Transacciones", "CuentaNombre",     "TEXT NOT NULL DEFAULT 'Efectivo'");
        if (!cols.Contains("TarjetaCreditoId")) AddColumna(conn, "Transacciones", "TarjetaCreditoId", "INTEGER");
        if (!cols.Contains("UsuarioId"))        AddColumna(conn, "Transacciones", "UsuarioId",        "INTEGER NOT NULL DEFAULT 1");
        if (!cols.Contains("CuentaId"))         AddColumna(conn, "Transacciones", "CuentaId",         "INTEGER");

        // Decisión D2: qué tarjeta salda este pago. Distingue "compré con la tarjeta" (gasto
        // que aún no mueve efectivo) de "pagué la tarjeta" (mueve efectivo pero no es un gasto
        // nuevo: ya se contó al comprar). Antes esto se adivinaba mirando si la categoría decía
        // "Tarjeta", que es frágil.
        if (!cols.Contains("PagoDeTarjetaId"))
        {
            AddColumna(conn, "Transacciones", "PagoDeTarjetaId", "INTEGER");

            // Los pagos que ya existían los generó TarjetasViewModel.Abonar con una descripción
            // exacta; sin esto se contarían dos veces (la compra y el pago).
            EjecutarNonQuery(conn, """
                UPDATE Transacciones
                SET PagoDeTarjetaId = (
                    SELECT tc.Id FROM TarjetasCredito tc
                    WHERE tc.UsuarioId = Transacciones.UsuarioId
                      AND Transacciones.Descripcion = 'Pago tarjeta ' || tc.Nombre)
                WHERE Tipo = 'Gasto' AND Categoria = 'Tarjeta'
                  AND TarjetaCreditoId IS NULL
                  AND Descripcion LIKE 'Pago tarjeta %'
                """);
        }
    }

    private static void MigrarMetasAhorro(SqliteConnection conn)
    {
        var cols = GetColumnas(conn, "MetasAhorro");
        if (!cols.Contains("UsuarioId"))
            AddColumna(conn, "MetasAhorro", "UsuarioId", "INTEGER NOT NULL DEFAULT 1");
    }

    private static void MigrarGastosFijos(SqliteConnection conn)
    {
        var cols = GetColumnas(conn, "GastosFijos");
        if (!cols.Contains("DiaVencimiento2")) AddColumna(conn, "GastosFijos", "DiaVencimiento2", "INTEGER NOT NULL DEFAULT 0");
        if (!cols.Contains("UsuarioId"))       AddColumna(conn, "GastosFijos", "UsuarioId",       "INTEGER NOT NULL DEFAULT 1");
    }

    private static void MigrarIngresosFijos(SqliteConnection conn)
    {
        var cols = GetColumnas(conn, "IngresosFijos");
        if (!cols.Contains("DiaIngreso2")) AddColumna(conn, "IngresosFijos", "DiaIngreso2", "INTEGER NOT NULL DEFAULT 0");
        if (!cols.Contains("UsuarioId"))   AddColumna(conn, "IngresosFijos", "UsuarioId",   "INTEGER NOT NULL DEFAULT 1");
        if (!cols.Contains("CuentaId"))    AddColumna(conn, "IngresosFijos", "CuentaId",    "INTEGER");
    }

    private static void MigrarCategorias(SqliteConnection conn)
    {
        var cols = GetColumnas(conn, "Categorias");
        if (!cols.Contains("UsuarioId"))
        {
            // Tabla vieja con solo Nombre → recrear con UsuarioId nullable (NULL=global)
            using var tr = conn.BeginTransaction();
            try
            {
                EjecutarNonQuery(conn, """
                    CREATE TABLE Categorias_v2 (
                        Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                        UsuarioId INTEGER,
                        Nombre    TEXT NOT NULL,
                        UNIQUE(UsuarioId, Nombre)
                    )
                    """);
                // Las categorías existentes se vuelven globales (UsuarioId = NULL)
                EjecutarNonQuery(conn, "INSERT OR IGNORE INTO Categorias_v2 (UsuarioId, Nombre) SELECT NULL, Nombre FROM Categorias");
                EjecutarNonQuery(conn, "DROP TABLE Categorias");
                EjecutarNonQuery(conn, "ALTER TABLE Categorias_v2 RENAME TO Categorias");
                tr.Commit();
            }
            catch { tr.Rollback(); throw; }
        }
    }

    /// <summary>Migra de sistema mono-usuario a multi-usuario: crea el primer Admin desde Configuracion.</summary>
    private static void MigrarAMultiUsuario(SqliteConnection conn)
    {
        var hayUsuarios = EjecutarScalar<long>(conn, "SELECT COUNT(*) FROM Usuarios");
        if (hayUsuarios > 0) return;

        // Leer credenciales del sistema antiguo
        using var cmdCfg = conn.CreateCommand();
        cmdCfg.CommandText = "SELECT Clave, Valor FROM Configuracion WHERE Clave IN ('usuario','password_hash')";
        string usuario = "Admin", passwordHash = Hash("admin123");
        using var r = cmdCfg.ExecuteReader();
        while (r.Read())
        {
            if (r.GetString(0) == "usuario")       usuario      = r.GetString(1);
            if (r.GetString(0) == "password_hash") passwordHash = r.GetString(1);
        }
        r.Close();

        // Insertar usuario administrador
        EjecutarNonQuery(conn,
            "INSERT INTO Usuarios (NombreUsuario, PasswordHash, Rol, Activo, FechaCreacion) VALUES ($u,$h,'Admin',1,date('now'))",
            ("$u", usuario), ("$h", passwordHash));

        long adminId = EjecutarScalar<long>(conn, "SELECT last_insert_rowid()");

        // Asociar datos existentes al admin (UsuarioId ya tiene DEFAULT 1 pero puede no coincidir)
        if (adminId != 1)
        {
            EjecutarNonQuery(conn, $"UPDATE Transacciones   SET UsuarioId={adminId} WHERE UsuarioId=1");
            EjecutarNonQuery(conn, $"UPDATE MetasAhorro     SET UsuarioId={adminId} WHERE UsuarioId=1");
            EjecutarNonQuery(conn, $"UPDATE TarjetasCredito SET UsuarioId={adminId} WHERE UsuarioId=1");
            EjecutarNonQuery(conn, $"UPDATE GastosFijos     SET UsuarioId={adminId} WHERE UsuarioId=1");
            EjecutarNonQuery(conn, $"UPDATE IngresosFijos   SET UsuarioId={adminId} WHERE UsuarioId=1");
            EjecutarNonQuery(conn, $"UPDATE PagosMensuales  SET UsuarioId={adminId} WHERE UsuarioId=1");
        }
    }

    private static void SeedConfiguracion(SqliteConnection conn)
    {
        // Valores por defecto de configuración global
        EjecutarNonQuery(conn,
            "INSERT OR IGNORE INTO Configuracion VALUES ('moneda','₡'),('max_usuarios','20'),('presupuesto_mensual','500000')");

        // Categorías globales por defecto
        // Nota: SQLite trata NULL != NULL en UNIQUE, así que INSERT OR IGNORE no evita duplicados
        // cuando UsuarioId es NULL. Usamos WHERE NOT EXISTS para verificar antes de insertar.
        var cats = new[] { "Alimentación", "Transporte", "Salud", "Entretenimiento",
                           "Educación", "Servicios", "Hogar", "Ropa", "Tecnología", "Otros" }
                   .Concat(CategoriasSistema);
        foreach (var c in cats)
            EjecutarNonQuery(conn,
                "INSERT INTO Categorias (UsuarioId, Nombre) SELECT NULL,$n WHERE NOT EXISTS (SELECT 1 FROM Categorias WHERE UsuarioId IS NULL AND Nombre=$n)",
                ("$n", c));
    }

    // ── Usuarios ──────────────────────────────────────────────────────────────

    public Usuario? ValidarCredenciales(string nombreUsuario, string password)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, NombreUsuario, Rol, Activo
            FROM Usuarios
            WHERE NombreUsuario=$u AND PasswordHash=$h
            """;
        cmd.Parameters.AddWithValue("$u", nombreUsuario.Trim());
        cmd.Parameters.AddWithValue("$h", Hash(password));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Usuario
        {
            Id            = r.GetInt32(0),
            NombreUsuario = r.GetString(1),
            Rol           = r.GetString(2),
            Activo        = r.GetInt32(3) == 1
        };
    }

    public bool ExisteUsuario(string nombreUsuario)
    {
        using var conn = Abrir();
        var n = EjecutarScalar<long>(conn,
            "SELECT COUNT(*) FROM Usuarios WHERE NombreUsuario=$u COLLATE NOCASE",
            ("$u", nombreUsuario.Trim()));
        return n > 0;
    }

    public int ContarUsuarios()
    {
        using var conn = Abrir();
        return (int)EjecutarScalar<long>(conn, "SELECT COUNT(*) FROM Usuarios");
    }

    public int ObtenerMaxUsuarios()
    {
        var val = ObtenerConfig("max_usuarios", "20");
        return int.TryParse(val, out var n) ? n : 20;
    }

    public Usuario InsertarUsuario(string nombreUsuario, string password, string rol = "Normal")
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            "INSERT INTO Usuarios (NombreUsuario, PasswordHash, Rol, Activo, FechaCreacion) VALUES ($u,$h,$r,1,date('now'))",
            ("$u", nombreUsuario.Trim()), ("$h", Hash(password)), ("$r", rol));

        long id = EjecutarScalar<long>(conn, "SELECT last_insert_rowid()");

        // Seed tarjetas BAC para cada nuevo usuario
        SeedTarjetasUsuario(conn, (int)id);

        return new Usuario { Id = (int)id, NombreUsuario = nombreUsuario.Trim(), Rol = rol, Activo = true };
    }

    private static void SeedTarjetasUsuario(SqliteConnection conn, int usuarioId)
    {
        EjecutarNonQuery(conn,
            "INSERT OR IGNORE INTO TarjetasCredito (UsuarioId, Nombre, LimiteCredito, DiaCierre, DiaPago) VALUES ($uid,'BAC Economía',1000000,15,5)",
            ("$uid", usuarioId));
        EjecutarNonQuery(conn,
            "INSERT OR IGNORE INTO TarjetasCredito (UsuarioId, Nombre, LimiteCredito, DiaCierre, DiaPago) VALUES ($uid,'BAC Gane Premios',1000000,15,5)",
            ("$uid", usuarioId));
    }

    public List<Usuario> ObtenerUsuarios()
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, NombreUsuario, PasswordHash, Rol, Activo, FechaCreacion FROM Usuarios ORDER BY Id";
        var list = new List<Usuario>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Usuario
            {
                Id            = r.GetInt32(0),
                NombreUsuario = r.GetString(1),
                PasswordHash  = r.GetString(2),
                Rol           = r.GetString(3),
                Activo        = r.GetInt32(4) == 1,
                FechaCreacion = DateTime.Parse(r.GetString(5))
            });
        return list;
    }

    public void ToggleUsuarioActivo(int usuarioId, bool activo)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            "UPDATE Usuarios SET Activo=$a WHERE Id=$id",
            ("$a", activo ? 1 : 0), ("$id", usuarioId));
    }

    public void EliminarUsuario(int usuarioId)
    {
        using var conn = Abrir();
        // Eliminar todos los datos del usuario
        EjecutarNonQuery(conn, "DELETE FROM Transacciones   WHERE UsuarioId=$id", ("$id", usuarioId));
        EjecutarNonQuery(conn, "DELETE FROM MetasAhorro     WHERE UsuarioId=$id", ("$id", usuarioId));
        EjecutarNonQuery(conn, "DELETE FROM TarjetasCredito WHERE UsuarioId=$id", ("$id", usuarioId));
        EjecutarNonQuery(conn, "DELETE FROM GastosFijos     WHERE UsuarioId=$id", ("$id", usuarioId));
        EjecutarNonQuery(conn, "DELETE FROM IngresosFijos   WHERE UsuarioId=$id", ("$id", usuarioId));
        EjecutarNonQuery(conn, "DELETE FROM PagosMensuales  WHERE UsuarioId=$id", ("$id", usuarioId));
        EjecutarNonQuery(conn, "DELETE FROM Categorias      WHERE UsuarioId=$id", ("$id", usuarioId));
        EjecutarNonQuery(conn, "DELETE FROM Usuarios        WHERE Id=$id",        ("$id", usuarioId));
    }

    public void CambiarPassword(int usuarioId, string nuevaPassword)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            "UPDATE Usuarios SET PasswordHash=$h WHERE Id=$id",
            ("$h", Hash(nuevaPassword)), ("$id", usuarioId));
    }

    // ── Autenticación (legado – compatibilidad interna) ────────────────────────

    public bool ValidarCredencialesLegado(string usuario, string password)
    {
        using var conn = Abrir();
        var usuarioDb = EjecutarScalar<string>(conn, "SELECT Valor FROM Configuracion WHERE Clave='usuario'");
        var hashDb    = EjecutarScalar<string>(conn, "SELECT Valor FROM Configuracion WHERE Clave='password_hash'");
        return string.Equals(usuario, usuarioDb, StringComparison.OrdinalIgnoreCase)
            && hashDb == Hash(password);
    }

    // ── Configuración ─────────────────────────────────────────────────────────

    public string ObtenerConfig(string clave, string defecto = "")
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT Valor FROM Configuracion WHERE Clave=$k";
        cmd.Parameters.AddWithValue("$k", clave);
        var r = cmd.ExecuteScalar();
        return r?.ToString() ?? defecto;
    }

    public void GuardarConfig(string clave, string valor)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            "INSERT INTO Configuracion(Clave,Valor) VALUES($k,$v) ON CONFLICT(Clave) DO UPDATE SET Valor=$v",
            ("$k", clave), ("$v", valor));
    }

    // ── Categorías ────────────────────────────────────────────────────────────

    /// <summary>
    /// Categorías que la propia aplicación escribe por nombre al generar transacciones
    /// automáticas (pagos de fijos, salario, pagos de tarjeta). Se siembran como globales
    /// y no se pueden renombrar ni borrar: si cambiaran de nombre, el código seguiría
    /// escribiendo el nombre viejo y las transacciones quedarían descolgadas del filtro.
    /// </summary>
    public static readonly string[] CategoriasSistema =
        ["Gastos Fijos", "Ingresos Fijos", "Salario", "Tarjeta"];

    public static bool EsCategoriaSistema(string nombre) =>
        CategoriasSistema.Contains(nombre, StringComparer.OrdinalIgnoreCase);

    /// <summary>Devuelve nombres de categorías visibles para el usuario: globales + propias.</summary>
    public List<string> ObtenerCategorias(int usuarioId)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Nombre FROM Categorias
            WHERE UsuarioId IS NULL OR UsuarioId = $uid
            ORDER BY CASE WHEN UsuarioId IS NULL THEN 0 ELSE 1 END, Nombre
            """;
        cmd.Parameters.AddWithValue("$uid", usuarioId);
        var list = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    /// <summary>Devuelve categorías con detalle (EsGlobal) para la pantalla de gestión.</summary>
    public List<Categoria> ObtenerCategoriasDetalle(int usuarioId)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Nombre, CASE WHEN UsuarioId IS NULL THEN 1 ELSE 0 END AS EsGlobal
            FROM Categorias
            WHERE UsuarioId IS NULL OR UsuarioId = $uid
            ORDER BY CASE WHEN UsuarioId IS NULL THEN 0 ELSE 1 END, Nombre
            """;
        cmd.Parameters.AddWithValue("$uid", usuarioId);
        var list = new List<Categoria>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Categoria { Id = r.GetInt32(0), Nombre = r.GetString(1), EsGlobal = r.GetInt32(2) == 1 });
        return list;
    }

    /// <summary>Inserta categoría personal del usuario (o global si esGlobal=true, solo admin).</summary>
    public void InsertarCategoria(string nombre, int usuarioId, bool esGlobal = false)
    {
        using var conn = Abrir();
        object? uid = esGlobal ? (object?)null : usuarioId;
        EjecutarNonQuery(conn,
            "INSERT OR IGNORE INTO Categorias (UsuarioId, Nombre) VALUES ($uid,$n)",
            ("$uid", uid), ("$n", nombre.Trim()));
    }

    public void EliminarCategoria(int categoriaId, int usuarioId, bool esAdmin)
    {
        using var conn = Abrir();

        // Las categorías de sistema no se borran ni siendo admin: la aplicación las escribe
        // por nombre al generar transacciones automáticas.
        var nombre = EjecutarScalar<string>(conn,
            "SELECT COALESCE((SELECT Nombre FROM Categorias WHERE Id=$id),'')", ("$id", categoriaId));
        if (EsCategoriaSistema(nombre)) return;

        // Admin puede borrar cualquiera; usuario normal solo las propias
        if (esAdmin)
            EjecutarNonQuery(conn, "DELETE FROM Categorias WHERE Id=$id", ("$id", categoriaId));
        else
            EjecutarNonQuery(conn, "DELETE FROM Categorias WHERE Id=$id AND UsuarioId=$uid",
                ("$id", categoriaId), ("$uid", usuarioId));
    }

    public void ActualizarCategoria(int categoriaId, string nombreNuevo, int usuarioId, bool esAdmin)
    {
        using var conn = Abrir();

        // Ver EliminarCategoria: renombrarla dejaría el código escribiendo el nombre viejo.
        var nombreActual = EjecutarScalar<string>(conn,
            "SELECT COALESCE((SELECT Nombre FROM Categorias WHERE Id=$id),'')", ("$id", categoriaId));
        if (EsCategoriaSistema(nombreActual)) return;

        if (esAdmin)
            EjecutarNonQuery(conn,
                "UPDATE Categorias SET Nombre=$nuevo WHERE Id=$id",
                ("$nuevo", nombreNuevo.Trim()), ("$id", categoriaId));
        else
            EjecutarNonQuery(conn,
                "UPDATE Categorias SET Nombre=$nuevo WHERE Id=$id AND UsuarioId=$uid",
                ("$nuevo", nombreNuevo.Trim()), ("$id", categoriaId), ("$uid", usuarioId));
    }

    // ── Transacciones ─────────────────────────────────────────────────────────

    public void InsertarTransaccion(Transaccion t, int usuarioId)
    {
        // Una tarjeta de crédito es una fuente de deuda, no de ingresos.
        if (t.Tipo == "Ingreso" && t.TarjetaCreditoId.HasValue)
            throw new ArgumentException(
                "Un ingreso no puede tener una tarjeta de crédito como origen.", nameof(t));

        // Comprar con la tarjeta y pagar la tarjeta son movimientos opuestos: una misma fila no
        // puede ser los dos, o se contaría en los dos ejes a la vez.
        if (t.TarjetaCreditoId.HasValue && t.PagoDeTarjetaId.HasValue)
            throw new ArgumentException(
                "Una transacción no puede ser a la vez compra con tarjeta y pago de tarjeta.", nameof(t));

        // La categoría «Tarjeta» identifica los pagos de tarjeta, y un pago SIEMPRE tiene que
        // decir qué tarjeta salda. Si no, se contaría como un gasto nuevo además de la compra
        // que ya se registró: el mismo dinero dos veces. Los pagos se registran en la pantalla
        // de Tarjetas, que es la única que conoce el saldo pendiente.
        if (t.Categoria == "Tarjeta" && !t.PagoDeTarjetaId.HasValue)
            throw new ArgumentException(
                "Los pagos de tarjeta se registran desde la pantalla Tarjetas, para que se " +
                "descuenten del saldo y no se cuenten dos veces.", nameof(t));

        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Transacciones (UsuarioId,Tipo,Monto,Categoria,Descripcion,Fecha,Notas,CuentaNombre,TarjetaCreditoId,CuentaId,PagoDeTarjetaId)
            VALUES ($uid,$tipo,$monto,$cat,$desc,$fecha,$notas,$cuenta,$tarjeta,$cuentaid,$pagotarjeta)
            """;
        cmd.Parameters.AddWithValue("$uid",      usuarioId);
        cmd.Parameters.AddWithValue("$tipo",     t.Tipo);
        cmd.Parameters.AddWithValue("$monto",    (double)t.Monto);
        cmd.Parameters.AddWithValue("$cat",      t.Categoria);
        cmd.Parameters.AddWithValue("$desc",     t.Descripcion);
        cmd.Parameters.AddWithValue("$fecha",    t.Fecha.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$notas",    t.Notas);
        cmd.Parameters.AddWithValue("$cuenta",   t.CuentaNombre);
        cmd.Parameters.AddWithValue("$tarjeta",  t.TarjetaCreditoId.HasValue ? t.TarjetaCreditoId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$cuentaid", t.CuentaId.HasValue ? t.CuentaId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$pagotarjeta", t.PagoDeTarjetaId.HasValue ? t.PagoDeTarjetaId.Value : DBNull.Value);
        cmd.ExecuteNonQuery();

        // Actualizar saldo de tarjeta de crédito (gastos)
        if (t.TarjetaCreditoId.HasValue && t.Tipo == "Gasto")
        {
            EjecutarNonQuery(conn,
                "UPDATE TarjetasCredito SET SaldoUsado = SaldoUsado + $m WHERE Id=$id AND UsuarioId=$uid",
                ("$m", (double)t.Monto), ("$id", t.TarjetaCreditoId.Value), ("$uid", usuarioId));
        }

        // El saldo de la cuenta no se toca: se deriva de estas mismas transacciones al leerlo.
    }

    public List<Transaccion> ObtenerTransacciones(int usuarioId)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText =
            "SELECT Id,Tipo,Monto,Categoria,Descripcion,Fecha,Notas,CuentaNombre,TarjetaCreditoId,CuentaId,PagoDeTarjetaId " +
            "FROM Transacciones WHERE UsuarioId=$uid ORDER BY Fecha DESC, Id DESC";
        cmd.Parameters.AddWithValue("$uid", usuarioId);
        var list = new List<Transaccion>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(MapTransaccion(r));
        return list;
    }

    public List<Transaccion> ObtenerTransaccionesPorTarjeta(int tarjetaId)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText =
            "SELECT Id,Tipo,Monto,Categoria,Descripcion,Fecha,Notas,CuentaNombre,TarjetaCreditoId,CuentaId,PagoDeTarjetaId " +
            "FROM Transacciones WHERE TarjetaCreditoId=$id ORDER BY Fecha DESC";
        cmd.Parameters.AddWithValue("$id", tarjetaId);
        var list = new List<Transaccion>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(MapTransaccion(r));
        return list;
    }

    public void EliminarTransaccion(int id)
    {
        using var conn = Abrir();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Tipo, Monto, TarjetaCreditoId FROM Transacciones WHERE Id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                var tipo   = r.GetString(0);
                var monto  = r.GetDouble(1);
                var tarjId = r.IsDBNull(2) ? (int?)null : r.GetInt32(2);

                // Revertir saldo de tarjeta de crédito (sigue siendo un contador: ver D3)
                if (tarjId.HasValue && tipo == "Gasto")
                    EjecutarNonQuery(conn,
                        "UPDATE TarjetasCredito SET SaldoUsado = MAX(0, SaldoUsado - $m) WHERE Id=$id2",
                        ("$m", monto), ("$id2", tarjId.Value));

                // El saldo de la cuenta no se revierte: al borrar la fila deja de sumar sola.
            }
        }

        EjecutarNonQuery(conn, "DELETE FROM Transacciones WHERE Id=$id", ("$id", id));
    }

    private static Transaccion MapTransaccion(SqliteDataReader r) => new()
    {
        Id               = r.GetInt32(0),
        Tipo             = r.GetString(1),
        Monto            = (decimal)r.GetDouble(2),
        Categoria        = r.GetString(3),
        Descripcion      = r.GetString(4),
        Fecha            = DateTime.Parse(r.GetString(5)),
        Notas            = r.IsDBNull(6) ? "" : r.GetString(6),
        CuentaNombre     = r.IsDBNull(7) ? "Efectivo" : r.GetString(7),
        TarjetaCreditoId = r.IsDBNull(8) ? null : r.GetInt32(8),
        CuentaId         = r.IsDBNull(9) ? null : r.GetInt32(9),
        PagoDeTarjetaId  = r.IsDBNull(10) ? null : r.GetInt32(10)
    };

    // ── Metas de Ahorro ───────────────────────────────────────────────────────

    public void InsertarMeta(MetaAhorro m, int usuarioId)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO MetasAhorro (UsuarioId,Nombre,MontoObjetivo,MontoActual,FechaLimite)
            VALUES ($uid,$n,$obj,$act,$f)
            """;
        cmd.Parameters.AddWithValue("$uid", usuarioId);
        cmd.Parameters.AddWithValue("$n",   m.Nombre);
        cmd.Parameters.AddWithValue("$obj", (double)m.MontoObjetivo);
        cmd.Parameters.AddWithValue("$act", (double)m.MontoActual);
        cmd.Parameters.AddWithValue("$f",   m.FechaLimite.ToString("yyyy-MM-dd"));
        cmd.ExecuteNonQuery();
    }

    public List<MetaAhorro> ObtenerMetas(int usuarioId)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Nombre,MontoObjetivo,MontoActual,FechaLimite FROM MetasAhorro WHERE UsuarioId=$uid";
        cmd.Parameters.AddWithValue("$uid", usuarioId);
        var list = new List<MetaAhorro>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new MetaAhorro
            {
                Id            = r.GetInt32(0),
                Nombre        = r.GetString(1),
                MontoObjetivo = (decimal)r.GetDouble(2),
                MontoActual   = (decimal)r.GetDouble(3),
                FechaLimite   = DateTime.Parse(r.GetString(4))
            });
        return list;
    }

    public void ActualizarMontoMeta(int id, decimal montoActual)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            "UPDATE MetasAhorro SET MontoActual=$m WHERE Id=$id",
            ("$m", (double)montoActual), ("$id", id));
    }

    public void EliminarMeta(int id)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn, "DELETE FROM MetasAhorro WHERE Id=$id", ("$id", id));
    }

    // ── Tarjetas de Crédito ───────────────────────────────────────────────────

    public List<TarjetaCredito> ObtenerTarjetas(int usuarioId)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Nombre,LimiteCredito,SaldoUsado,DiaCierre,DiaPago,FechaUltimoCorte FROM TarjetasCredito WHERE UsuarioId=$uid";
        cmd.Parameters.AddWithValue("$uid", usuarioId);
        var list = new List<TarjetaCredito>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var t = new TarjetaCredito
            {
                Id               = r.GetInt32(0),
                Nombre           = r.GetString(1),
                LimiteCredito    = (decimal)r.GetDouble(2),
                SaldoUsado       = (decimal)r.GetDouble(3),
                DiaCierre        = r.GetInt32(4),
                DiaPago          = r.GetInt32(5),
                FechaUltimoCorte = r.IsDBNull(6) || string.IsNullOrEmpty(r.GetString(6))
                    ? DateTime.MinValue
                    : DateTime.Parse(r.GetString(6))
            };
            list.Add(t);
        }

        // Auto-reset al cruzar la fecha de cierre de período
        foreach (var t in list)
            AutoRenovarPeriodo(conn, t);

        return list;
    }

    private void AutoRenovarPeriodo(SqliteConnection conn, TarjetaCredito t)
    {
        var inicioPeriodo = t.InicioPeriodo;
        if (t.FechaUltimoCorte < inicioPeriodo.Date)
        {
            EjecutarNonQuery(conn,
                "UPDATE TarjetasCredito SET SaldoUsado=0, FechaUltimoCorte=$f WHERE Id=$id",
                ("$f", inicioPeriodo.ToString("yyyy-MM-dd")), ("$id", t.Id));
            t.SaldoUsado       = 0;
            t.FechaUltimoCorte = inicioPeriodo;
        }
    }

    public void InsertarTarjeta(TarjetaCredito t, int usuarioId)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            "INSERT INTO TarjetasCredito (UsuarioId, Nombre, LimiteCredito, DiaCierre, DiaPago) VALUES ($uid,$n,$l,$c,$p)",
            ("$uid", usuarioId), ("$n", t.Nombre), ("$l", (double)t.LimiteCredito), ("$c", t.DiaCierre), ("$p", t.DiaPago));
    }

    public void ActualizarTarjeta(TarjetaCredito t)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            "UPDATE TarjetasCredito SET Nombre=$nombre, LimiteCredito=$lim, SaldoUsado=$saldo, DiaCierre=$cierre, DiaPago=$pago WHERE Id=$id",
            ("$nombre", t.Nombre), ("$lim", (double)t.LimiteCredito), ("$saldo", (double)t.SaldoUsado),
            ("$cierre", t.DiaCierre), ("$pago", t.DiaPago), ("$id", t.Id));
    }

    public void EliminarTarjeta(int id)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn, "DELETE FROM TarjetasCredito WHERE Id=$id", ("$id", id));
    }

    // ── Cuentas ───────────────────────────────────────────────────────────────

    /// <summary>
    /// El saldo se calcula aquí: <c>SaldoInicial + Σ transacciones de la cuenta</c>.
    /// No existe ningún contador acumulado que mantener en sincronía, así que la deriva entre
    /// el saldo mostrado y las transacciones es imposible por construcción.
    /// </summary>
    public List<Cuenta> ObtenerCuentas(int usuarioId)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.Id, c.Nombre, c.Tipo, c.Banco, c.Activa, c.SaldoInicial,
                   c.SaldoInicial + COALESCE(
                       (SELECT SUM(CASE WHEN t.Tipo='Ingreso' THEN t.Monto ELSE -t.Monto END)
                        FROM Transacciones t WHERE t.CuentaId = c.Id), 0)
            FROM Cuentas c
            WHERE c.UsuarioId = $uid
            ORDER BY c.Activa DESC, c.Nombre
            """;
        cmd.Parameters.AddWithValue("$uid", usuarioId);
        var list = new List<Cuenta>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Cuenta
            {
                Id           = r.GetInt32(0),
                Nombre       = r.GetString(1),
                Tipo         = r.GetString(2),
                Banco        = r.GetString(3),
                Activa       = r.GetInt32(4) == 1,
                SaldoInicial = (decimal)r.GetDouble(5),
                SaldoActual  = (decimal)r.GetDouble(6)
            });
        return list;
    }

    public void InsertarCuenta(Cuenta c, int usuarioId)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            "INSERT INTO Cuentas (UsuarioId, Nombre, Tipo, Banco, Activa, SaldoInicial) VALUES ($uid,$n,$t,$b,$a,$si)",
            ("$uid", usuarioId), ("$n", c.Nombre), ("$t", c.Tipo),
            ("$b", c.Banco), ("$a", c.Activa ? 1 : 0), ("$si", (double)c.SaldoInicial));
    }

    public void ActualizarCuenta(Cuenta c)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            "UPDATE Cuentas SET Nombre=$n, Tipo=$t, Banco=$b, Activa=$a, SaldoInicial=$si WHERE Id=$id",
            ("$n", c.Nombre), ("$t", c.Tipo), ("$b", c.Banco),
            ("$a", c.Activa ? 1 : 0), ("$si", (double)c.SaldoInicial), ("$id", c.Id));
    }

    public void EliminarCuenta(int id)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn, "DELETE FROM Cuentas WHERE Id=$id", ("$id", id));
    }

    public void AbonarTarjeta(int tarjetaId, decimal monto)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            "UPDATE TarjetasCredito SET SaldoUsado = MAX(0, SaldoUsado - $m) WHERE Id=$id",
            ("$m", (double)monto), ("$id", tarjetaId));
    }

    // ── Gastos Fijos ──────────────────────────────────────────────────────────

    public List<GastoFijo> ObtenerGastosFijosConEstado(int anio, int mes, int usuarioId)
    {
        using var conn = Abrir();
        AsegurarPagosMensualesGasto(conn, anio, mes, usuarioId);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT g.Id, g.Nombre, g.Monto, g.DiaVencimiento AS Dia, g.DiaVencimiento2, g.Activo,
                   COALESCE(p.Pagado, 0), COALESCE(p.Id, 0), COALESCE(p.MetodoPago, '')
            FROM GastosFijos g
            LEFT JOIN PagosMensuales p
                ON p.FijoId=g.Id AND p.TipoFijo='Gasto' AND p.Anio=$a AND p.Mes=$m AND p.Dia=g.DiaVencimiento AND p.UsuarioId=$uid
            WHERE g.Activo=1 AND g.UsuarioId=$uid

            UNION ALL

            SELECT g.Id, g.Nombre, g.Monto, g.DiaVencimiento2 AS Dia, 0, g.Activo,
                   COALESCE(p.Pagado, 0), COALESCE(p.Id, 0), COALESCE(p.MetodoPago, '')
            FROM GastosFijos g
            LEFT JOIN PagosMensuales p
                ON p.FijoId=g.Id AND p.TipoFijo='Gasto' AND p.Anio=$a AND p.Mes=$m AND p.Dia=g.DiaVencimiento2 AND p.UsuarioId=$uid
            WHERE g.Activo=1 AND g.DiaVencimiento2 > 0 AND g.UsuarioId=$uid

            ORDER BY Dia
            """;
        cmd.Parameters.AddWithValue("$a",   anio);
        cmd.Parameters.AddWithValue("$m",   mes);
        cmd.Parameters.AddWithValue("$uid", usuarioId);

        var list = new List<GastoFijo>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new GastoFijo
            {
                Id              = r.GetInt32(0),
                Nombre          = r.GetString(1),
                Monto           = (decimal)r.GetDouble(2),
                DiaEspecifico   = r.GetInt32(3),
                DiaVencimiento2 = r.GetInt32(4),
                Activo          = r.GetInt32(5) == 1,
                PagadoEsteMes   = r.GetInt32(6) == 1,
                PagoMensualId   = r.GetInt32(7),
                MetodoPago      = r.IsDBNull(8) ? "" : r.GetString(8)
            });
        return list;
    }

    public void InsertarGastoFijo(GastoFijo g, int usuarioId)
    {
        using var conn = Abrir();

        // Dos días iguales generarían dos filas en la interfaz compartiendo el MISMO
        // PagoMensual (la clave única incluye Dia): marcar una marcaría las dos y el total
        // se contaría por duplicado. Un segundo día repetido es, sencillamente, no tener segundo día.
        var dia2 = g.DiaVencimiento2 == g.DiaVencimiento ? 0 : g.DiaVencimiento2;

        EjecutarNonQuery(conn,
            "INSERT INTO GastosFijos (UsuarioId,Nombre,Monto,DiaVencimiento,DiaVencimiento2) VALUES ($uid,$n,$m,$d,$d2)",
            ("$uid", usuarioId), ("$n", g.Nombre), ("$m", (double)g.Monto), ("$d", g.DiaVencimiento), ("$d2", dia2));
    }

    public void EliminarGastoFijo(int id)
    {
        using var conn = Abrir();

        // Revertir saldos y borrar transacciones generadas por pagos marcados
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT p.TransaccionId, t.Monto, t.CuentaId, t.TarjetaCreditoId
                FROM PagosMensuales p
                LEFT JOIN Transacciones t ON t.Id = p.TransaccionId
                WHERE p.FijoId = $id AND p.TipoFijo = 'Gasto'
                  AND p.Pagado = 1 AND p.TransaccionId IS NOT NULL
                """;
            cmd.Parameters.AddWithValue("$id", id);
            var pagos = new List<(long TransId, double Monto, int? CuentaId, int? TarjetaId)>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                pagos.Add((r.GetInt64(0), r.GetDouble(1),
                           r.IsDBNull(2) ? null : r.GetInt32(2),
                           r.IsDBNull(3) ? null : r.GetInt32(3)));

            foreach (var (transId, monto, cuentaId, tarjetaId) in pagos)
            {
                EjecutarNonQuery(conn, "DELETE FROM Transacciones WHERE Id=$tid", ("$tid", transId));
                if (tarjetaId.HasValue)
                    EjecutarNonQuery(conn,
                        "UPDATE TarjetasCredito SET SaldoUsado = SaldoUsado - $m WHERE Id=$cid",
                        ("$m", monto), ("$cid", tarjetaId.Value));
            }
        }

        EjecutarNonQuery(conn, "DELETE FROM PagosMensuales WHERE TipoFijo='Gasto' AND FijoId=$id", ("$id", id));
        EjecutarNonQuery(conn, "DELETE FROM GastosFijos WHERE Id=$id", ("$id", id));
    }

    public void MarcarGastoFijoPagado(int pagoMensualId, bool pagado, string metodoPago,
                                       int usuarioId, int? cuentaId = null, int? tarjetaId = null)
    {
        using var conn = Abrir();

        if (pagado)
        {
            // Leer datos del gasto fijo
            GastoFijo? datos = null;
            int anio = 0, mes = 0, dia = 0;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT g.Id, g.Nombre, g.Monto, p.Anio, p.Mes, p.Dia
                    FROM PagosMensuales p
                    JOIN GastosFijos g ON g.Id = p.FijoId
                    WHERE p.Id = $id
                    """;
                cmd.Parameters.AddWithValue("$id", pagoMensualId);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    datos = new GastoFijo { Id = r.GetInt32(0), Nombre = r.GetString(1), Monto = (decimal)r.GetDouble(2) };
                    anio  = r.GetInt32(3);
                    mes   = r.GetInt32(4);
                    dia   = r.GetInt32(5);
                }
            }
            if (datos is null) return;

            var fecha = new DateTime(anio, mes, Math.Min(dia, DateTime.DaysInMonth(anio, mes)));

            // Crear transacción con CuentaId y TarjetaCreditoId correctamente enlazados
            using var cmdIns = conn.CreateCommand();
            cmdIns.CommandText = """
                INSERT INTO Transacciones
                    (UsuarioId, Tipo, Monto, Categoria, Descripcion, Fecha, Notas, CuentaNombre, CuentaId, TarjetaCreditoId)
                VALUES
                    ($uid, 'Gasto', $monto, 'Gastos Fijos', $desc, $fecha, 'Pago automático de gasto fijo', $cuenta, $cuentaid, $tarjeta);
                SELECT last_insert_rowid();
                """;
            cmdIns.Parameters.AddWithValue("$uid",      usuarioId);
            cmdIns.Parameters.AddWithValue("$monto",    (double)datos.Monto);
            cmdIns.Parameters.AddWithValue("$desc",     datos.Nombre);
            cmdIns.Parameters.AddWithValue("$fecha",    fecha.ToString("yyyy-MM-dd"));
            cmdIns.Parameters.AddWithValue("$cuenta",   metodoPago);
            cmdIns.Parameters.AddWithValue("$cuentaid", cuentaId.HasValue ? cuentaId.Value : DBNull.Value);
            cmdIns.Parameters.AddWithValue("$tarjeta",  tarjetaId.HasValue ? tarjetaId.Value : DBNull.Value);
            var newTransId = (long)cmdIns.ExecuteScalar()!;

            // Guardar TransaccionId en PagosMensuales
            EjecutarNonQuery(conn,
                "UPDATE PagosMensuales SET Pagado=1, MetodoPago=$m, TransaccionId=$tid WHERE Id=$id",
                ("$m", metodoPago), ("$tid", newTransId), ("$id", pagoMensualId));

            // Actualizar saldo de tarjeta de crédito
            if (tarjetaId.HasValue)
                EjecutarNonQuery(conn,
                    "UPDATE TarjetasCredito SET SaldoUsado = SaldoUsado + $m WHERE Id=$id",
                    ("$m", (double)datos.Monto), ("$id", tarjetaId.Value));

        }
        else
        {
            // Leer TransaccionId y datos de saldo desde la transacción original
            long?  prevTransId  = null;
            int?   prevCuentaId = null;
            int?   prevTarjetaId = null;
            double prevMonto    = 0;

            using (var cmdSel = conn.CreateCommand())
            {
                cmdSel.CommandText = """
                    SELECT p.TransaccionId, t.Monto, t.CuentaId, t.TarjetaCreditoId
                    FROM PagosMensuales p
                    LEFT JOIN Transacciones t ON t.Id = p.TransaccionId
                    WHERE p.Id = $id
                    """;
                cmdSel.Parameters.AddWithValue("$id", pagoMensualId);
                using var r = cmdSel.ExecuteReader();
                if (r.Read())
                {
                    prevTransId   = r.IsDBNull(0) ? null : r.GetInt64(0);
                    prevMonto     = r.IsDBNull(1) ? 0    : r.GetDouble(1);
                    prevCuentaId  = r.IsDBNull(2) ? null : r.GetInt32(2);
                    prevTarjetaId = r.IsDBNull(3) ? null : r.GetInt32(3);
                }
            }

            if (prevTransId.HasValue)
                EjecutarNonQuery(conn, "DELETE FROM Transacciones WHERE Id=$id", ("$id", prevTransId.Value));

            if (prevTarjetaId.HasValue)
                EjecutarNonQuery(conn,
                    "UPDATE TarjetasCredito SET SaldoUsado = SaldoUsado - $m WHERE Id=$id",
                    ("$m", prevMonto), ("$id", prevTarjetaId.Value));


            EjecutarNonQuery(conn,
                "UPDATE PagosMensuales SET Pagado=0, MetodoPago='', TransaccionId=NULL WHERE Id=$id",
                ("$id", pagoMensualId));
        }
    }

    // ── Ingresos Fijos ────────────────────────────────────────────────────────

    public List<IngresoFijo> ObtenerIngresosFijosConEstado(int anio, int mes, int usuarioId)
    {
        using var conn = Abrir();
        AsegurarPagosMensualesIngreso(conn, anio, mes, usuarioId);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT i.Id, i.Nombre, i.Monto, i.DiaIngreso AS Dia, i.DiaIngreso2, i.Activo,
                   COALESCE(p.Pagado, 0), COALESCE(p.Id, 0)
            FROM IngresosFijos i
            LEFT JOIN PagosMensuales p
                ON p.FijoId=i.Id AND p.TipoFijo='Ingreso' AND p.Anio=$a AND p.Mes=$m AND p.Dia=i.DiaIngreso AND p.UsuarioId=$uid
            WHERE i.Activo=1 AND i.UsuarioId=$uid

            UNION ALL

            SELECT i.Id, i.Nombre, i.Monto, i.DiaIngreso2 AS Dia, 0, i.Activo,
                   COALESCE(p.Pagado, 0), COALESCE(p.Id, 0)
            FROM IngresosFijos i
            LEFT JOIN PagosMensuales p
                ON p.FijoId=i.Id AND p.TipoFijo='Ingreso' AND p.Anio=$a AND p.Mes=$m AND p.Dia=i.DiaIngreso2 AND p.UsuarioId=$uid
            WHERE i.Activo=1 AND i.DiaIngreso2 > 0 AND i.UsuarioId=$uid

            ORDER BY Dia
            """;
        cmd.Parameters.AddWithValue("$a",   anio);
        cmd.Parameters.AddWithValue("$m",   mes);
        cmd.Parameters.AddWithValue("$uid", usuarioId);

        var list = new List<IngresoFijo>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new IngresoFijo
            {
                Id              = r.GetInt32(0),
                Nombre          = r.GetString(1),
                Monto           = (decimal)r.GetDouble(2),
                DiaEspecifico   = r.GetInt32(3),
                DiaIngreso2     = r.GetInt32(4),
                Activo          = r.GetInt32(5) == 1,
                RecibidoEsteMes = r.GetInt32(6) == 1,
                PagoMensualId   = r.GetInt32(7)
            });
        return list;
    }

    public void InsertarIngresoFijo(IngresoFijo i, int usuarioId)
    {
        using var conn = Abrir();

        // Ver la nota en InsertarGastoFijo: dos días iguales comparten PagoMensual y duplican.
        var dia2 = i.DiaIngreso2 == i.DiaIngreso ? 0 : i.DiaIngreso2;

        EjecutarNonQuery(conn,
            "INSERT INTO IngresosFijos (UsuarioId,Nombre,Monto,DiaIngreso,DiaIngreso2,CuentaId) VALUES ($uid,$n,$m,$d,$d2,$cid)",
            ("$uid", usuarioId), ("$n", i.Nombre), ("$m", (double)i.Monto),
            ("$d", i.DiaIngreso), ("$d2", dia2),
            ("$cid", i.CuentaId.HasValue ? i.CuentaId.Value : DBNull.Value));
    }

    public void EliminarIngresoFijo(int id)
    {
        using var conn = Abrir();

        // Revertir saldo por pagos recibidos y borrar sus transacciones
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT p.TransaccionId, i.Monto, i.CuentaId
                FROM PagosMensuales p
                JOIN IngresosFijos i ON i.Id = p.FijoId
                WHERE p.FijoId=$id AND p.TipoFijo='Ingreso' AND p.Pagado=1
                  AND p.TransaccionId IS NOT NULL
                """;
            cmd.Parameters.AddWithValue("$id", id);
            var pagos = new List<(long TransId, double Monto, int? CuentaId)>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                pagos.Add((r.GetInt64(0), r.GetDouble(1), r.IsDBNull(2) ? null : r.GetInt32(2)));

            foreach (var (transId, monto, cuentaId) in pagos)
            {
                EjecutarNonQuery(conn, "DELETE FROM Transacciones WHERE Id=$tid", ("$tid", transId));
            }
        }

        EjecutarNonQuery(conn, "DELETE FROM PagosMensuales WHERE TipoFijo='Ingreso' AND FijoId=$id", ("$id", id));
        EjecutarNonQuery(conn, "DELETE FROM IngresosFijos WHERE Id=$id", ("$id", id));
    }

    public void MarcarIngresoFijoRecibido(int pagoMensualId, bool recibido, int usuarioId)
    {
        using var conn = Abrir();

        if (recibido)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT i.Nombre, i.Monto, p.Anio, p.Mes, p.Dia, i.CuentaId
                FROM PagosMensuales p
                JOIN IngresosFijos i ON i.Id = p.FijoId
                WHERE p.Id = $id
                """;
            cmd.Parameters.AddWithValue("$id", pagoMensualId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return;

            var nombre   = r.GetString(0);
            var monto    = (decimal)r.GetDouble(1);
            int anio     = r.GetInt32(2);
            int mes      = r.GetInt32(3);
            int dia      = r.GetInt32(4);
            var cuentaId = r.IsDBNull(5) ? (int?)null : r.GetInt32(5);
            r.Close();

            var fecha        = new DateTime(anio, mes, Math.Min(dia, DateTime.DaysInMonth(anio, mes)));
            string cuentaNombre = "Efectivo";
            if (cuentaId.HasValue)
            {
                using var cmdCuenta = conn.CreateCommand();
                cmdCuenta.CommandText = "SELECT Nombre FROM Cuentas WHERE Id=$id";
                cmdCuenta.Parameters.AddWithValue("$id", cuentaId.Value);
                cuentaNombre = cmdCuenta.ExecuteScalar()?.ToString() ?? "Efectivo";
            }

            using var cmdIns = conn.CreateCommand();
            cmdIns.CommandText = """
                INSERT INTO Transacciones
                    (UsuarioId, Tipo, Monto, Categoria, Descripcion, Fecha, Notas, CuentaNombre, CuentaId)
                VALUES
                    ($uid, 'Ingreso', $monto, 'Ingresos Fijos', $desc, $fecha, 'Ingreso fijo recibido', $cuentanombre, $cuentaid);
                SELECT last_insert_rowid();
                """;
            cmdIns.Parameters.AddWithValue("$uid",         usuarioId);
            cmdIns.Parameters.AddWithValue("$monto",       (double)monto);
            cmdIns.Parameters.AddWithValue("$desc",        nombre);
            cmdIns.Parameters.AddWithValue("$fecha",       fecha.ToString("yyyy-MM-dd"));
            cmdIns.Parameters.AddWithValue("$cuentanombre", cuentaNombre);
            cmdIns.Parameters.AddWithValue("$cuentaid",    cuentaId.HasValue ? cuentaId.Value : DBNull.Value);
            var newId = (long)cmdIns.ExecuteScalar()!;

            EjecutarNonQuery(conn,
                "UPDATE PagosMensuales SET Pagado=1, TransaccionId=$tid WHERE Id=$id",
                ("$tid", newId), ("$id", pagoMensualId));

        }
        else
        {
            // Obtener TransaccionId y datos para revertir saldo
            using var cmdSel = conn.CreateCommand();
            cmdSel.CommandText = """
                SELECT p.TransaccionId, i.Monto, i.CuentaId
                FROM PagosMensuales p
                JOIN IngresosFijos i ON i.Id = p.FijoId
                WHERE p.Id = $id
                """;
            cmdSel.Parameters.AddWithValue("$id", pagoMensualId);
            using var r2 = cmdSel.ExecuteReader();
            long?  tid      = null;
            int?   cuentaId = null;
            double monto    = 0;
            if (r2.Read())
            {
                tid      = r2.IsDBNull(0) ? null : r2.GetInt64(0);
                monto    = r2.GetDouble(1);
                cuentaId = r2.IsDBNull(2) ? null : r2.GetInt32(2);
            }
            r2.Close();

            if (tid.HasValue)
                EjecutarNonQuery(conn, "DELETE FROM Transacciones WHERE Id=$id", ("$id", tid.Value));


            EjecutarNonQuery(conn,
                "UPDATE PagosMensuales SET Pagado=0, TransaccionId=NULL WHERE Id=$id",
                ("$id", pagoMensualId));
        }
    }

    private static void AsegurarPagosMensualesGasto(SqliteConnection conn, int anio, int mes, int usuarioId)
    {
        using var c1 = conn.CreateCommand();
        c1.CommandText = $"""
            INSERT OR IGNORE INTO PagosMensuales (UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia, Pagado)
            SELECT {usuarioId}, 'Gasto', Id, {anio}, {mes}, DiaVencimiento, 0
            FROM GastosFijos WHERE Activo=1 AND UsuarioId={usuarioId}
            """;
        c1.ExecuteNonQuery();

        using var c2 = conn.CreateCommand();
        c2.CommandText = $"""
            INSERT OR IGNORE INTO PagosMensuales (UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia, Pagado)
            SELECT {usuarioId}, 'Gasto', Id, {anio}, {mes}, DiaVencimiento2, 0
            FROM GastosFijos WHERE Activo=1 AND DiaVencimiento2 > 0 AND UsuarioId={usuarioId}
            """;
        c2.ExecuteNonQuery();
    }

    private static void AsegurarPagosMensualesIngreso(SqliteConnection conn, int anio, int mes, int usuarioId)
    {
        using var c1 = conn.CreateCommand();
        c1.CommandText = $"""
            INSERT OR IGNORE INTO PagosMensuales (UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia, Pagado)
            SELECT {usuarioId}, 'Ingreso', Id, {anio}, {mes}, DiaIngreso, 0
            FROM IngresosFijos WHERE Activo=1 AND UsuarioId={usuarioId}
            """;
        c1.ExecuteNonQuery();

        using var c2 = conn.CreateCommand();
        c2.CommandText = $"""
            INSERT OR IGNORE INTO PagosMensuales (UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia, Pagado)
            SELECT {usuarioId}, 'Ingreso', Id, {anio}, {mes}, DiaIngreso2, 0
            FROM IngresosFijos WHERE Activo=1 AND DiaIngreso2 > 0 AND UsuarioId={usuarioId}
            """;
        c2.ExecuteNonQuery();
    }

    // ── Reportes ──────────────────────────────────────────────────────────────

    public List<BarMonth> ObtenerDatosMensuales(int usuarioId, int anios = 1)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        var desde = DateTime.Today.AddMonths(-(anios * 12 - 1));
        cmd.CommandText = """
            SELECT strftime('%Y-%m', Fecha) AS Periodo,
                   SUM(CASE WHEN Tipo='Ingreso' THEN Monto ELSE 0 END) AS Ingresos,
                   SUM(CASE WHEN Tipo='Gasto'   THEN Monto ELSE 0 END) AS Gastos
            FROM Transacciones
            WHERE Fecha >= $desde AND UsuarioId = $uid
              AND PagoDeTarjetaId IS NULL   -- eje presupuesto: en qué se gastó
            GROUP BY Periodo
            ORDER BY Periodo
            """;
        cmd.Parameters.AddWithValue("$desde", desde.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$uid",   usuarioId);

        var list = new List<BarMonth>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var periodo = r.GetString(0);
            var parts   = periodo.Split('-');
            var label   = $"{NombreMes(int.Parse(parts[1]))}\n{parts[0]}";
            list.Add(new BarMonth
            {
                MesLabel = label,
                Ingresos = r.IsDBNull(1) ? 0 : r.GetDouble(1),
                Gastos   = r.IsDBNull(2) ? 0 : r.GetDouble(2)
            });
        }
        return list;
    }

    private static string NombreMes(int m) => m switch
    {
        1 => "Ene", 2 => "Feb", 3 => "Mar", 4 => "Abr",
        5 => "May", 6 => "Jun", 7 => "Jul", 8 => "Ago",
        9 => "Set", 10 => "Oct", 11 => "Nov", _ => "Dic"
    };

    // ── Control Laboral ───────────────────────────────────────────────────────

    private static void InicializarControlLaboral(SqliteConnection conn)
    {
        EjecutarNonQuery(conn, """
            CREATE TABLE IF NOT EXISTS ConfiguracionLaboral (
                UsuarioId      INTEGER PRIMARY KEY,
                SalarioPorHora REAL    NOT NULL DEFAULT 0,
                JornadaSemanal REAL    NOT NULL DEFAULT 48,
                DiaPago        INTEGER NOT NULL DEFAULT 15
            );
            CREATE TABLE IF NOT EXISTS RegistrosDiasLaborales (
                Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                UsuarioId           INTEGER NOT NULL,
                Fecha               TEXT    NOT NULL,
                HorasNormales       REAL    NOT NULL DEFAULT 0,
                HorasExtraDiurnas   REAL    NOT NULL DEFAULT 0,
                HorasExtraNocturnas REAL    NOT NULL DEFAULT 0,
                HorasDobles         REAL    NOT NULL DEFAULT 0,
                EsFeriado           INTEGER NOT NULL DEFAULT 0,
                EsAusencia          INTEGER NOT NULL DEFAULT 0,
                TieneGoceSalario    INTEGER NOT NULL DEFAULT 0,
                Viaticos            REAL    NOT NULL DEFAULT 0,
                UNIQUE(UsuarioId, Fecha)
            );
            CREATE TABLE IF NOT EXISTS PeriodosLaborales (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                UsuarioId     INTEGER NOT NULL,
                Anio          INTEGER NOT NULL,
                Mes           INTEGER NOT NULL,
                SalarioBruto  REAL    NOT NULL DEFAULT 0,
                Deducciones   REAL    NOT NULL DEFAULT 0,
                SalarioNeto   REAL    NOT NULL DEFAULT 0,
                Cerrado       INTEGER NOT NULL DEFAULT 0,
                TransaccionId INTEGER,
                UNIQUE(UsuarioId, Anio, Mes)
            )
            """);

        // Migrar columnas nuevas en ConfiguracionLaboral (instalaciones existentes)
        var cfgCols = GetColumnas(conn, "ConfiguracionLaboral");
        if (!cfgCols.Contains("ModoPago"))  AddColumna(conn, "ConfiguracionLaboral", "ModoPago",  "TEXT    NOT NULL DEFAULT 'Mensual'");
        if (!cfgCols.Contains("DiaPago2"))  AddColumna(conn, "ConfiguracionLaboral", "DiaPago2",  "INTEGER NOT NULL DEFAULT 0");
        if (!cfgCols.Contains("DiaSemana")) AddColumna(conn, "ConfiguracionLaboral", "DiaSemana", "INTEGER NOT NULL DEFAULT 5");
        if (!cfgCols.Contains("Viaticos"))  AddColumna(conn, "ConfiguracionLaboral", "Viaticos",  "REAL    NOT NULL DEFAULT 0");

        // Migrar PeriodosLaborales
        var plCols = GetColumnas(conn, "PeriodosLaborales");
        if (!plCols.Contains("Notas")) AddColumna(conn, "PeriodosLaborales", "Notas", "TEXT NOT NULL DEFAULT ''");

        // Tabla de ingresos laborales directos (sin necesidad de cerrar período)
        EjecutarNonQuery(conn, """
            CREATE TABLE IF NOT EXISTS IngresoLaboralDirecto (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                UsuarioId     INTEGER NOT NULL,
                Fecha         TEXT    NOT NULL,
                Monto         REAL    NOT NULL DEFAULT 0,
                Descripcion   TEXT    NOT NULL DEFAULT '',
                CuentaId      INTEGER,
                TransaccionId INTEGER
            )
            """);
    }

    public ConfiguracionLaboral ObtenerConfiguracionLaboral(int usuarioId)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT SalarioPorHora, JornadaSemanal, DiaPago, ModoPago, DiaPago2, DiaSemana, Viaticos
            FROM ConfiguracionLaboral WHERE UsuarioId=$uid
            """;
        cmd.Parameters.AddWithValue("$uid", usuarioId);
        using var r = cmd.ExecuteReader();
        if (r.Read())
            return new ConfiguracionLaboral
            {
                UsuarioId      = usuarioId,
                SalarioPorHora = (decimal)r.GetDouble(0),
                JornadaSemanal = (decimal)r.GetDouble(1),
                DiaPago        = r.GetInt32(2),
                ModoPago       = r.IsDBNull(3) ? "Mensual" : r.GetString(3),
                DiaPago2       = r.IsDBNull(4) ? 0         : r.GetInt32(4),
                DiaSemana      = r.IsDBNull(5) ? 5         : r.GetInt32(5),
                Viaticos       = r.IsDBNull(6) ? 0m        : (decimal)r.GetDouble(6)
            };
        return new ConfiguracionLaboral { UsuarioId = usuarioId };
    }

    public void GuardarConfiguracionLaboral(ConfiguracionLaboral c)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            """
            INSERT INTO ConfiguracionLaboral (UsuarioId, SalarioPorHora, JornadaSemanal, DiaPago, ModoPago, DiaPago2, DiaSemana, Viaticos)
            VALUES ($uid,$sph,$js,$dp,$mp,$dp2,$ds,$vt)
            ON CONFLICT(UsuarioId) DO UPDATE SET
                SalarioPorHora=$sph, JornadaSemanal=$js, DiaPago=$dp,
                ModoPago=$mp, DiaPago2=$dp2, DiaSemana=$ds, Viaticos=$vt
            """,
            ("$uid", c.UsuarioId), ("$sph", (double)c.SalarioPorHora),
            ("$js",  (double)c.JornadaSemanal), ("$dp",  c.DiaPago),
            ("$mp",  c.ModoPago),       ("$dp2", c.DiaPago2),
            ("$ds",  c.DiaSemana),      ("$vt",  (double)c.Viaticos));
    }

    public List<RegistroDiaLaboral> ObtenerRegistrosDia(int usuarioId, int anio, int mes)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Fecha, HorasNormales, HorasExtraDiurnas, HorasExtraNocturnas,
                   HorasDobles, EsFeriado, EsAusencia, TieneGoceSalario, Viaticos
            FROM RegistrosDiasLaborales
            WHERE UsuarioId=$uid AND strftime('%Y',$col)=$anio AND strftime('%m',$col)=$mes
            ORDER BY Fecha
            """;
        // trick: bind Fecha column literal separately from the date filter values
        cmd.CommandText = """
            SELECT Id, Fecha, HorasNormales, HorasExtraDiurnas, HorasExtraNocturnas,
                   HorasDobles, EsFeriado, EsAusencia, TieneGoceSalario, Viaticos
            FROM RegistrosDiasLaborales
            WHERE UsuarioId=$uid
              AND Fecha >= $inicio AND Fecha <= $fin
            ORDER BY Fecha
            """;
        var inicio = new DateTime(anio, mes, 1).ToString("yyyy-MM-dd");
        var fin    = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes)).ToString("yyyy-MM-dd");
        cmd.Parameters.AddWithValue("$uid",   usuarioId);
        cmd.Parameters.AddWithValue("$inicio", inicio);
        cmd.Parameters.AddWithValue("$fin",    fin);

        var list = new List<RegistroDiaLaboral>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new RegistroDiaLaboral
            {
                Id                  = r.GetInt32(0),
                UsuarioId           = usuarioId,
                Fecha               = DateTime.Parse(r.GetString(1)),
                HorasNormales       = r.GetDouble(2),
                HorasExtraDiurnas   = r.GetDouble(3),
                HorasExtraNocturnas = r.GetDouble(4),
                HorasDobles         = r.GetDouble(5),
                EsFeriado           = r.GetInt32(6) == 1,
                EsAusencia          = r.GetInt32(7) == 1,
                TieneGoceSalario    = r.GetInt32(8) == 1,
                Viaticos            = (decimal)r.GetDouble(9)
            });
        return list;
    }

    public void InsertarOActualizarRegistroDia(RegistroDiaLaboral reg)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            """
            INSERT INTO RegistrosDiasLaborales
                (UsuarioId, Fecha, HorasNormales, HorasExtraDiurnas, HorasExtraNocturnas,
                 HorasDobles, EsFeriado, EsAusencia, TieneGoceSalario, Viaticos)
            VALUES ($uid, $f, $hn, $hed, $hen, $hd, $ef, $ea, $tg, $vt)
            ON CONFLICT(UsuarioId, Fecha) DO UPDATE SET
                HorasNormales=$hn, HorasExtraDiurnas=$hed, HorasExtraNocturnas=$hen,
                HorasDobles=$hd, EsFeriado=$ef, EsAusencia=$ea, TieneGoceSalario=$tg, Viaticos=$vt
            """,
            ("$uid", reg.UsuarioId),
            ("$f",   reg.Fecha.ToString("yyyy-MM-dd")),
            ("$hn",  reg.HorasNormales),
            ("$hed", reg.HorasExtraDiurnas),
            ("$hen", reg.HorasExtraNocturnas),
            ("$hd",  reg.HorasDobles),
            ("$ef",  reg.EsFeriado   ? 1 : 0),
            ("$ea",  reg.EsAusencia  ? 1 : 0),
            ("$tg",  reg.TieneGoceSalario ? 1 : 0),
            ("$vt",  (double)reg.Viaticos));
    }

    public void EliminarRegistroDia(int id)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn, "DELETE FROM RegistrosDiasLaborales WHERE Id=$id", ("$id", id));
    }

    public List<PeriodoLaboral> ObtenerPeriodosLaborales(int usuarioId)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Anio, Mes, SalarioBruto, Deducciones, SalarioNeto, Cerrado, TransaccionId, Notas
            FROM PeriodosLaborales
            WHERE UsuarioId=$uid
            ORDER BY Anio DESC, Mes DESC
            """;
        cmd.Parameters.AddWithValue("$uid", usuarioId);
        var list = new List<PeriodoLaboral>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new PeriodoLaboral
            {
                Id            = r.GetInt32(0),
                UsuarioId     = usuarioId,
                Anio          = r.GetInt32(1),
                Mes           = r.GetInt32(2),
                SalarioBruto  = (decimal)r.GetDouble(3),
                Deducciones   = (decimal)r.GetDouble(4),
                SalarioNeto   = (decimal)r.GetDouble(5),
                Cerrado       = r.GetInt32(6) == 1,
                TransaccionId = r.IsDBNull(7) ? null : r.GetInt32(7),
                Notas         = r.IsDBNull(8) ? "" : r.GetString(8)
            });
        return list;
    }

    // ── Cierre mensual ───────────────────────────────────────────────────────

    private static void InicializarResumenMensual(SqliteConnection conn)
    {
        EjecutarNonQuery(conn, """
            CREATE TABLE IF NOT EXISTS ResumenMensual (
                Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                UsuarioId        INTEGER NOT NULL,
                Anio             INTEGER NOT NULL,
                Mes              INTEGER NOT NULL,
                Ingresos         REAL    NOT NULL DEFAULT 0,
                Gastos           REAL    NOT NULL DEFAULT 0,
                Balance          REAL    NOT NULL DEFAULT 0,
                BalanceAcumulado REAL    NOT NULL DEFAULT 0,
                NumTransacciones INTEGER NOT NULL DEFAULT 0,
                PeriodoLaboral   INTEGER NOT NULL DEFAULT 0,
                FechaCierre      TEXT    NOT NULL DEFAULT '',
                Notas            TEXT    NOT NULL DEFAULT '',
                UNIQUE(UsuarioId, Anio, Mes)
            )
            """);
    }

    /// <summary>Devuelve los totales de transacciones del mes para el preview de confirmación.</summary>
    public (decimal Ingresos, decimal Gastos, int NumTrans) ObtenerTotalesMes(
        int usuarioId, int anio, int mes)
    {
        using var conn = Abrir();
        var inicio = new DateTime(anio, mes, 1);
        var fin    = inicio.AddMonths(1).AddDays(-1);

        decimal ingresos = 0, gastos = 0;
        int numTrans = 0;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Tipo, SUM(Monto), COUNT(*)
            FROM Transacciones
            WHERE UsuarioId=$uid
              AND PagoDeTarjetaId IS NULL   -- eje presupuesto: qué se ganó y en qué se gastó
              AND Fecha >= $desde AND Fecha <= $hasta
            GROUP BY Tipo
            """;
        cmd.Parameters.AddWithValue("$uid",   usuarioId);
        cmd.Parameters.AddWithValue("$desde", inicio.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$hasta", fin.ToString("yyyy-MM-dd"));

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            int c = r.GetInt32(2);
            if (r.GetString(0) == "Ingreso") { ingresos = (decimal)r.GetDouble(1); numTrans += c; }
            else                             { gastos   = (decimal)r.GetDouble(1); numTrans += c; }
        }
        return (ingresos, gastos, numTrans);
    }

    /// <summary>Genera el snapshot del mes y lo guarda en ResumenMensual. Idempotente.</summary>
    public void CerrarMes(int usuarioId, int anio, int mes, string notas = "")
    {
        using var conn = Abrir();
        var inicio   = new DateTime(anio, mes, 1);
        var fin      = inicio.AddMonths(1).AddDays(-1);
        string desde = inicio.ToString("yyyy-MM-dd");
        string hasta = fin.ToString("yyyy-MM-dd");

        // Ingresos / gastos del mes
        decimal ingresos = 0, gastos = 0;
        int numTrans = 0;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT Tipo, SUM(Monto), COUNT(*)
                FROM Transacciones
                WHERE UsuarioId=$uid
                  AND PagoDeTarjetaId IS NULL   -- eje presupuesto
                  AND Fecha >= $desde AND Fecha <= $hasta
                GROUP BY Tipo
                """;
            cmd.Parameters.AddWithValue("$uid",   usuarioId);
            cmd.Parameters.AddWithValue("$desde", desde);
            cmd.Parameters.AddWithValue("$hasta", hasta);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int c = r.GetInt32(2);
                if (r.GetString(0) == "Ingreso") { ingresos = (decimal)r.GetDouble(1); numTrans += c; }
                else                             { gastos   = (decimal)r.GetDouble(1); numTrans += c; }
            }
        }

        // Balance acumulado al final del mes
        decimal balanceAcum;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COALESCE(SUM(CASE WHEN Tipo='Ingreso' THEN Monto ELSE -Monto END), 0)
                FROM Transacciones
                WHERE UsuarioId=$uid AND Fecha <= $hasta
                  AND TarjetaCreditoId IS NULL   -- eje efectivo: cuánto quedó de verdad
                """;
            cmd.Parameters.AddWithValue("$uid",   usuarioId);
            cmd.Parameters.AddWithValue("$hasta", hasta);
            balanceAcum = (decimal)Convert.ToDouble(cmd.ExecuteScalar() ?? 0.0);
        }

        // ¿El período laboral de ese mes está cerrado?
        bool periodoLaboral;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COUNT(*) FROM PeriodosLaborales
                WHERE UsuarioId=$uid AND Anio=$anio AND Mes=$mes AND Cerrado=1
                """;
            cmd.Parameters.AddWithValue("$uid",  usuarioId);
            cmd.Parameters.AddWithValue("$anio", anio);
            cmd.Parameters.AddWithValue("$mes",  mes);
            periodoLaboral = (long)cmd.ExecuteScalar()! > 0;
        }

        EjecutarNonQuery(conn, """
            INSERT INTO ResumenMensual
                (UsuarioId, Anio, Mes, Ingresos, Gastos, Balance, BalanceAcumulado,
                 NumTransacciones, PeriodoLaboral, FechaCierre, Notas)
            VALUES ($uid,$anio,$mes,$ing,$gas,$bal,$balAcum,$nt,$pl,$fc,$notas)
            ON CONFLICT(UsuarioId, Anio, Mes) DO UPDATE SET
                Ingresos=$ing, Gastos=$gas, Balance=$bal, BalanceAcumulado=$balAcum,
                NumTransacciones=$nt, PeriodoLaboral=$pl, FechaCierre=$fc, Notas=$notas
            """,
            ("$uid",    usuarioId),
            ("$anio",   anio),
            ("$mes",    mes),
            ("$ing",    (double)ingresos),
            ("$gas",    (double)gastos),
            ("$bal",    (double)(ingresos - gastos)),
            ("$balAcum",(double)balanceAcum),
            ("$nt",     numTrans),
            ("$pl",     periodoLaboral ? 1 : 0),
            ("$fc",     DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
            ("$notas",  notas));
    }

    public List<ResumenMensual> ObtenerResumenesMensuales(int usuarioId)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Anio, Mes, Ingresos, Gastos, Balance, BalanceAcumulado,
                   NumTransacciones, PeriodoLaboral, FechaCierre, Notas
            FROM ResumenMensual
            WHERE UsuarioId=$uid
            ORDER BY Anio DESC, Mes DESC
            """;
        cmd.Parameters.AddWithValue("$uid", usuarioId);

        var list = new List<ResumenMensual>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new ResumenMensual
            {
                Id               = r.GetInt32(0),
                UsuarioId        = usuarioId,
                Anio             = r.GetInt32(1),
                Mes              = r.GetInt32(2),
                Ingresos         = (decimal)r.GetDouble(3),
                Gastos           = (decimal)r.GetDouble(4),
                Balance          = (decimal)r.GetDouble(5),
                BalanceAcumulado = (decimal)r.GetDouble(6),
                NumTransacciones = r.GetInt32(7),
                PeriodoLaboral   = r.GetInt32(8) == 1,
                FechaCierre      = r.GetString(9),
                Notas            = r.IsDBNull(10) ? "" : r.GetString(10)
            });
        return list;
    }

    public bool MesCerrado(int usuarioId, int anio, int mes)
    {
        using var conn = Abrir();
        return EjecutarScalar<long>(conn,
            "SELECT COUNT(*) FROM ResumenMensual WHERE UsuarioId=$uid AND Anio=$anio AND Mes=$mes",
            ("$uid", usuarioId), ("$anio", anio), ("$mes", mes)) > 0;
    }

    /// <summary>
    /// Consolida el resumen del período en PeriodosLaborales SIN generar ingreso automático.
    /// TransaccionId queda NULL — el ingreso se registra por separado si el usuario lo desea.
    /// </summary>
    public void ConsolidarPeriodoLaboral(int usuarioId, int anio, int mes,
        decimal salarioBruto, decimal deducciones, decimal salarioNeto)
    {
        using var conn = Abrir();
        EjecutarNonQuery(conn,
            """
            INSERT INTO PeriodosLaborales (UsuarioId, Anio, Mes, SalarioBruto, Deducciones, SalarioNeto, Cerrado)
            VALUES ($uid, $a, $m, $sb, $ded, $sn, 1)
            ON CONFLICT(UsuarioId, Anio, Mes) DO UPDATE SET
                SalarioBruto=$sb, Deducciones=$ded, SalarioNeto=$sn, Cerrado=1
            """,
            ("$uid", usuarioId), ("$a", anio), ("$m", mes),
            ("$sb",  (double)salarioBruto), ("$ded", (double)deducciones),
            ("$sn",  (double)salarioNeto));
    }

    /// <summary>Elimina todos los registros diarios del período indicado.</summary>
    public void ReiniciarRegistrosDia(int usuarioId, int anio, int mes)
    {
        using var conn = Abrir();
        var mesStr = mes.ToString("D2");
        EjecutarNonQuery(conn,
            """
            DELETE FROM RegistrosDiasLaborales
            WHERE UsuarioId = $uid
              AND strftime('%Y', Fecha) = $anio
              AND strftime('%m', Fecha) = $mes
            """,
            ("$uid", usuarioId), ("$anio", anio.ToString()), ("$mes", mesStr));
    }

    public int CerrarPeriodoLaboral(int usuarioId, int anio, int mes,
        decimal salarioBruto, decimal deducciones, decimal salarioNeto)
    {
        using var conn = Abrir();
        using var tr   = conn.BeginTransaction();
        try
        {
            // Insertar la transacción de ingreso
            var fecha = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes));
            using var cmdIns = conn.CreateCommand();
            cmdIns.CommandText = """
                INSERT INTO Transacciones
                    (UsuarioId, Tipo, Monto, Categoria, Descripcion, Fecha, Notas, CuentaNombre)
                VALUES ($uid, 'Ingreso', $monto, 'Salario', $desc, $fecha, 'Salario neto del período laboral', 'Efectivo');
                SELECT last_insert_rowid()
                """;
            cmdIns.Parameters.AddWithValue("$uid",   usuarioId);
            cmdIns.Parameters.AddWithValue("$monto", (double)salarioNeto);
            cmdIns.Parameters.AddWithValue("$desc",  $"Salario {new DateTime(anio, mes, 1):MMMM yyyy}");
            cmdIns.Parameters.AddWithValue("$fecha", fecha.ToString("yyyy-MM-dd"));
            var tid = (int)(long)cmdIns.ExecuteScalar()!;

            // Insertar o actualizar el período
            EjecutarNonQuery(conn,
                """
                INSERT INTO PeriodosLaborales (UsuarioId, Anio, Mes, SalarioBruto, Deducciones, SalarioNeto, Cerrado, TransaccionId)
                VALUES ($uid, $a, $m, $sb, $ded, $sn, 1, $tid)
                ON CONFLICT(UsuarioId, Anio, Mes) DO UPDATE SET
                    SalarioBruto=$sb, Deducciones=$ded, SalarioNeto=$sn, Cerrado=1, TransaccionId=$tid
                """,
                ("$uid", usuarioId), ("$a", anio), ("$m", mes),
                ("$sb",  (double)salarioBruto), ("$ded", (double)deducciones),
                ("$sn",  (double)salarioNeto),  ("$tid", tid));

            tr.Commit();
            return tid;
        }
        catch { tr.Rollback(); throw; }
    }

    // ── Ingresos laborales directos ───────────────────────────────────────────

    public List<IngresoLaboralDirecto> ObtenerIngresosLaboralesDirectos(int usuarioId)
    {
        using var conn = Abrir();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Fecha, Monto, Descripcion, CuentaId, TransaccionId
            FROM IngresoLaboralDirecto
            WHERE UsuarioId=$uid
            ORDER BY Fecha DESC, Id DESC
            """;
        cmd.Parameters.AddWithValue("$uid", usuarioId);
        var list = new List<IngresoLaboralDirecto>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new IngresoLaboralDirecto
            {
                Id            = r.GetInt32(0),
                UsuarioId     = usuarioId,
                Fecha         = DateTime.Parse(r.GetString(1)),
                Monto         = (decimal)r.GetDouble(2),
                Descripcion   = r.IsDBNull(3) ? "" : r.GetString(3),
                CuentaId      = r.IsDBNull(4) ? null : r.GetInt32(4),
                TransaccionId = r.IsDBNull(5) ? null : r.GetInt32(5)
            });
        return list;
    }

    public void InsertarIngresoLaboralDirecto(int usuarioId, DateTime fecha, decimal monto,
        string descripcion, int? cuentaId)
    {
        using var conn = Abrir();
        using var tr   = conn.BeginTransaction();
        try
        {
            // Obtener nombre de la cuenta si hay CuentaId
            string cuentaNombre = "Efectivo";
            if (cuentaId.HasValue)
            {
                using var cmdNombre = conn.CreateCommand();
                cmdNombre.CommandText = "SELECT Nombre FROM Cuentas WHERE Id=$id";
                cmdNombre.Parameters.AddWithValue("$id", cuentaId.Value);
                var n = cmdNombre.ExecuteScalar();
                if (n is string s) cuentaNombre = s;
            }

            // Crear transacción de ingreso
            using var cmdIns = conn.CreateCommand();
            cmdIns.CommandText = """
                INSERT INTO Transacciones
                    (UsuarioId, Tipo, Monto, Categoria, Descripcion, Fecha, Notas, CuentaNombre, CuentaId)
                VALUES ($uid, 'Ingreso', $monto, 'Salario', $desc, $fecha, 'Ingreso laboral directo', $cuentaNombre, $cuentaId);
                SELECT last_insert_rowid()
                """;
            cmdIns.Parameters.AddWithValue("$uid",         usuarioId);
            cmdIns.Parameters.AddWithValue("$monto",       (double)monto);
            cmdIns.Parameters.AddWithValue("$desc",        string.IsNullOrWhiteSpace(descripcion) ? "Ingreso laboral" : descripcion);
            cmdIns.Parameters.AddWithValue("$fecha",       fecha.ToString("yyyy-MM-dd"));
            cmdIns.Parameters.AddWithValue("$cuentaNombre", cuentaNombre);
            cmdIns.Parameters.AddWithValue("$cuentaId",   cuentaId.HasValue ? cuentaId.Value : DBNull.Value);
            var tid = (int)(long)cmdIns.ExecuteScalar()!;


            // Insertar registro de ingreso directo
            EjecutarNonQuery(conn,
                "INSERT INTO IngresoLaboralDirecto (UsuarioId, Fecha, Monto, Descripcion, CuentaId, TransaccionId) VALUES ($uid,$fecha,$monto,$desc,$cid,$tid)",
                ("$uid",   usuarioId),
                ("$fecha", fecha.ToString("yyyy-MM-dd")),
                ("$monto", (double)monto),
                ("$desc",  descripcion),
                ("$cid",   cuentaId.HasValue ? cuentaId.Value : DBNull.Value),
                ("$tid",   tid));

            tr.Commit();
        }
        catch { tr.Rollback(); throw; }
    }

    public void EliminarIngresoLaboralDirecto(int id)
    {
        using var conn = Abrir();
        using var tr   = conn.BeginTransaction();
        try
        {
            // Leer datos para revertir saldo
            int?   transId  = null;
            int?   cuentaId = null;
            double monto    = 0;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT TransaccionId, CuentaId, Monto FROM IngresoLaboralDirecto WHERE Id=$id";
                cmd.Parameters.AddWithValue("$id", id);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    transId  = r.IsDBNull(0) ? null : r.GetInt32(0);
                    cuentaId = r.IsDBNull(1) ? null : r.GetInt32(1);
                    monto    = r.GetDouble(2);
                }
            }


            // Eliminar transacción vinculada
            if (transId.HasValue)
                EjecutarNonQuery(conn, "DELETE FROM Transacciones WHERE Id=$id", ("$id", transId.Value));

            EjecutarNonQuery(conn, "DELETE FROM IngresoLaboralDirecto WHERE Id=$id", ("$id", id));
            tr.Commit();
        }
        catch { tr.Rollback(); throw; }
    }

    public void ActualizarPeriodoLaboral(int id, decimal salarioBruto, decimal deducciones,
        decimal salarioNeto, string notas)
    {
        using var conn = Abrir();
        using var tr   = conn.BeginTransaction();
        try
        {
            // Leer TransaccionId actual para actualizar el monto
            int? transId = null;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT TransaccionId FROM PeriodosLaborales WHERE Id=$id";
                cmd.Parameters.AddWithValue("$id", id);
                var v = cmd.ExecuteScalar();
                if (v is long l) transId = (int)l;
            }

            EjecutarNonQuery(conn,
                "UPDATE PeriodosLaborales SET SalarioBruto=$sb, Deducciones=$ded, SalarioNeto=$sn, Notas=$notas WHERE Id=$id",
                ("$sb",    (double)salarioBruto),
                ("$ded",   (double)deducciones),
                ("$sn",    (double)salarioNeto),
                ("$notas", notas),
                ("$id",    id));

            // Actualizar monto de la transacción vinculada (no afecta saldo de cuenta ya que CuentaId era NULL)
            if (transId.HasValue)
                EjecutarNonQuery(conn,
                    "UPDATE Transacciones SET Monto=$m WHERE Id=$id",
                    ("$m", (double)salarioNeto), ("$id", transId.Value));

            tr.Commit();
        }
        catch { tr.Rollback(); throw; }
    }

    public void EliminarPeriodoLaboral(int id)
    {
        using var conn = Abrir();
        using var tr   = conn.BeginTransaction();
        try
        {
            int? transId = null;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT TransaccionId FROM PeriodosLaborales WHERE Id=$id";
                cmd.Parameters.AddWithValue("$id", id);
                var v = cmd.ExecuteScalar();
                if (v is long l) transId = (int)l;
            }

            if (transId.HasValue)
                EjecutarNonQuery(conn, "DELETE FROM Transacciones WHERE Id=$id", ("$id", transId.Value));

            EjecutarNonQuery(conn, "DELETE FROM PeriodosLaborales WHERE Id=$id", ("$id", id));
            tr.Commit();
        }
        catch { tr.Rollback(); throw; }
    }

    // ── Verificación de integridad ────────────────────────────────────────────

    /// <summary>
    /// Recorre la base buscando discrepancias entre las distintas fuentes de verdad
    /// (Transacciones, Cuentas.SaldoActual, TarjetasCredito.SaldoUsado, PagosMensuales…)
    /// y referencias rotas. Es de solo lectura: nunca modifica datos.
    /// Una base sana devuelve una lista vacía.
    /// </summary>
    public List<ProblemaIntegridad> VerificarIntegridad()
    {
        using var conn = Abrir();
        var problemas = new List<ProblemaIntegridad>();

        void Add(SeveridadProblema sev, string area, string desc, string detalle = "", int cant = 1) =>
            problemas.Add(new ProblemaIntegridad
            {
                Severidad = sev, Area = area, Descripcion = desc, Detalle = detalle, Cantidad = cant
            });

        void Contar(string sql, SeveridadProblema sev, string area, string desc, string detalle = "")
        {
            var n = (int)EjecutarScalar<long>(conn, sql);
            if (n > 0) Add(sev, area, desc, detalle, n);
        }

        // ── 1. Cuentas ──
        // Ya no hay nada que comprobar aquí: desde la decisión D1 el saldo de una cuenta se
        // calcula (SaldoInicial + Σ transacciones) en lugar de mantenerse como contador, así
        // que la deriva entre el saldo mostrado y las transacciones es imposible. La
        // comprobación que vivía aquí pasaría siempre.

        // ── 2. Tarjetas: SaldoUsado vs. cargos del período en curso ──
        // El modelo actual afirma que SaldoUsado son los cargos desde el último corte.
        // Se consulta la tabla directamente: ObtenerTarjetas() escribiría (AutoRenovarPeriodo).
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Nombre, SaldoUsado, DiaCierre FROM TarjetasCredito";
            var tarjetas = new List<(int Id, string Nombre, decimal Saldo, int DiaCierre)>();
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    tarjetas.Add((r.GetInt32(0), r.GetString(1), (decimal)r.GetDouble(2), r.GetInt32(3)));

            foreach (var t in tarjetas)
            {
                var inicioPeriodo = new TarjetaCredito { DiaCierre = t.DiaCierre }.InicioPeriodo;
                var cargos = (decimal)EjecutarScalar<double>(conn,
                    "SELECT COALESCE(SUM(Monto), 0) FROM Transacciones " +
                    "WHERE TarjetaCreditoId = $id AND Tipo = 'Gasto' AND Fecha >= $desde",
                    ("$id", t.Id), ("$desde", inicioPeriodo.ToString("yyyy-MM-dd")));

                var deriva = t.Saldo - cargos;
                if (Math.Abs(deriva) > 0.01m)
                    Add(SeveridadProblema.Critico, "Tarjetas",
                        $"El saldo usado de «{t.Nombre}» no coincide con los cargos del período.",
                        $"Guardado: {t.Saldo:N2} · Cargos desde {inicioPeriodo:dd/MM/yyyy}: {cargos:N2} · Deriva: {deriva:N2}");
            }
        }

        // ── 3. Referencias rotas ──
        Contar("SELECT COUNT(*) FROM Transacciones " +
               "WHERE CuentaId IS NOT NULL AND CuentaId NOT IN (SELECT Id FROM Cuentas)",
            SeveridadProblema.Critico, "Cuentas",
            "Transacciones que apuntan a una cuenta que ya no existe.",
            "Sus importes ya no ajustan ningún saldo: la reversión al borrarlas no hace nada.");

        Contar("SELECT COUNT(*) FROM Transacciones " +
               "WHERE TarjetaCreditoId IS NOT NULL AND TarjetaCreditoId NOT IN (SELECT Id FROM TarjetasCredito)",
            SeveridadProblema.Critico, "Tarjetas",
            "Transacciones que apuntan a una tarjeta que ya no existe.",
            "Quedan excluidas de todos los totales y son inalcanzables desde la interfaz.");

        Contar("SELECT COUNT(*) FROM PagosMensuales " +
               "WHERE TransaccionId IS NOT NULL AND TransaccionId NOT IN (SELECT Id FROM Transacciones)",
            SeveridadProblema.Advertencia, "Pagos fijos",
            "Pagos marcados que apuntan a una transacción borrada.");

        Contar("SELECT COUNT(*) FROM PagosMensuales " +
               "WHERE TipoFijo='Gasto' AND FijoId NOT IN (SELECT Id FROM GastosFijos)",
            SeveridadProblema.Advertencia, "Pagos fijos",
            "Pagos mensuales de un gasto fijo que ya no existe.");

        Contar("SELECT COUNT(*) FROM PagosMensuales " +
               "WHERE TipoFijo='Ingreso' AND FijoId NOT IN (SELECT Id FROM IngresosFijos)",
            SeveridadProblema.Advertencia, "Pagos fijos",
            "Pagos mensuales de un ingreso fijo que ya no existe.");

        Contar("SELECT COUNT(*) FROM IngresoLaboralDirecto " +
               "WHERE TransaccionId IS NOT NULL AND TransaccionId NOT IN (SELECT Id FROM Transacciones)",
            SeveridadProblema.Advertencia, "Control Laboral",
            "Ingresos laborales directos cuya transacción fue borrada.");

        Contar("SELECT COUNT(*) FROM PeriodosLaborales " +
               "WHERE TransaccionId IS NOT NULL AND TransaccionId NOT IN (SELECT Id FROM Transacciones)",
            SeveridadProblema.Advertencia, "Control Laboral",
            "Períodos laborales cuya transacción de salario fue borrada.");

        // ── 4. Estados imposibles ──
        Contar("SELECT COUNT(*) FROM PagosMensuales WHERE Pagado=1 AND TransaccionId IS NULL",
            SeveridadProblema.Critico, "Pagos fijos",
            "Pagos marcados como pagados sin transacción asociada.",
            "Eliminar el ingreso fijo correspondiente provoca un fallo de la aplicación.");

        Contar("SELECT COUNT(*) FROM Transacciones WHERE TarjetaCreditoId IS NOT NULL AND Tipo='Ingreso'",
            SeveridadProblema.Critico, "Transacciones",
            "Ingresos registrados con una tarjeta de crédito como origen.",
            "No suman en ningún total: quedan fuera del balance, los reportes y el cierre de mes.");

        Contar("SELECT COUNT(*) FROM Transacciones WHERE Monto <= 0",
            SeveridadProblema.Advertencia, "Transacciones",
            "Transacciones con importe cero o negativo.");

        Contar("SELECT COUNT(*) FROM GastosFijos " +
               "WHERE DiaVencimiento2 > 0 AND DiaVencimiento2 = DiaVencimiento",
            SeveridadProblema.Critico, "Gastos fijos",
            "Gastos fijos con el segundo día igual al primero.",
            "Ambas filas comparten el mismo pago mensual: marcar una marca las dos y el total se duplica.");

        Contar("SELECT COUNT(*) FROM IngresosFijos " +
               "WHERE DiaIngreso2 > 0 AND DiaIngreso2 = DiaIngreso",
            SeveridadProblema.Critico, "Ingresos fijos",
            "Ingresos fijos con el segundo día igual al primero.",
            "Ambas filas comparten el mismo pago mensual: marcar una marca las dos y el total se duplica.");

        Contar("SELECT COUNT(*) FROM Transacciones " +
               "WHERE Categoria NOT IN (SELECT Nombre FROM Categorias)",
            SeveridadProblema.Advertencia, "Categorías",
            "Transacciones con una categoría que ya no existe.",
            "Siguen agrupándose en los gráficos pero no aparecen en el desplegable de filtro.");

        Contar("SELECT COUNT(*) FROM IngresosFijos " +
               "WHERE CuentaId IS NOT NULL AND CuentaId NOT IN (SELECT Id FROM Cuentas)",
            SeveridadProblema.Advertencia, "Ingresos fijos",
            "Ingresos fijos ligados a una cuenta que ya no existe.");

        // ── 5. Datos huérfanos de usuarios eliminados ──
        // EliminarUsuario() sólo limpia 7 de las 13 tablas que tienen UsuarioId.
        foreach (var tabla in new[]
                 {
                     "Transacciones", "MetasAhorro", "TarjetasCredito", "GastosFijos",
                     "IngresosFijos", "PagosMensuales", "Categorias", "Cuentas",
                     "ConfiguracionLaboral", "RegistrosDiasLaborales", "PeriodosLaborales",
                     "IngresoLaboralDirecto", "ResumenMensual"
                 })
        {
            // Categorias admite UsuarioId NULL (globales): esas no son huérfanas.
            var filtroNull = tabla == "Categorias" ? "UsuarioId IS NOT NULL AND " : "";
            Contar($"SELECT COUNT(*) FROM {tabla} " +
                   $"WHERE {filtroNull}UsuarioId NOT IN (SELECT Id FROM Usuarios)",
                SeveridadProblema.Advertencia, "Usuarios",
                $"Filas en «{tabla}» de un usuario que ya no existe.");
        }

        // ── 6. Cierres mensuales desactualizados ──
        // CerrarMes() guarda una foto. Nada impide editar el mes después del cierre.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT UsuarioId, Anio, Mes, Ingresos, Gastos FROM ResumenMensual";
            var cierres = new List<(int Uid, int Anio, int Mes, decimal Ing, decimal Gas)>();
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    cierres.Add((r.GetInt32(0), r.GetInt32(1), r.GetInt32(2),
                                 (decimal)r.GetDouble(3), (decimal)r.GetDouble(4)));

            foreach (var c in cierres)
            {
                var desde = new DateTime(c.Anio, c.Mes, 1);
                var hasta = desde.AddMonths(1).AddDays(-1);

                // Mismo eje que usa CerrarMes al guardar el resumen (presupuesto), o la
                // comprobación reportaría desfases que no existen.
                decimal Total(string tipo) => (decimal)EjecutarScalar<double>(conn,
                    "SELECT COALESCE(SUM(Monto),0) FROM Transacciones " +
                    "WHERE UsuarioId=$uid AND PagoDeTarjetaId IS NULL AND Tipo=$tipo " +
                    "AND Fecha >= $desde AND Fecha <= $hasta",
                    ("$uid", c.Uid), ("$tipo", tipo),
                    ("$desde", desde.ToString("yyyy-MM-dd")), ("$hasta", hasta.ToString("yyyy-MM-dd")));

                var ing = Total("Ingreso");
                var gas = Total("Gasto");

                if (Math.Abs(ing - c.Ing) > 0.01m || Math.Abs(gas - c.Gas) > 0.01m)
                    Add(SeveridadProblema.Advertencia, "Cierre mensual",
                        $"El cierre de {c.Mes:D2}/{c.Anio} ya no coincide con sus transacciones.",
                        $"Guardado: +{c.Ing:N2} / -{c.Gas:N2} · Actual: +{ing:N2} / -{gas:N2}");
            }
        }

        return problemas
            .OrderBy(p => p.Severidad)
            .ThenBy(p => p.Area)
            .ToList();
    }
}
