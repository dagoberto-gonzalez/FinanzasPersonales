# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build
dotnet run
# Or launch directly:
bin/Debug/net10.0-windows/FinanzasPersonales.exe
```

Target: `.NET 10 WPF` (`net10.0-windows`). Only builds/runs on Windows.

`Tools/DocGenerator` is a separate console project (its own `.csproj`) excluded from the main build via `<Compile Remove="Tools\**" />` in `FinanzasPersonales.csproj` — build it independently if needed.

### Tests

```bash
dotnet test Tests/FinanzasPersonales.Tests.csproj
```

xUnit, in `Tests/` — excluded from the main build via `<Compile Remove="Tests\**" />`. There is
deliberately **no `.sln`**, so bare `dotnet build` still means "build the app" (which
`build-installer.bat` and the `/check` + `/release` commands rely on).

- `BaseDePrueba` spins up an isolated temp SQLite file per test. This only works because
  `AppDatabase` takes an optional `dbPath` — do not remove that parameter.
- `RutasDeDineroTests` — characterization of the money paths that work today. These are the
  safety net for making `Cuentas.SaldoActual` / `TarjetasCredito.SaldoUsado` derived values.
  **They must stay green through that refactor.**
- `BugsConocidosTests` — these assert **current broken behaviour** on purpose, as executable
  proof each defect is real. When a bug is fixed, invert the assertion and move the test to
  `RutasDeDineroTests`. One of these going red is the signal that a fix landed.
- `VerificarIntegridadTests` — validates the verifier itself by injecting known corruption.

### Installer

`build-installer.bat` publishes self-contained win-x64 (`PublishProfile=win-x64-installer`) then compiles `Installer/setup.iss` (Inno Setup) into a Windows installer.

## Architecture

**Pattern:** MVVM (manual `INotifyPropertyChanged` — no CommunityToolkit).

```
Models/          Pure data classes (Transaccion, MetaAhorro, Usuario, Cuenta, TarjetaCredito,
                 PieSlice, ProblemaIntegridad, and the Control Laboral models: ConfiguracionLaboral,
                 PeriodoLaboral, RegistroDiaLaboral, IngresoLaboralDirecto, HistorialLaboralItem,
                 ResumenMensual…)
Data/            AppDatabase.cs — all SQLite CRUD via Microsoft.Data.Sqlite (~2300 lines, single file)
                 ctor takes an optional dbPath (null = %APPDATA%); required for tests
                 VerificarIntegridad() — read-only consistency scan, see below
Services/        SessionService (auth state: UsuarioId, Rol, EsAdmin)
                 AppSettings (runtime config: Moneda symbol — declared but NOT wired up yet)
                 Dinero — parses user-entered amounts; use it for every money input
                 PdfExportService / PdfLaboralService — QuestPDF report generation
ViewModels/      BaseViewModel, RelayCommand/RelayCommand<T>, one VM per view
Controls/        PieChartControl, BarChartControl — custom charts drawn on WPF Canvas
Views/           UserControls per section (Dashboard, Transacciones, Cuentas, Tarjetas,
                 GastosFijos, IngresosFijos, Metas, Categorias, Reportes, ControlLaboral, Admin…)
Windows/         LoginWindow, RegistroWindow, IngresoLaboralWindow
MainWindow        Sidebar nav (grouped in collapsible Expanders) + ContentControl;
                 views created lazily and cached
App.xaml         All WPF styles/themes (dark palette, Button variants, DataGrid…)
```

Depends on `MahApps.Metro`, `Microsoft.Data.Sqlite`, and `QuestPDF` (see `FinanzasPersonales.csproj`).

## Navigation

`MainWindow.xaml.cs` owns one cached `UserControl` instance per section (`_dashView`, `_transView`,
`_cuentasView`, `_controlLaboralView`, etc.), built lazily via `Obtener*()` factory methods. Each
`Mostrar*()` method fetches its view, calls `.Actualizar()` to refresh from SQLite, sets it as
`MainContent.Content`, calls `DesactivarTodos()` to re-enable every nav button, then disables the
current section's button (`IsEnabled=false` drives the "active" visual in the `NavButton` style) and
expands its sidebar group (`ExpGeneral`, `ExpRecurrentes`, `ExpFinanzas`, `ExpLaboral`, `ExpCuenta`)
via `ExpandirGrupo()`.

## Database

SQLite file lives in `%APPDATA%\FinanzasPersonales\finanzas.db`. `AppDatabase` opens a new
connection per operation (no connection pooling / EF Core) and runs idempotent
`CREATE TABLE IF NOT EXISTS` + ad-hoc column/table migrations on startup. Key tables:

- `Configuracion (Clave, Valor)` — seeded with `moneda='₡'`, `max_usuarios`, `presupuesto_mensual`
- `Usuarios (Id, NombreUsuario, PasswordHash, Rol, Activo, FechaCreacion)` — Rol: "Admin"|"Normal"
- `Transacciones (Id, UsuarioId, Tipo, Monto, Categoria, Descripcion, Fecha, Notas, CuentaNombre, CuentaId, TarjetaCreditoId)`
- `MetasAhorro (Id, UsuarioId, Nombre, MontoObjetivo, MontoActual, FechaLimite)`
- `Cuentas (Id, UsuarioId, Nombre, Tipo, Banco, Activa, SaldoActual)` — UNIQUE(UsuarioId, Nombre)
- `TarjetasCredito (Id, UsuarioId, Nombre, LimiteCredito, SaldoUsado, DiaCierre, DiaPago, FechaUltimoCorte)` — UNIQUE(UsuarioId, Nombre)
- `GastosFijos` / `IngresosFijos (Id, UsuarioId, Nombre, Monto, Dia*Vencimiento|Ingreso, Dia*2, Activo)`
- `PagosMensuales (Id, UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia, Pagado, MetodoPago, TransaccionId)` — tracks per-month payment of a GastoFijo/IngresoFijo, UNIQUE(UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia)
- `Categorias (Id, UsuarioId, Nombre)` — UsuarioId=NULL means global (all users see it), UNIQUE(UsuarioId, Nombre)
- **Control Laboral (hourly-wage timesheet module):** `ConfiguracionLaboral (UsuarioId PK, SalarioPorHora, JornadaSemanal, DiaPago…)`, `RegistrosDiasLaborales (UsuarioId, Fecha, HorasNormales, HorasExtraDiurnas/Nocturnas, HorasDobles, EsFeriado, EsAusencia…)` — UNIQUE(UsuarioId, Fecha), `PeriodosLaborales (UsuarioId, Anio, Mes, SalarioBruto, Deducciones, SalarioNeto, Cerrado, TransaccionId)`, `IngresoLaboralDirecto`, `ResumenMensual (UsuarioId, Anio, Mes, Ingresos, Gastos, Balance, BalanceAcumulado, NumTransacciones, FechaCierre…)` — one row per closed month, insert is idempotent (`INSERT OR IGNORE`)

Money amounts in `Transacciones`/`Cuentas`/`TarjetasCredito` are kept in sync manually: e.g.
inserting a Gasto against a `CuentaId` decrements `Cuentas.SaldoActual`, deleting it reverts the
delta, and marking a `GastoFijo`/`IngresoFijo` paid in `PagosMensuales` both creates a linked
`Transaccion` (`TransaccionId`) and adjusts the account balance — unmarking reverses both.

## Invariants worth knowing

These are enforced in `AppDatabase` (not just in the UI), so breaking them fails loudly:

- **An `Ingreso` can never carry a `TarjetaCreditoId`** — `InsertarTransaccion` throws. Every
  total in the app excludes transactions with a card, so such a row would be invisible money.
  The Transacciones screen also drops "Tarjeta" from the method list when the type is Ingreso.
- **A fixed item's second day can never equal the first** — `InsertarGastoFijo` /
  `InsertarIngresoFijo` normalise it to 0. Two equal days produce two UI rows sharing one
  `PagosMensuales` row (the unique key includes `Dia`), so the amount gets counted twice.
- **`AppDatabase.CategoriasSistema`** (`Gastos Fijos`, `Ingresos Fijos`, `Salario`, `Tarjeta`)
  are written *by name* by the code that generates automatic transactions. They are seeded as
  global categories and cannot be renamed or deleted, not even by an admin — renaming one would
  leave the code writing a name nobody can filter by.
- **Money typed by the user goes through `Services.Dinero.TryParse`**, never a bare
  `decimal.TryParse`. Fields pre-filled with `ToString("N0")` carry thousands separators
  (`1.000.000`) while people type decimals as `1500,50` or `1500.50`; one parser handles all.

## Integrity verification

Money lives in several manually-synchronised places: `Transacciones` (the event log),
`Cuentas.SaldoActual` and `TarjetasCredito.SaldoUsado` (running counters updated by hand in
~9 places each), plus `MetasAhorro.MontoActual` and `PeriodosLaborales`. Nothing reconciles them.

`AppDatabase.VerificarIntegridad()` is that missing reconciliation. It is **read-only — it
reports, it never repairs** — and returns `List<ProblemaIntegridad>` covering: stored-vs-computed
balances for accounts and cards, broken references (transactions → deleted account/card, payments
→ deleted transaction), impossible states (payment marked paid with no transaction, income booked
against a credit card, fixed items whose two due days are equal), orphan rows from incompletely
deleted users, and stale `ResumenMensual` snapshots.

Surfaced in the UI under **Admin → Diagnóstico de integridad**. A healthy database returns an
empty list; that empty list is the exit criterion for any refactor of the money paths.

## Key Patterns

- **Placeholder text on TextBox:** uses the `Tag` property — the style shows `Tag` as greyed hint when `Text == ""`.
- **Pie/Bar charts:** `PieChartControl`/`BarChartControl` have a `Slices`/data DependencyProperty. Any assignment triggers `Redraw()` which rebuilds Canvas `Path`/shape elements.
- **Goals VM wrapper:** `MetaAhorroVm` (nested in `MetasViewModel.cs`) wraps `MetaAhorro` and adds `MontoAgregar` + `AgregarDineroCommand` so each card has its own text field binding.
- **Monto coloring in DataGrid:** done with inline `DataTrigger` on `Tipo == "Gasto"` — no converter needed.
- **PDF exports:** `PdfExportService` (general finance report) and `PdfLaboralService` (Control Laboral payroll report) build documents with QuestPDF's fluent `Document.Create` API and save to `MyDocuments`.

## Dark Theme Palette

| Key | Hex | Use |
|---|---|---|
| `BgBrush` | `#1E1E2E` | Window background |
| `SurfaceBrush` | `#24273A` | Sidebar, inputs |
| `CardBrush` | `#2A2D3E` | Cards |
| `AccentBrush` | `#7C3AED` | Primary actions |
| `GreenBrush` | `#A6E3A1` | Ingresos |
| `RedBrush` | `#F38BA8` | Gastos / danger |
