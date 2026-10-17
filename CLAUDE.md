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
- `Bloque1RegresionTests` / `Bloque2D1Tests` / `Bloque2D2Tests` / `Bloque2D3Tests` — regressions
  for each fixed defect. Several began life in a `BugsConocidosTests` file that asserted the
  broken behaviour on purpose; that file is gone now that no known defect is left open. If a
  defect is ever parked again, reuse the convention: assert the breakage, then invert it.
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

SQLite file lives in `%APPDATA%\FinanzasPersonales\finanzas.db` — **Release builds only** (the
installer). Debug builds (`dotnet run`, Visual Studio) use `finanzas-dev.db` beside it and add
" · DESARROLLO" to every window title (`AppDatabase.EsDesarrollo`), so testing while coding never
touches real data.

**Backups** (`Services/CopiasDeSeguridad`): on startup, once a day and **before the database is opened** (opening runs migrations, so an upgrade is backed up as it was), a copy goes to `Copias\` beside
the database as `{finanzas|finanzas-dev}_yyyy-MM-dd_HHmmss.db`; only files matching that pattern are
pruned (last 30 kept). Admin → Copias de seguridad saves a copy elsewhere and restores one —
restoring first saves the current data as `…_antes-de-restaurar_…db`, then the app shuts down so
migrations run on the restored file. Always copy with SQLite's backup API, never `File.Copy`: a
copy taken mid-write is corrupt. `AppDatabase` opens a new
connection per operation (no connection pooling / EF Core) and runs idempotent
`CREATE TABLE IF NOT EXISTS` + ad-hoc column/table migrations on startup. Key tables:

- `Configuracion (Clave, Valor)` — seeded with `moneda='₡'`, `max_usuarios`. The monthly budget used to live here as one global key; it is now per user
- `Usuarios (Id, NombreUsuario, PasswordHash, Rol, Activo, FechaCreacion, PresupuestoMensual)` — Rol: "Admin"|"Normal". `PresupuestoMensual` (0 = none) drives the Dashboard budget card and alert, measured on the `AfectaPresupuesto` axis; edited on the card itself. See "Accounts and passwords" below
- `Transacciones (Id, UsuarioId, Tipo, Monto, Categoria, Descripcion, Fecha, Notas, CuentaNombre, CuentaId, TarjetaCreditoId, PagoDeTarjetaId, TransferenciaId)` — `TarjetaCreditoId` / `PagoDeTarjetaId` are the D2 axes; `TransferenciaId` links the two halves of a transfer (see below)
- `MetasAhorro (Id, UsuarioId, Nombre, MontoObjetivo, MontoActual, FechaLimite, CuentaId)` + `MovimientosMeta (Id, MetaId → CASCADE, Fecha, Monto, Tipo, TransaccionId → CASCADE)` — **decision D5: a goal is an earmark ("bolsillo") inside an account.** Its reserved amount is `Σ MovimientosMeta`, never stored. *Apartar* reserves without moving money (only `Cuenta.Libre = SaldoActual − Apartado` changes, and it cannot exceed what is free); *Usar* inserts a real expense from the goal's account plus a negative movement linked to it, in one transaction (deleting that expense cascades the movement back; editing it is refused); *Liberar* un-reserves without spending. `MontoActual` is now only the pre-D5 counter of goals without an account, turned into an initial earmark when their owner picks one (`AsignarCuentaAMeta`)
- `Cuentas (Id, UsuarioId, Nombre, Tipo, Banco, Activa, SaldoInicial)` — UNIQUE(UsuarioId, Nombre). There is **no stored current balance**: see "Derived balances" below
- `TarjetasCredito (Id, UsuarioId, Nombre, LimiteCredito, DiaCierre, DiaPago)` — UNIQUE(UsuarioId, Nombre). No stored balance: see "Derived balances"
- `GastosFijos` / `IngresosFijos (Id, UsuarioId, Nombre, Monto, Dia*Vencimiento|Ingreso, Dia*2, Activo, FechaInicio)` — `FechaInicio` (day 1 of the month) is the first month the item exists: their screens navigate between months (`ViewModels/MesNavegable`), and without it an item created today showed as unpaid in every earlier month. Rows from older databases took it from their first `PagosMensuales` month
- `PagosMensuales (Id, UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia, Pagado, MetodoPago, TransaccionId)` — tracks per-month payment of a GastoFijo/IngresoFijo, UNIQUE(UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia)
- `Categorias (Id, UsuarioId, Nombre, Tipo)` — UsuarioId=NULL means global (all users see it), UNIQUE(UsuarioId, Nombre). `Tipo` is `Gasto` | `Ingreso` | `Ambos` and decides which ones the Transacciones form offers for each movement type (`Categoria.SirvePara`); it is UI scoping only, not a rule on stored rows. The form's `CategoriasForm` is adjusted in place — add, move the selection, then remove — so the selected item is **never** removed: a WPF ComboBox that loses its selected item drops the selection and stops reading the binding (the bug the payment-method combo had)
- **Control Laboral (hourly-wage timesheet module):** `ConfiguracionLaboral (UsuarioId PK, SalarioMensual, JornadaSemanal, DiasPorSemana, PorcentajeDeduccion, ModoPago, DiaPago, Viaticos…)` — the **monthly salary is the input**; the hourly rate is derived as `mensual ÷ 240` (the payroll's legal divisor, not the worked week) and the `SalarioPorHora` column is only written for consistency, never read, `RegistrosDiasLaborales (UsuarioId, Fecha, HorasNormales, HorasExtraDiurnas/Nocturnas, HorasDobles, EsFeriado, HorasAusencia, TieneGoceSalario…)` — UNIQUE(UsuarioId, Fecha). **`HorasNormales` means hours worked on a public holiday**, paid at 1× on top of the fixed salary (the payslip's "Horas Normales" line); `EsFeriado` is just `HorasNormales > 0`, kept for old rows, `PeriodosLaborales (UsuarioId, FechaInicio, FechaFin, SalarioBruto, Deducciones, SalarioNeto, Cerrado, TransaccionId, SalarioMensual, PorcentajeDeduccion, Viaticos)` — UNIQUE(UsuarioId, FechaInicio). A period is a **date range**, not a month: the hours must come from the same range (`ConfiguracionLaboral.PeriodoQueContiene`), which the old `(Anio, Mes)` key made impossible for two fortnights in one month. The last three columns are the parameters **frozen at close**, so a later raise does not rewrite periods already paid (NULL on rows closed before they existed), `ComprobantesPago (PeriodoId PK → PeriodosLaborales ON DELETE CASCADE, SalarioFijo, Horas/Monto Extra·Nocturnas·Dobles, OtrosAjustes, Viaticos, Deducciones, Notas)` — what the payslip says, stored apart from what was calculated, `IngresoLaboralDirecto`, `ResumenMensual (UsuarioId, Anio, Mes, Ingresos, Gastos, Balance, BalanceAcumulado, NumTransacciones, FechaCierre…)` — one row per closed month (a snapshot; closing again overwrites it). `BalanceAcumulado` is the Dashboard's *Disponible* as of month end (`DisponibleAl`: opening balances + cash axis); it used to leave opening balances out. The Dashboard offers the **oldest** unclosed month since the user started (`MesesSinCerrar`: sign-up or first movement, whichever is earlier)

### The two axes (decision D2)

A movement can affect **what you spent**, **what you have left**, or both. Conflating them in a
single figure was the source of the inconsistencies between screens. `Transaccion` exposes both:

| | `AfectaPresupuesto` | `AfectaEfectivo` |
|---|---|---|
| Cash / debit purchase | yes | yes |
| **Credit card purchase** (`TarjetaCreditoId`) | **yes** | no |
| **Card payment** (`PagoDeTarjetaId`) | no | **yes** |
| **Transfer** (`TransferenciaId`, two halves) | no | **yes** (the halves cancel out) |

- **`AfectaPresupuesto`** (`PagoDeTarjetaId IS NULL AND TransferenciaId IS NULL`) drives categories, the budget alert,
  monthly/annual reports, `CerrarMes`, and the PDF. A card payment is not a new expense — it was
  already counted at purchase, and counting it again would double it.
- **`AfectaEfectivo`** (`TarjetaCreditoId IS NULL`) drives account balances and the dashboard's
  *Disponible*. A card purchase does not move money until the card is paid. *Disponible* is the
  sum of `ObtenerCuentas` balances (opening balance included) plus cash — `AfectaEfectivo` rows
  with no `CuentaId`. Summing transactions alone left out every opening balance.

**Editing a transaction** (`ActualizarTransaccion`) is a plain `UPDATE` with the same rules as
inserting (`ValidarTransaccion`) — balances follow on their own. Two kinds are refused, by
`MotivoNoEditable`: card payments (`PagoDeTarjetaId`, owned by the Tarjetas screen) and Control
Laboral income (`IngresoLaboralDirecto`/`PeriodosLaborales` keep their own copy of the amount).
A fixed item's payment *is* editable: `PagosMensuales` stores no amount, it reads the transaction's.

**Transfers** (`AppDatabase.Transferir`) move money between accounts, or between an account and
cash (`null`): a `Gasto` from the origin and an `Ingreso` into the destination, category
`Transferencia`, sharing `TransferenciaId`. They are off the budget axis, and on the cash axis
they cancel out, so *Disponible* does not move. Deleting either half deletes both; editing is
refused; `InsertarTransaccion` rejects the category so a lone half cannot be typed in. Only the
free part of an account with goal earmarks can be transferred. `EsCategoriaAutomatica` keeps
`Tarjeta` and `Transferencia` out of every form. Cash shows as a read-only "Efectivo" row in
Cuentas (`ObtenerSaldoEfectivo`, the same figure the Dashboard uses).

Whenever you sum transactions, **say which axis you mean** — never filter on `TarjetaCreditoId`
by hand for a "totals" query. A row may not be both a card purchase and a card payment;
`InsertarTransaccion` rejects that.

Before D2, ten places filtered `TarjetaCreditoId IS NULL`, which effectively said "a card
purchase is nothing": it vanished from every total and reappeared months later under the
category `Tarjeta`, so the category breakdown and the budget alert were blind to card spending.

### Derived balances (decision D1)

**An account's balance is computed, never stored.** `ObtenerCuentas` returns
`SaldoInicial + Σ (transactions with that CuentaId)`. There is no counter to keep in sync, so
an account balance cannot drift from its transactions — it is the same number by construction.

Practical consequences when touching this code:

- **Never write a balance.** Inserting, deleting or reverting a transaction adjusts nothing;
  the sum changes on its own. Ten `UPDATE Cuentas SET SaldoActual = ...` statements were removed
  for exactly this reason — do not reintroduce one.
- `Cuenta.SaldoActual` is a **read-only projection**. Assigning to it persists nothing.
- `SaldoInicial` is the opening balance and the only writable figure. It is how a user squares
  an account against their bank statement (Cuentas → Saldo inicial).
- Indexes in `CrearIndices()` (notably `IX_Transacciones_CuentaId`) are what keep the per-read
  `SUM` cheap. Keep them.

**Card balances are derived too (decision D3).** `ObtenerTarjetas` returns two different
figures, which used to be collapsed into one:

- **`SaldoUsado`** — all purchases minus all payments. The debt actually owed.
- **`ConsumoPeriodo`** — purchases since the last `DiaCierre`. Informational only.

`AutoRenovarPeriodo` is gone. It zeroed `SaldoUsado` on the closing date, but the closing date
is not when you pay: between the cut and the payment day the app claimed you owed nothing. Worse,
it was an `UPDATE` inside `ObtenerTarjetas`, so merely opening a screen on the wrong day
destroyed the figure. Reading never writes now, and `FechaUltimoCorte` no longer exists —
the period start comes from `DiaCierre` via `TarjetaCredito.InicioPeriodo`.

`AbonarTarjeta()` is gone as well: a payment is just a transaction carrying `PagoDeTarjetaId`.

Marking a `GastoFijo`/`IngresoFijo` paid in `PagosMensuales` still creates a linked
`Transaccion` (`TransaccionId`); unmarking deletes it, and every balance follows on its own.
`EliminarTransaccion` is now a plain `DELETE` — there is nothing left to revert.

**Deleting a fixed item keeps its past payments.** `EliminarGastoFijo` / `EliminarIngresoFijo`
drop the item and its `PagosMensuales` rows but leave the transactions: deleting means "no longer
expected", not "never paid". They used to delete every past payment's transaction too, erasing
real money from history, reports and balances.

### Foreign keys

The schema declares its foreign keys and `Abrir()` turns enforcement on for every connection —
SQLite defaults it off, and the setting is per connection, not per database. Deleting a row that
others point at now fails at the engine, not only where someone remembered to check.

- **`UsuarioId` is `ON DELETE CASCADE`** in all thirteen per-user tables. Deleting a user takes
  their data with it, so adding a new table cannot be forgotten. `Categorias.UsuarioId` stays
  nullable — NULL means global and survives.
- **`Transacciones.CuentaId` / `TarjetaCreditoId` / `PagoDeTarjetaId` are `RESTRICT`.** An
  account or card with movements cannot be deleted; to stop seeing it, deactivate it
  (`Cuenta.Activa`). The ViewModels check first and offer that instead of letting the delete fail.
- **Optional links to a transaction are `SET NULL`** (`PagosMensuales`, `PeriodosLaborales`,
  `IngresoLaboralDirecto`). `EliminarTransaccion` additionally unmarks the linked
  `PagosMensuales` row: the constraint alone would leave `Pagado=1` pointing at nothing, which
  is the state that used to crash the app.
- **`PagosMensuales.FijoId` has no foreign key** — it points at `GastosFijos` or `IngresosFijos`
  depending on `TipoFijo`, and a key cannot have two destinations. The verifier covers it by hand.

Adding or changing a foreign key means recreating the table (SQLite has no `ALTER TABLE ADD
CONSTRAINT`): see `MigrarAClavesForaneas`, which follows SQLite's documented procedure —
`foreign_keys=OFF` plus `legacy_alter_table=ON` so the rename does not rewrite other tables'
references, all inside one transaction. It runs once, guarded by `TieneClavesForaneas`, and
**must stay before `CrearIndices`**: recreating a table drops its indexes.

Category names are still stored as text on `Transacciones.Categoria`, deliberately —
normalising to a `CategoriaId` would buy little here, since categories are picked from a
dropdown rather than typed. `ActualizarCategoria` cascades a rename to the history instead, and
a category in use cannot be deleted.

## Invariants worth knowing

These are enforced in `AppDatabase` (not just in the UI), so breaking them fails loudly:

- **An `Ingreso` can never carry a `TarjetaCreditoId`** — `InsertarTransaccion` throws. Every
  total in the app excludes transactions with a card, so such a row would be invisible money.
  The Transacciones screen also drops "Tarjeta" from the method list when the type is Ingreso.
- **Changing a fixed item's days moves its recorded payments** (`ReubicarPagos`, called by
  `ActualizarGastoFijo` / `ActualizarIngresoFijo`). `PagosMensuales` keys a payment by its day,
  so moving the due date from 10 to 15 without moving the row would show an already-paid month as
  pending and invite paying it twice. Kept days keep their rows; a removed day hands its rows to
  the day that replaces it (via negative days, so swapping 10↔25 does not hit the unique key); a
  removed day with no replacement drops its unpaid rows and keeps paid ones as history. Editing
  the amount or account never rewrites past payments — each is its own transaction.
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
  It **rejects ambiguous input rather than guessing** — `1.200.00` is valid in no convention
  (it would be `1.200,00` or `1,200.00`), and silently picking a reading is how a wrong figure
  ends up recorded. A number may not use the same character for grouping and for decimals, and
  thousands groups must be three digits.

- **Money is always shown with two decimals** (`N2`, `₡216.818,38`), on screens, messages, PDFs
  and the fields pre-filled for editing. Rounding to the colón hid the cents the payroll has, and a
  field pre-filled rounded lost them when saved. The only `N0` left on purpose: the bar chart's
  `₡120k` axis and percentages.

## Accounts and passwords

- **There is no factory admin account.** A fresh install has zero users; the first person to
  register becomes `Admin` (`RegistroViewModel`), and the login window says so. Previously
  `MigrarAMultiUsuario` created `Admin`/`admin123` in *any* empty database, so whoever opened a
  new install first was admin. It now only creates a user when migrating a mono-user database
  (credentials from `Configuracion`, or existing data).
- **Passwords go through `Services.Contrasenas`**, never a bare hash: PBKDF2-HMAC-SHA256, random
  per-user salt, 600,000 iterations (~60 ms on .NET 10), stored as
  `pbkdf2-sha256$iterations$salt$hash`. The old unsalted SHA-256 hex is accepted only to migrate
  it: `ValidarCredenciales` re-saves it in the new format on the next successful login, so no one
  has to recreate their account. Raising `Iteraciones` later upgrades accounts the same way.
- Tests lower `Contrasenas.Iteraciones` (internal, `InternalsVisibleTo`) in `BaseDePrueba`'s
  static constructor; every test creates a user and the real cost would add a minute.

## Control Laboral: calculated vs paid

`Models/CalculoPeriodo` is the one pure calculation used by the summary, the close and the
payslip comparison. It mirrors the employer's payroll line by line, as reconstructed from two
real payslips (`CalculoPeriodoTests` reproduces both to the cent): fixed salary = a fixed fraction
of the monthly (half per fortnight, **not** prorated by days); rate = hour × factor rounded to the
cent, total = hours × rate; **bruto excludes viáticos**, the deduction applies to bruto, and
viáticos are added to the net. The payroll rounds inconsistently, so comparisons tolerate < ₡1
(`LineaComparacion.Tolerancia`).

- `ConsolidarPeriodoLaboral(uid, ini, fin)` computes from the stored records itself and freezes
  the parameters; it takes no figures from the caller.
- **Never overwrite calculated figures with paid ones.** The old "Editar cierre" did exactly
  that and left nothing to compare against; it was replaced by the payslip form.
- The payslip form starts empty on purpose: pre-filling it with the calculated values would hide
  every difference.
- **Closing and getting paid are independent.** "Registrar ingreso" stays available after a period
  is closed (the pay arrives days later) until that period has its income:
  `IngresoLaboralDirecto.PeriodoInicio` links an income to its period, `IngresoDelPeriodo` finds it
  (only while its transaction exists — deleting it from Transacciones makes the period pending
  again), and a second income for the same period is rejected. In the history a linked income is
  shown in its period's row ("Ingreso" column), never as its own row: that double-counted the
  money in the year's total.

## Integrity verification

`Transacciones` is the ledger, and since D1/D3 account and card balances are derived from it, so
they cannot drift. What is still kept by hand: `PeriodosLaborales` and the `ResumenMensual` snapshots,
which nothing recomputes when the month behind them changes. Goal earmarks are derived like balances (D5). Dangling references are prevented by the foreign
keys (see "Foreign keys" above) and reported by `PRAGMA foreign_key_check`.

`AppDatabase.VerificarIntegridad()` is the missing reconciliation. It is **read-only — it
reports, it never repairs** — and returns `List<ProblemaIntegridad>` covering: `PRAGMA
foreign_key_check` for every declared relation at once, the polymorphic `PagosMensuales.FijoId`
that no key can express, impossible states (payment marked paid with no transaction, income
booked against a credit card, a card paid for more than was bought on it, fixed items whose two
due days are equal), categories no longer in the table, and stale `ResumenMensual` snapshots.

The stored-vs-computed balance checks it began with are gone: once D1 and D3 made those figures
derived, neither could ever fire again. That is the pattern to expect here — when a class of bug
becomes structurally impossible, its check should be deleted, not kept as reassurance.

Surfaced in the UI under **Admin → Diagnóstico de integridad**. A healthy database returns an
empty list; that empty list is the exit criterion for any refactor of the money paths.

## Key Patterns

- **Placeholder text on TextBox:** uses the `Tag` property — the style shows `Tag` as greyed hint when `Text == ""`.
- **Pie/Bar charts:** `PieChartControl`/`BarChartControl` have a `Slices`/data DependencyProperty. Any assignment triggers `Redraw()` which rebuilds Canvas `Path`/shape elements.
- **Goals VM wrapper:** `MetaAhorroVm` (in `MetasViewModel.cs`) wraps `MetaAhorro` with its own amount field, the Apartar / Usar / Liberar commands and its own error message, so each card reports errors next to itself.
- **Monto coloring in DataGrid:** done with inline `DataTrigger` on `Tipo == "Gasto"` — no converter needed.
- **PDF exports:** `PdfExportService` (general finance report) and `PdfLaboralService` (Control Laboral payroll report) build documents with QuestPDF's fluent `Document.Create` API and save to `MyDocuments`.

## Themes ("Océano" dark / light)

Two themes, switched live from the sidebar (Cuenta → Tema claro/oscuro), saved per user in
`Usuarios.Tema`; the last one chosen is also stored as `Configuracion('tema')`, which the login
window uses before anyone has signed in.

- **`Services/Temas.cs` is the only source of colours.** Each `Paleta` lists every named colour;
  `Temas.Aplicar` turns it into resources (`BgBrush`, `SurfaceBrush`, `CardBrush`, `BorderBrush`,
  `AccentBrush`, `TextBrush`, `TextMutedBrush`, `GreenBrush`, `RedBrush`, the `*TintBrush` soft
  backgrounds, `HoverBrush`/`SelectedBrush` for rows…) and swaps MahApps between `Dark.Blue` and
  `Light.Blue`. App.xaml no longer defines any brush.
- **XAML must use `{DynamicResource XBrush}`**, never `StaticResource` or a literal hex: those are
  resolved once and keep the theme the window opened with. The only literals left on purpose are
  the logo and the yellow Dashboard alerts (dark text on yellow reads in both themes).
- **C# that picks a colour by data** (green/red balance, budget, diagnostics, charts) reads
  `Temas.Actual`. Those values are computed when the view is built, so `MainWindow` discards its
  cached views on a theme change and re-shows the current section. Without WPF (tests)
  `Temas.Actual` is the dark palette, which is why tests can assert dark hex values.
- Hex with alpha is `#AARRGGBB` in WPF: a 22 % tint of `#4ADE80` is `#224ADE80`, not `#4ADE8022`.
