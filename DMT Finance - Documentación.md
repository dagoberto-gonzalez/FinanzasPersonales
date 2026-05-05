# DMT Finance — Documentación Completa

> Aplicación de finanzas personales desarrollada en WPF (.NET 10) con SQLite.
> Documentación generada el 11 de abril de 2026.

---

## Tabla de contenidos

1. [Descripción general](#1-descripción-general)
2. [Arquitectura del proyecto](#2-arquitectura-del-proyecto)
3. [Base de datos](#3-base-de-datos)
4. [Módulos y funcionalidades](#4-módulos-y-funcionalidades)
5. [Lógica de Cuentas](#5-lógica-de-cuentas)
6. [Lógica de cierre de mes](#6-lógica-de-cierre-de-mes)
7. [Flujo de usuario paso a paso](#7-flujo-de-usuario-paso-a-paso)

---

## 1. Descripción general

**DMT Finance** es una aplicación de escritorio para Windows que permite a uno o varios usuarios gestionar sus finanzas personales de forma integral. Está orientada al mercado costarricense (moneda por defecto: colón ₡, cálculos de deducciones según la CCSS) y corre completamente de forma local: no requiere internet ni servidores externos.

### Propósito principal

- Registrar ingresos y gastos con categorías personalizadas.
- Controlar múltiples cuentas bancarias y tarjetas de crédito.
- Dar seguimiento a gastos fijos e ingresos fijos recurrentes mes a mes.
- Gestionar metas de ahorro con seguimiento de progreso.
- Llevar un control detallado de horas laborales, horas extra y salario con deducciones legales.
- Generar reportes visuales y exportar informes en PDF.
- Cerrar cada mes como snapshot histórico inmutable del estado financiero.

### Tecnologías utilizadas

| Tecnología | Versión | Uso |
|---|---|---|
| .NET / WPF | 10.0-windows | Framework UI (solo Windows) |
| Microsoft.Data.Sqlite | 9.0.3 | Base de datos local |
| QuestPDF | 2025.1.0 | Exportación de reportes en PDF |
| MahApps.Metro | 2.4.11 | Componentes UI adicionales |

---

## 2. Arquitectura del proyecto

### Patrón de diseño: MVVM manual

La aplicación sigue el patrón **Model–View–ViewModel (MVVM)** implementado sin frameworks externos:

- `INotifyPropertyChanged` implementado manualmente en `BaseViewModel`.
- Comandos implementados con `RelayCommand` y `RelayCommand<T>`.
- Las vistas (`Views/`) no contienen lógica de negocio: solo bindings y eventos de UI.

### Estructura de carpetas

```
FinanzasPersonales/
├── App.xaml / App.xaml.cs           # Punto de entrada, estilos globales, paleta de colores
├── MainWindow.xaml / .cs            # Ventana principal: sidebar + área de contenido
│
├── Models/                          # Clases de datos puras (sin lógica de persistencia)
│   ├── Usuario.cs
│   ├── Transaccion.cs
│   ├── Categoria.cs
│   ├── MetaAhorro.cs
│   ├── TarjetaCredito.cs
│   ├── Cuenta.cs
│   ├── GastoFijo.cs
│   ├── IngresoFijo.cs
│   ├── ConfiguracionLaboral.cs
│   ├── RegistroDiaLaboral.cs
│   ├── PeriodoLaboral.cs
│   ├── ResumenMensual.cs
│   ├── BarMonth.cs                  # DTO para gráfico de barras
│   └── PieSlice.cs                  # DTO para gráfico de pastel
│
├── Data/
│   └── AppDatabase.cs               # Toda la capa de acceso a datos (SQLite, sin ORM)
│
├── Services/
│   ├── SessionService.cs            # Estado de sesión global (estático)
│   ├── AppSettings.cs               # Configuración en tiempo de ejecución (moneda)
│   ├── PdfExportService.cs          # Exportación de reportes financieros a PDF
│   └── PdfLaboralService.cs         # Exportación de reportes laborales a PDF
│
├── ViewModels/
│   ├── BaseViewModel.cs             # INotifyPropertyChanged base
│   ├── RelayCommand.cs              # ICommand genérico
│   ├── GrupoDia.cs                  # Helper para agrupar por día
│   ├── LoginViewModel.cs
│   ├── RegistroViewModel.cs
│   ├── DashboardViewModel.cs
│   ├── TransaccionesViewModel.cs
│   ├── CuentasViewModel.cs
│   ├── MetasViewModel.cs
│   ├── GastosFijosViewModel.cs
│   ├── IngresosFijosViewModel.cs
│   ├── TarjetasViewModel.cs
│   ├── CategoriasViewModel.cs
│   ├── ControlLaboralViewModel.cs
│   ├── ReportesViewModel.cs
│   ├── AdminViewModel.cs
│   └── CambiarPasswordViewModel.cs
│
├── Views/                           # UserControls (una por sección)
│   ├── DashboardView.xaml / .cs
│   ├── TransaccionesView.xaml / .cs
│   ├── CuentasView.xaml / .cs
│   ├── MetasView.xaml / .cs
│   ├── GastosFijosView.xaml / .cs
│   ├── IngresosFijosView.xaml / .cs
│   ├── TarjetasView.xaml / .cs
│   ├── CategoriasView.xaml / .cs
│   ├── ControlLaboralView.xaml / .cs
│   ├── ReportesView.xaml / .cs
│   ├── AdminView.xaml / .cs
│   └── CambiarPasswordView.xaml / .cs
│
├── Windows/
│   ├── LoginWindow.xaml / .cs       # Ventana de autenticación
│   └── RegistroWindow.xaml / .cs    # Ventana de registro de usuario
│
└── Controls/
    ├── PieChartControl.xaml / .cs   # Gráfico de dona personalizado (Canvas + Path)
    └── BarChartControl.xaml / .cs   # Gráfico de barras mensuales
```

### Navegación en MainWindow

`MainWindow` mantiene instancias cacheadas de cada vista (se crean al primer acceso — inicialización lazy). El botón activo en el sidebar se indica poniendo `IsEnabled = false`, lo que activa un estilo visual "activo" en el `NavButton`. Al navegar, se llama `.Actualizar()` en la vista destino para refrescar datos desde SQLite.

```
Sidebar (botones nav) → NavigateTo(view) → ContentControl muestra la vista
                                         → view.Actualizar() recarga datos
```

### Paleta de colores (tema oscuro)

| Clave | Hex | Uso |
|---|---|---|
| `BgBrush` | `#1E1E2E` | Fondo principal de la ventana |
| `SurfaceBrush` | `#24273A` | Sidebar, inputs |
| `CardBrush` | `#2A2D3E` | Tarjetas y paneles |
| `AccentBrush` | `#7C3AED` | Botones primarios, énfasis |
| `GreenBrush` | `#A6E3A1` | Ingresos, valores positivos |
| `RedBrush` | `#F38BA8` | Gastos, peligro, vencimientos |

### Punto de entrada (`App.xaml.cs`)

```
1. Inicializar AppDatabase (crea el archivo .db si no existe)
2. Mostrar LoginWindow
3. Si login exitoso → mostrar MainWindow
4. Si login fallido o cancelado → cerrar aplicación
```

---

## 3. Base de datos

### Ubicación del archivo

```
%APPDATA%\FinanzasPersonales\finanzas.db
# Ejemplo: C:\Users\Dagoberto González\AppData\Roaming\FinanzasPersonales\finanzas.db
```

La aplicación abre una conexión nueva por operación (no usa connection pooling ni Entity Framework Core). Las contraseñas se almacenan como hash SHA-256.

---

### Tablas

#### `Configuracion`

Almacena parámetros globales de la aplicación.

| Campo | Tipo | Descripción |
|---|---|---|
| `Clave` | TEXT PK | Nombre del parámetro |
| `Valor` | TEXT | Valor del parámetro |

Valores por defecto:

| Clave | Valor por defecto | Descripción |
|---|---|---|
| `moneda` | `₡` | Símbolo de moneda |
| `max_usuarios` | `20` | Límite de usuarios registrados |
| `presupuesto_mensual` | `500000` | Presupuesto mensual de referencia |

---

#### `Usuarios`

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | Identificador único |
| `NombreUsuario` | TEXT UNIQUE NOT NULL | Nombre de usuario |
| `PasswordHash` | TEXT NOT NULL | Hash SHA-256 de la contraseña |
| `Rol` | TEXT NOT NULL DEFAULT `'Normal'` | `'Admin'` o `'Normal'` |
| `Activo` | INTEGER NOT NULL DEFAULT `1` | `1` = activo, `0` = desactivado |
| `FechaCreacion` | TEXT DEFAULT `date('now')` | Fecha de registro |

> El primer usuario registrado en la aplicación recibe automáticamente el rol `Admin`. Los siguientes reciben rol `Normal`.

---

#### `Transacciones`

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | Identificador único |
| `UsuarioId` | INTEGER NOT NULL DEFAULT `1` | Usuario propietario |
| `Tipo` | TEXT NOT NULL | `'Ingreso'` o `'Gasto'` |
| `Monto` | REAL NOT NULL | Monto de la transacción |
| `Categoria` | TEXT NOT NULL | Categoría |
| `Descripcion` | TEXT DEFAULT `''` | Descripción breve |
| `Fecha` | TEXT NOT NULL | Fecha en formato ISO |
| `Notas` | TEXT DEFAULT `''` | Notas adicionales |
| `CuentaNombre` | TEXT DEFAULT `'Efectivo'` | Cuenta o método de pago |
| `TarjetaCreditoId` | INTEGER | FK a `TarjetasCredito` (opcional) |
| `CuentaId` | INTEGER | FK a `Cuentas` (opcional) |

---

#### `MetasAhorro`

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | Identificador único |
| `UsuarioId` | INTEGER NOT NULL DEFAULT `1` | Usuario propietario |
| `Nombre` | TEXT NOT NULL | Nombre de la meta |
| `MontoObjetivo` | REAL NOT NULL | Meta total a alcanzar |
| `MontoActual` | REAL DEFAULT `0` | Monto ahorrado hasta ahora |
| `FechaLimite` | TEXT NOT NULL | Fecha límite para alcanzar la meta |

---

#### `GastosFijos`

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | Identificador único |
| `UsuarioId` | INTEGER NOT NULL DEFAULT `1` | Usuario propietario |
| `Nombre` | TEXT NOT NULL | Nombre del gasto (ej. "Internet", "Alquiler") |
| `Monto` | REAL NOT NULL | Monto a pagar |
| `DiaVencimiento` | INTEGER NOT NULL | Día del mes (1–31) en que vence |
| `DiaVencimiento2` | INTEGER DEFAULT `0` | Segundo día de vencimiento opcional (`0` = ninguno) |
| `Activo` | INTEGER DEFAULT `1` | `1` = activo |

---

#### `IngresosFijos`

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | Identificador único |
| `UsuarioId` | INTEGER NOT NULL DEFAULT `1` | Usuario propietario |
| `Nombre` | TEXT NOT NULL | Nombre del ingreso (ej. "Salario", "Alquiler cobrado") |
| `Monto` | REAL NOT NULL | Monto esperado |
| `DiaIngreso` | INTEGER NOT NULL | Día del mes (1–31) en que se recibe |
| `DiaIngreso2` | INTEGER DEFAULT `0` | Segundo día de ingreso opcional (`0` = ninguno) |
| `Activo` | INTEGER DEFAULT `1` | `1` = activo |
| `CuentaId` | INTEGER | FK a `Cuentas` — cuenta destino del depósito automático (opcional) |

---

#### `Categorias`

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | Identificador único |
| `UsuarioId` | INTEGER | FK a `Usuarios`; `NULL` = categoría global (visible para todos) |
| `Nombre` | TEXT NOT NULL | Nombre de la categoría |

Restricción: `UNIQUE(UsuarioId, Nombre)`.

Categorías globales por defecto: `Alimentación`, `Transporte`, `Salud`, `Entretenimiento`, `Educación`, `Servicios`, `Hogar`, `Ropa`, `Tecnología`, `Otros`.

---

#### `Cuentas`

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | Identificador único |
| `UsuarioId` | INTEGER NOT NULL | Usuario propietario |
| `Nombre` | TEXT NOT NULL | Nombre identificador de la cuenta |
| `Tipo` | TEXT DEFAULT `'Débito'` | Tipo: `Cuenta corriente`, `Cuenta de ahorros`, `Cuenta en dólares`, etc. |
| `Banco` | TEXT DEFAULT `''` | Nombre del banco |
| `Activa` | INTEGER DEFAULT `1` | `1` = activa |
| `SaldoActual` | REAL DEFAULT `0` | Saldo actualizado en tiempo real |

Restricción: `UNIQUE(UsuarioId, Nombre)`.

---

#### `TarjetasCredito`

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | Identificador único |
| `UsuarioId` | INTEGER NOT NULL DEFAULT `1` | Usuario propietario |
| `Nombre` | TEXT NOT NULL | Nombre identificador (ej. "BAC Visa") |
| `LimiteCredito` | REAL DEFAULT `0` | Límite de crédito aprobado |
| `SaldoUsado` | REAL DEFAULT `0` | Monto consumido en el período actual |
| `DiaCierre` | INTEGER DEFAULT `15` | Día del mes en que cierra el período de facturación |
| `DiaPago` | INTEGER DEFAULT `5` | Día del mes límite para pago |
| `FechaUltimoCorte` | TEXT DEFAULT `''` | Fecha de inicio del período actual |

Restricción: `UNIQUE(UsuarioId, Nombre)`.

---

#### `PagosMensuales`

Registra el estado mensual de cada gasto fijo o ingreso fijo (pagado/pendiente).

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | Identificador único |
| `UsuarioId` | INTEGER NOT NULL DEFAULT `1` | Usuario propietario |
| `TipoFijo` | TEXT NOT NULL | `'Gasto'` o `'Ingreso'` |
| `FijoId` | INTEGER NOT NULL | FK a `GastosFijos` o `IngresosFijos` |
| `Anio` | INTEGER NOT NULL | Año del registro |
| `Mes` | INTEGER NOT NULL | Mes del registro (1–12) |
| `Dia` | INTEGER DEFAULT `1` | Día específico dentro del mes |
| `Pagado` | INTEGER DEFAULT `0` | `0` = pendiente, `1` = pagado/recibido |
| `MetodoPago` | TEXT DEFAULT `''` | Método con el que se pagó |
| `TransaccionId` | INTEGER | FK a `Transacciones` — transacción generada automáticamente |

Restricción: `UNIQUE(UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia)`.

---

#### `ConfiguracionLaboral`

| Campo | Tipo | Descripción |
|---|---|---|
| `UsuarioId` | INTEGER PK | Un registro por usuario |
| `SalarioPorHora` | REAL DEFAULT `0` | Tarifa horaria base |
| `JornadaSemanal` | REAL DEFAULT `48` | Horas de jornada semanal ordinaria |
| `ModoPago` | TEXT DEFAULT `'Mensual'` | `Mensual`, `Quincenal` o `Semanal` |
| `DiaPago` | INTEGER DEFAULT `15` | Día de pago (modo mensual) |
| `DiaPago2` | INTEGER DEFAULT `0` | Segundo día de pago (modo quincenal) |
| `DiaSemana` | INTEGER DEFAULT `5` | Día de semana de pago (1=lunes … 7=domingo, modo semanal) |
| `Viaticos` | REAL DEFAULT `0` | Viáticos fijos por período |

---

#### `RegistrosDiasLaborales`

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | Identificador único |
| `UsuarioId` | INTEGER NOT NULL | Usuario propietario |
| `Fecha` | TEXT NOT NULL | Fecha del día registrado |
| `HorasNormales` | REAL DEFAULT `0` | Horas ordinarias |
| `HorasExtraDiurnas` | REAL DEFAULT `0` | Horas extra diurnas (recargo 50%) |
| `HorasExtraNocturnas` | REAL DEFAULT `0` | Horas extra nocturnas (recargo 75%) |
| `HorasDobles` | REAL DEFAULT `0` | Horas dobles (recargo 100%) |
| `EsFeriado` | INTEGER DEFAULT `0` | `1` = día feriado |
| `EsAusencia` | INTEGER DEFAULT `0` | `1` = ausencia del trabajador |
| `TieneGoceSalario` | INTEGER DEFAULT `0` | `1` = ausencia con goce de salario |
| `Viaticos` | REAL DEFAULT `0` | Viáticos del día |

Restricción: `UNIQUE(UsuarioId, Fecha)` — un registro por día.

---

#### `PeriodosLaborales`

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | Identificador único |
| `UsuarioId` | INTEGER NOT NULL | Usuario propietario |
| `Anio` | INTEGER NOT NULL | Año del período |
| `Mes` | INTEGER NOT NULL | Mes del período (1–12) |
| `SalarioBruto` | REAL DEFAULT `0` | Salario bruto calculado |
| `Deducciones` | REAL DEFAULT `0` | CCSS obrero: 10.67% del bruto |
| `SalarioNeto` | REAL DEFAULT `0` | Salario neto a recibir |
| `Cerrado` | INTEGER DEFAULT `0` | `1` = período cerrado (no editable) |
| `TransaccionId` | INTEGER | FK a `Transacciones` — ingreso de salario generado |

Restricción: `UNIQUE(UsuarioId, Anio, Mes)`.

---

#### `ResumenMensual`

Snapshot de cierre de mes. Una vez creado, no se modifica (idempotente).

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | Identificador único |
| `UsuarioId` | INTEGER NOT NULL | Usuario propietario |
| `Anio` | INTEGER NOT NULL | Año del cierre |
| `Mes` | INTEGER NOT NULL | Mes del cierre (1–12) |
| `Ingresos` | REAL DEFAULT `0` | Total de ingresos del mes |
| `Gastos` | REAL DEFAULT `0` | Total de gastos del mes |
| `Balance` | REAL DEFAULT `0` | `Ingresos - Gastos` |
| `BalanceAcumulado` | REAL DEFAULT `0` | Balance acumulado histórico |
| `NumTransacciones` | INTEGER DEFAULT `0` | Cantidad de transacciones del mes |
| `PeriodoLaboral` | INTEGER DEFAULT `0` | `1` = el período laboral fue cerrado este mes |
| `FechaCierre` | TEXT DEFAULT `''` | Timestamp exacto del cierre |
| `Notas` | TEXT DEFAULT `''` | Notas opcionales del usuario |

Restricción: `UNIQUE(UsuarioId, Anio, Mes)`.

---

### Relaciones entre tablas

```
Usuarios ──────────────────────────────────────────────────────────────────────┐
  │                                                                             │
  ├── Transacciones (UsuarioId)                                                 │
  │       ├── TarjetasCredito (TarjetaCreditoId) ──── Usuarios (UsuarioId) ◄───┤
  │       └── Cuentas (CuentaId) ──────────────────── Usuarios (UsuarioId) ◄───┤
  │                                                                             │
  ├── MetasAhorro (UsuarioId)                                                   │
  ├── GastosFijos (UsuarioId)                                                   │
  │       └── PagosMensuales (FijoId, TipoFijo='Gasto')                        │
  │               └── Transacciones (TransaccionId)                             │
  ├── IngresosFijos (UsuarioId)                                                 │
  │       ├── Cuentas (CuentaId) — depósito automático                          │
  │       └── PagosMensuales (FijoId, TipoFijo='Ingreso')                       │
  │               └── Transacciones (TransaccionId)                             │
  ├── Categorias (UsuarioId) ← NULL = global                                    │
  ├── ConfiguracionLaboral (UsuarioId, PK)                                      │
  ├── RegistrosDiasLaborales (UsuarioId)                                        │
  ├── PeriodosLaborales (UsuarioId)                                             │
  │       └── Transacciones (TransaccionId) — ingreso de salario               │
  └── ResumenMensual (UsuarioId)                                                │
                                                                                │
Configuracion (global, sin FK) ◄──────────────────────────────────────────────┘
```

---

## 4. Módulos y funcionalidades

### Dashboard

**Vista:** `DashboardView.xaml` | **VM:** `DashboardViewModel.cs`

El Dashboard es la pantalla de inicio y muestra un resumen completo del estado financiero actual.

**Secciones:**

| Sección | Contenido |
|---|---|
| Tarjeta de balance | Balance total del mes (verde si positivo, rojo si negativo) |
| Resumen mensual | Ingresos y gastos totales del mes actual |
| Gráfico de pastel | Distribución de gastos por categoría (excluye cargos de tarjeta) |
| Gráfico de barras | Tendencia mensual de los últimos 12 meses |
| Últimas transacciones | Las 5 transacciones más recientes |
| Gastos urgentes | Gastos fijos no pagados que vencen en ≤ 3 días |
| Tarjetas próximas a pago | Tarjetas de crédito con fecha límite en ≤ 5 días |
| Saldo por cuenta | Lista de cuentas bancarias activas con su saldo |
| Banner de cierre | Alerta si el mes anterior no fue cerrado |

**Lógica relevante:**
- Los cargos a tarjeta de crédito (`TarjetaCreditoId != null`) se excluyen del cálculo del balance y del gráfico de pastel, porque no representan flujo de efectivo real en ese momento.
- Si el mes anterior no tiene un `ResumenMensual`, aparece un banner con botón "Cerrar mes" para generar el snapshot.
- El balance acumulado considera todos los meses cerrados anteriores.

---

### Transacciones

**Vista:** `TransaccionesView.xaml` | **VM:** `TransaccionesViewModel.cs`

Módulo central para registrar cualquier movimiento de dinero.

**Formulario de nueva transacción:**

1. Tipo: **Ingreso** o **Gasto** (toggle).
2. Monto (numérico, requerido).
3. Categoría (dropdown desde `Categorias`).
4. Método de pago:
   - **Efectivo** → sin cuenta asociada.
   - **Transferencia / SINPE Móvil** → selector de cuenta bancaria.
   - **Tarjeta** (solo para gastos) → selector de tarjeta de crédito.
5. Descripción, notas, fecha.

**Al guardar:**
- Si se selecciona una **cuenta bancaria**: su `SaldoActual` aumenta (ingreso) o disminuye (gasto).
- Si se selecciona una **tarjeta de crédito**: su `SaldoUsado` aumenta.

**Filtros disponibles:**
- Texto libre (busca en descripción, notas, categoría).
- Rango de fechas (desde / hasta).
- Categoría específica.

**Al eliminar:**
- Si la transacción tenía cuenta o tarjeta asociada, el saldo se revierte automáticamente.

---

### Cuentas

**Vista:** `CuentasView.xaml` | **VM:** `CuentasViewModel.cs`

Gestiona las cuentas bancarias del usuario.

**Campos de una cuenta:**
- Nombre (único por usuario).
- Tipo: `Cuenta corriente`, `Cuenta de ahorros`, `Cuenta en dólares`, etc.
- Banco (texto libre).
- Estado: activa / inactiva.
- `SaldoActual`: se actualiza automáticamente al registrar/eliminar transacciones vinculadas.

**Operaciones:**
- Crear, editar inline, eliminar.
- El saldo se muestra en verde si es ≥ 0 y en rojo si es negativo.

---

### Tarjetas de Crédito

**Vista:** `TarjetasView.xaml` | **VM:** `TarjetasViewModel.cs`

**Campos de una tarjeta:**
- Nombre.
- Límite de crédito.
- Día de cierre del período de facturación (`DiaCierre`).
- Día límite de pago (`DiaPago`).
- `SaldoUsado`: aumenta con cada transacción vinculada.

**Lógica de períodos:**

La tarjeta calcula automáticamente el período actual usando el `DiaCierre`:

```
InicioPeriodo  = último DiaCierre que ya pasó
FinPeriodo     = próximo DiaCierre
FechaLimitePago = primer DiaPago después del FinPeriodo
DiasHastaCierre = FinPeriodo - Hoy
DiasHastaPago   = FechaLimitePago - Hoy
```

**Auto-renovación de período (`AutoRenovarPeriodo`):**
Cuando se consultan las tarjetas, si la fecha actual supera el `DiaCierre`, el `SaldoUsado` se reinicia a `0` y `FechaUltimoCorte` se actualiza. Esto simula el corte del período de facturación.

**Abono:**
Reducir el `SaldoUsado` manualmente (para registrar un pago realizado).

**Historial de movimientos:**
Al seleccionar una tarjeta, se muestran todas las transacciones vinculadas a ella.

> Para nuevos usuarios se crean automáticamente dos tarjetas BAC de ejemplo.

---

### Control Laboral

**Vista:** `ControlLaboralView.xaml` | **VM:** `ControlLaboralViewModel.cs`

Permite llevar un registro detallado de horas trabajadas y calcular el salario.

**Configuración laboral:**

| Campo | Descripción |
|---|---|
| Salario por hora | Tarifa base por hora ordinaria |
| Jornada semanal | Horas ordinarias por semana (default: 48) |
| Modo de pago | `Mensual`, `Quincenal` o `Semanal` |
| Día de pago | Según el modo de pago seleccionado |
| Viáticos | Monto fijo adicional por período |

**Registro diario:**

Cada día se pueden registrar:
- Horas normales.
- Horas extra diurnas (recargo del **50%** → factor ×1.5).
- Horas extra nocturnas (recargo del **75%** → factor ×1.75).
- Horas dobles (recargo del **100%** → factor ×2.0).
- Feriado y/o ausencia (con o sin goce de salario).
- Viáticos del día.

**Cálculo de salario bruto:**

```
Bruto = (HorasNormales × TarifaHora)
      + (HorasExtraDiurnas × TarifaHora × 1.5)
      + (HorasExtraNocturnas × TarifaHora × 1.75)
      + (HorasDobles × TarifaHora × 2.0)
      + Viáticos del período
```

**Deducciones (CCSS obrero — Ley costarricense):**

```
Deducciones = SalarioBruto × 10.67%
SalarioNeto = SalarioBruto - Deducciones
```

**Cierre de período laboral:**
- Marca el período como `Cerrado = 1`.
- Crea automáticamente una transacción de tipo `Ingreso` con el `SalarioNeto` calculado.
- El `TransaccionId` queda registrado en `PeriodosLaborales` para trazabilidad.

**Exportar PDF:**
Genera un informe con: resumen del período, desglose de horas, deducciones y tabla de registros diarios.

---

### Gastos Fijos

**Vista:** `GastosFijosView.xaml` | **VM:** `GastosFijosViewModel.cs`

Gestiona gastos recurrentes mensuales (servicios, suscripciones, alquiler, etc.).

**Características:**
- Cada gasto puede tener uno o dos días de vencimiento por mes (`DiaVencimiento`, `DiaVencimiento2`), lo que genera dos registros independientes en `PagosMensuales`.
- La lista se agrupa visualmente por día de vencimiento usando `GrupoDia<T>`.

**Estados de un gasto:**

| Estado | Condición |
|---|---|
| **Pagado** | `Pagado = 1` |
| **Urgente** | No pagado y vence en ≤ 3 días |
| **Vencido** | No pagado y la fecha ya pasó |
| **Pendiente** | No pagado, fecha futura |

**Al marcar como pagado:**
1. Se actualiza `PagosMensuales.Pagado = 1` y se guarda el `MetodoPago`.
2. Se crea automáticamente una `Transaccion` de tipo `Gasto` con la categoría y monto correspondientes.
3. Si el `MetodoPago` incluye una cuenta, se descuenta del saldo de esa cuenta.

**Al desmarcar pago:**
1. Se revierte `Pagado = 0`.
2. Se elimina la transacción generada automáticamente y se revierte el saldo de la cuenta.

---

### Ingresos Fijos

**Vista:** `IngresosFijosView.xaml` | **VM:** `IngresosFijosViewModel.cs`

Funciona de manera análoga a Gastos Fijos, pero para ingresos recurrentes.

**Diferencias clave:**
- Al marcar como recibido: se crea una transacción de tipo `Ingreso`.
- Si el ingreso tiene una `CuentaId` asociada, el monto se acredita automáticamente al saldo de esa cuenta.
- Al desmarcar: se elimina la transacción y se descuenta el saldo de la cuenta.

---

### Metas de Ahorro

**Vista:** `MetasView.xaml` | **VM:** `MetasViewModel.cs`

**Campos de una meta:**
- Nombre.
- Monto objetivo.
- Fecha límite (default: 3 meses desde hoy).
- `MontoActual`: inicia en `0`, se incrementa manualmente.

**Lógica de progreso:**

```
ProgresoPercent = Min(100%, MontoActual / MontoObjetivo × 100%)
```

La barra de progreso es **púrpura** mientras no se alcanza el objetivo y **verde** al llegar al 100%.

**Agregar dinero:**
Cada tarjeta de meta tiene un campo propio donde se ingresa el monto a añadir. Al presionar el botón, `MontoActual` aumenta en ese valor (sin generar transacción automáticamente — es solo un contador de seguimiento).

---

### Reportes

**Vista:** `ReportesView.xaml` | **VM:** `ReportesViewModel.cs`

**Secciones:**

1. **Resumen anual:** Ingresos, gastos y balance total del año seleccionado. Gráfico de barras por mes.
2. **Comparación de meses:** Selección de dos meses (cualquier año) para comparar ingresos y gastos lado a lado.
3. **Historial de cierres:** Tabla con todos los `ResumenMensual` registrados: balance, balance acumulado, número de transacciones, si el período laboral fue cerrado.
4. **Exportar PDF:** Genera un reporte financiero del año actual con resumen, tarjetas, gastos fijos y listado completo de transacciones.

---

### Administración

**Vista:** `AdminView.xaml` | **VM:** `AdminViewModel.cs`

Solo visible para usuarios con rol `Admin`.

**Gestión de usuarios:**
- Lista de todos los usuarios registrados.
- Activar / desactivar cuentas.
- Eliminar usuarios (elimina en cascada todas sus transacciones, metas, tarjetas, cuentas, gastos fijos, ingresos fijos, configuración laboral y registros).
- Un administrador no puede desactivarse ni eliminarse a sí mismo.

**Configuración global:**
- Símbolo de moneda.
- Límite máximo de usuarios (1–999).
- Se aplica inmediatamente a `AppSettings.Moneda`.

---

### Cambiar Contraseña

**Vista:** `CambiarPasswordView.xaml` | **VM:** `CambiarPasswordViewModel.cs`

Permite al usuario activo cambiar su propia contraseña. Requiere:
- Contraseña actual correcta.
- Nueva contraseña de al menos 6 caracteres.
- Confirmación coincidente.

---

### Categorías

**Vista:** `CategoriasView.xaml` | **VM:** `CategoriasViewModel.cs`

- Los usuarios pueden crear **categorías personales** (solo visibles para ellos).
- Los administradores pueden crear **categorías globales** (`UsuarioId = NULL`), que son visibles para todos los usuarios.
- Se pueden editar y eliminar las categorías propias. Los admins pueden gestionar las globales.

---

## 5. Lógica de Cuentas

### Cómo se actualiza el saldo

El `SaldoActual` de una cuenta se actualiza directamente en la base de datos cada vez que se realiza una operación que la involucra. No se recalcula desde cero leyendo el historial — se suma o resta incrementalmente.

| Operación | Efecto en saldo |
|---|---|
| Registrar un **ingreso** con cuenta | `SaldoActual += Monto` |
| Registrar un **gasto** con cuenta | `SaldoActual -= Monto` |
| Eliminar un **ingreso** con cuenta | `SaldoActual -= Monto` (reversión) |
| Eliminar un **gasto** con cuenta | `SaldoActual += Monto` (reversión) |
| Marcar ingreso fijo como recibido | `SaldoActual += Monto` |
| Desmarcar ingreso fijo como recibido | `SaldoActual -= Monto` |
| Marcar gasto fijo como pagado (con cuenta) | `SaldoActual -= Monto` |
| Desmarcar gasto fijo pagado | `SaldoActual += Monto` |
| Cerrar período laboral | `SaldoActual += SalarioNeto` (si cuenta vinculada) |

### Tipos de cuenta y comportamiento

Todos los tipos de cuenta se comportan igual internamente — la diferencia es solo descriptiva para el usuario:

- `Cuenta corriente`
- `Cuenta de ahorros`
- `Cuenta en dólares`
- `Débito` (default)

### Relación con transacciones

Una transacción puede tener:
- `CuentaId = NULL` y `TarjetaCreditoId = NULL` → **Efectivo** (no afecta saldos de cuentas).
- `CuentaId != NULL` → afecta el saldo de esa cuenta.
- `TarjetaCreditoId != NULL` → afecta el `SaldoUsado` de esa tarjeta (no el saldo de una cuenta bancaria).

### Relación con ingresos fijos

Un `IngresoFijo` puede tener un `CuentaId` asignado. Al marcarlo como recibido, el sistema crea una transacción de ingreso vinculada a esa cuenta y actualiza su saldo. Si se desmarca, revierte ambas operaciones.

---

## 6. Lógica de cierre de mes

El cierre de mes genera un snapshot inmutable del estado financiero de un mes. Es **idempotente**: si ya existe un cierre para ese mes, no se sobreescribe.

### Proceso completo

```
1. El usuario acciona "Cerrar mes" (desde el Dashboard o Reportes).
2. Se verifican los totales del mes a cerrar:
   - Ingresos y gastos de Transacciones (solo ese mes, ese usuario)
   - Número de transacciones
3. Se calcula:
   - Balance = Ingresos - Gastos
   - BalanceAcumulado = suma de todos los balances anteriores + Balance actual
4. Se inserta (o ignora si ya existe) en ResumenMensual:
   - Anio, Mes, Ingresos, Gastos, Balance, BalanceAcumulado
   - NumTransacciones
   - PeriodoLaboral = 1 si existe un PeriodoLaboral.Cerrado = 1 para ese mes
   - FechaCierre = timestamp del momento exacto del cierre
   - Notas (opcional, escritas por el usuario)
```

### Qué datos se guardan en el historial

Cada fila de `ResumenMensual` contiene:

| Campo | Descripción |
|---|---|
| `Ingresos` | Total de ingresos del mes (suma de transacciones tipo Ingreso) |
| `Gastos` | Total de gastos del mes (suma de transacciones tipo Gasto) |
| `Balance` | Diferencia: Ingresos − Gastos |
| `BalanceAcumulado` | Balance histórico hasta ese mes (corriente) |
| `NumTransacciones` | Conteo de registros en Transacciones |
| `PeriodoLaboral` | Bandera: si el período laboral de ese mes fue cerrado |
| `FechaCierre` | Timestamp exacto del cierre |
| `Notas` | Texto libre del usuario |

### Relación con el período laboral

El módulo de Control Laboral tiene su propio cierre independiente (`CerrarPeriodoLaboral`). Cuando se cierra el período laboral de un mes, el flag `PeriodoLaboral = 1` queda registrado en `ResumenMensual` al momento del cierre de mes, proporcionando un indicador de que el salario fue procesado ese período.

### Alerta de mes sin cerrar

El Dashboard detecta si el mes anterior no tiene `ResumenMensual` registrado y muestra un banner de alerta con un botón de acceso directo para cerrar el mes pendiente.

---

## 7. Flujo de usuario paso a paso

### Primer uso (nuevo usuario administrador)

```
1. Ejecutar la aplicación (FinanzasPersonales.exe)
2. Se abre LoginWindow.
3. Click en "Crear cuenta" → se abre RegistroWindow.
4. Ingresar nombre de usuario (≥ 3 caracteres) y contraseña (≥ 6 caracteres).
5. Confirmar contraseña → click en "Registrar".
6. Como es el primer usuario, recibe automáticamente rol Admin.
7. Confirmación en pantalla → volver a LoginWindow.
8. Ingresar credenciales → click en "Iniciar sesión".
9. La aplicación carga MainWindow con todas las secciones disponibles.
```

### Configuración inicial recomendada

```
1. Ir a Cuentas → crear cuentas bancarias (ej. "BAC Colones", "BCR Ahorros").
2. Ir a Tarjetas de Crédito → revisar/editar las tarjetas de ejemplo o agregar nuevas.
3. Ir a Categorías → revisar las globales, agregar categorías personales si se necesita.
4. Ir a Ingresos Fijos → registrar salario u otros ingresos recurrentes.
5. Ir a Gastos Fijos → registrar servicios, suscripciones, alquiler, etc.
6. (Opcional) Ir a Control Laboral → configurar tarifa horaria y modo de pago.
```

### Flujo mensual típico

```
Durante el mes:
├── Registrar transacciones del día a día en Transacciones.
├── En Gastos Fijos → marcar como "Pagado" cada servicio al pagarlo.
│       └── Se genera transacción automáticamente y se descuenta del saldo de cuenta.
├── En Ingresos Fijos → marcar como "Recibido" al cobrar.
│       └── Se genera transacción automáticamente y se acredita al saldo de cuenta.
├── (Laboral) Registrar horas trabajadas cada día en Control Laboral.
└── Revisar Dashboard para alertas de vencimientos y saldos.

Al final del mes:
├── Control Laboral → "Cerrar período" → calcula neto y crea ingreso de salario.
├── Dashboard o Reportes → "Cerrar mes" → genera ResumenMensual del mes.
└── Reportes → revisar historial, comparar con meses anteriores.
```

### Exportar reportes

```
Reporte financiero anual:
1. Ir a Reportes.
2. Seleccionar el año deseado.
3. Click en "Exportar PDF".
4. El PDF se guarda en: Mis Documentos/Reporte_Finanzas_{año}_{timestamp}.pdf

Reporte laboral mensual:
1. Ir a Control Laboral.
2. Seleccionar el mes.
3. Click en "Exportar PDF".
4. El PDF se guarda en: Mis Documentos/ControlLaboral_{período}_{timestamp}.pdf
```

### Gestión de múltiples usuarios (Admin)

```
1. Ir a Administración.
2. Cada usuario adicional puede crear su cuenta en LoginWindow → "Crear cuenta".
   (Limitado por max_usuarios en Configuración)
3. El Admin puede:
   - Ver la lista de usuarios.
   - Activar / desactivar cuentas.
   - Eliminar un usuario y todos sus datos.
4. Los datos de cada usuario son completamente independientes
   (cada uno ve solo sus propias transacciones, cuentas, etc.).
5. Las categorías globales son compartidas; las personales son privadas.
```

---

*Documentación generada para DMT Finance v1.0 — Abril 2026.*
