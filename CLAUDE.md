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

## Architecture

**Pattern:** MVVM (manual `INotifyPropertyChanged` — no CommunityToolkit).

```
Models/          Pure data classes (Transaccion, MetaAhorro, Usuario, PieSlice…)
Data/            AppDatabase.cs — all SQLite CRUD via Microsoft.Data.Sqlite
Services/        SessionService (auth state: UsuarioId, Rol, EsAdmin)
                 AppSettings (runtime config: Moneda symbol)
ViewModels/      BaseViewModel, RelayCommand/RelayCommand<T>, one VM per view
Controls/        PieChartControl — custom donut chart drawn on WPF Canvas
Views/           UserControls per section (Dashboard, Transacciones, Admin…)
Windows/         LoginWindow, RegistroWindow
MainWindow        Sidebar nav + ContentControl; views created lazily and cached
App.xaml         All WPF styles/themes (dark palette, Button variants, DataGrid…)
```

## Navigation

`MainWindow.xaml.cs` owns three cached `UserControl` instances. The active nav button is indicated by setting `IsEnabled=false` (which triggers an "active" visual in `NavButton` style). Navigating to a view calls `.Actualizar()` on it to refresh data from SQLite.

## Database

SQLite file lives in `%APPDATA%\FinanzasPersonales\finanzas.db`. Schema:
- `Usuarios (Id, NombreUsuario, PasswordHash, Rol, Activo, FechaCreacion)` — Rol: "Admin"|"Normal"
- `Transacciones (Id, UsuarioId, Tipo, Monto, Categoria, Descripcion, Fecha)`
- `MetasAhorro (Id, UsuarioId, Nombre, MontoObjetivo, MontoActual, FechaLimite)`
- `TarjetasCredito (Id, UsuarioId, Nombre, ...)` — UNIQUE(UsuarioId, Nombre)
- `GastosFijos (Id, UsuarioId, ...)`, `IngresosFijos (Id, UsuarioId, ...)`
- `Categorias (Id, UsuarioId, Nombre)` — UsuarioId=NULL means global (all users see it)

`AppDatabase` opens a new connection per operation (no connection pooling / EF Core).

## Key Patterns

- **Placeholder text on TextBox:** uses the `Tag` property — the style shows `Tag` as greyed hint when `Text == ""`.
- **Pie chart:** `PieChartControl` has a `Slices` DependencyProperty (`IEnumerable<PieSlice>`). Any assignment triggers `Redraw()` which rebuilds Canvas `Path` elements as donut segments.
- **Goals VM wrapper:** `MetaAhorroVm` (nested in `MetasViewModel.cs`) wraps `MetaAhorro` and adds `MontoAgregar` + `AgregarDineroCommand` so each card has its own text field binding.
- **Monto coloring in DataGrid:** done with inline `DataTrigger` on `Tipo == "Gasto"` — no converter needed.

## Dark Theme Palette

| Key | Hex | Use |
|---|---|---|
| `BgBrush` | `#1E1E2E` | Window background |
| `SurfaceBrush` | `#24273A` | Sidebar, inputs |
| `CardBrush` | `#2A2D3E` | Cards |
| `AccentBrush` | `#7C3AED` | Primary actions |
| `GreenBrush` | `#A6E3A1` | Ingresos |
| `RedBrush` | `#F38BA8` | Gastos / danger |
