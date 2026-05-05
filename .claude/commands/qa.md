Ejecuta un QA completo del proyecto DMT Finance (WPF/.NET 10 + SQLite).
Trabaja siempre desde el directorio raíz del proyecto: F:\Claude\FinanzasPersonales

Sigue EXACTAMENTE estos pasos en orden y acumula los resultados para el reporte final.

---

## PASO 1 — Build

Ejecuta: `dotnet build 2>&1`

- Si termina con "Compilación correcta" y "0 Errores" → ✅ BUILD OK
- Si hay errores de compilación → ❌ BUILD FALLIDO — lista cada error con archivo y línea
- Si hay advertencias → ⚠️ lista las advertencias

---

## PASO 2 — Verificación del ejecutable

Verifica que exista: `bin\Debug\net10.0-windows\FinanzasPersonales.exe`

- Si existe → ✅ EJECUTABLE PRESENTE
- Si no existe → ❌ EJECUTABLE NO ENCONTRADO

---

## PASO 3 — Análisis estático de bindings XAML

Busca en todos los archivos `Views\*.xaml` y `MainWindow.xaml` los siguientes patrones problemáticos.
Para cada uno, reporta el archivo y número de línea si se encuentra.

**3a.** Bindings sin Path ni valor (`{Binding}` solo, que no sea intencional como IsChecked en checkboxes de toggle):
```
Grep: \{Binding\s*\}  en archivos *.xaml
```
Ignora los casos donde el binding vacío es en `IsChecked` de CheckBox con Command (es intencional).

**3b.** Comandos referenciados en XAML — verifica que existan como propiedad `ICommand` en su ViewModel:
- Extrae todos los valores de `Command="{Binding ...Command"` de los XAML
- Para cada uno, busca en el ViewModel correspondiente que esa propiedad exista (`public ICommand NombreCommand`)

**3c.** Propiedades en bindings — verifica que no haya typos obvios buscando nombres que no aparezcan en ningún ViewModel:
- Extrae valores de `Text="{Binding Xyz}"`, `Visibility="{Binding Xyz}"`, `ItemsSource="{Binding Xyz}"`
- Cruza con las propiedades públicas de los ViewModels

Si no encuentras ningún problema → ✅ XAML BINDINGS OK
Si encuentras posibles problemas → ⚠️ lista cada uno con contexto

---

## PASO 4 — Verificación de esquema de base de datos

Lee `Data\AppDatabase.cs` y verifica que el método de inicialización cree TODAS estas tablas:

| Tabla | Campos obligatorios |
|---|---|
| Configuracion | Clave, Valor |
| Usuarios | Id, NombreUsuario, PasswordHash, Rol, Activo, FechaCreacion |
| Transacciones | Id, UsuarioId, Tipo, Monto, Categoria, Descripcion, Fecha, Notas, CuentaNombre, TarjetaCreditoId, CuentaId |
| MetasAhorro | Id, UsuarioId, Nombre, MontoObjetivo, MontoActual, FechaLimite |
| GastosFijos | Id, UsuarioId, Nombre, Monto, DiaVencimiento, DiaVencimiento2, Activo |
| IngresosFijos | Id, UsuarioId, Nombre, Monto, DiaIngreso, DiaIngreso2, Activo, CuentaId |
| Categorias | Id, UsuarioId, Nombre |
| Cuentas | Id, UsuarioId, Nombre, Tipo, Banco, Activa, SaldoActual |
| TarjetasCredito | Id, UsuarioId, Nombre, LimiteCredito, SaldoUsado, DiaCierre, DiaPago, FechaUltimoCorte |
| PagosMensuales | Id, UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia, Pagado, MetodoPago, TransaccionId |
| ConfiguracionLaboral | UsuarioId, SalarioPorHora, JornadaSemanal, ModoPago, DiaPago, DiaPago2, DiaSemana, Viaticos |
| RegistrosDiasLaborales | Id, UsuarioId, Fecha, HorasNormales, HorasExtraDiurnas, HorasExtraNocturnas, HorasDobles, EsFeriado, EsAusencia, TieneGoceSalario, Viaticos |
| PeriodosLaborales | Id, UsuarioId, Anio, Mes, SalarioBruto, Deducciones, SalarioNeto, Cerrado, TransaccionId |
| ResumenMensual | Id, UsuarioId, Anio, Mes, Ingresos, Gastos, Balance, BalanceAcumulado, NumTransacciones, PeriodoLaboral, FechaCierre, Notas |

Verifica también:
- Que `UNIQUE(UsuarioId, Nombre)` exista en Cuentas, TarjetasCredito y Categorias
- Que `UNIQUE(UsuarioId, Fecha)` exista en RegistrosDiasLaborales
- Que `UNIQUE(UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia)` exista en PagosMensuales
- Que la semilla de Configuracion incluya: moneda='₡', max_usuarios='20', presupuesto_mensual='500000'
- Que existan migraciones para columnas añadidas después (CuentaId en Transacciones, CuentaId en IngresosFijos, SaldoActual en Cuentas)

✅ por cada tabla correcta / ❌ por cada tabla o campo faltante

---

## PASO 5 — Pruebas de operaciones críticas con SQLite

Crea una base de datos SQLite temporal en memoria ejecutando los siguientes bloques SQL con `sqlite3`. Si sqlite3 no está disponible en PATH, usa PowerShell con el módulo Microsoft.Data.Sqlite cargando el DLL desde los paquetes NuGet del proyecto.

Si ninguna opción está disponible, busca sqlite3.exe en las rutas de paquetes NuGet del proyecto:
`%USERPROFILE%\.nuget\packages\microsoft.data.sqlite.core\**\runtimes\**\native\e_sqlite3.dll`
y reporta ⚠️ NO SE PUEDE EJECUTAR SQL DIRECTAMENTE — VERIFICACIÓN MANUAL REQUERIDA para este paso.

Si sí puedes ejecutar SQL, crea un script temporal y ejecútalo:

```sql
-- Crear esquema mínimo
CREATE TABLE Cuentas (Id INTEGER PRIMARY KEY, UsuarioId INTEGER, Nombre TEXT, SaldoActual REAL DEFAULT 0, Tipo TEXT DEFAULT 'Débito', Banco TEXT DEFAULT '', Activa INTEGER DEFAULT 1);
CREATE TABLE TarjetasCredito (Id INTEGER PRIMARY KEY, UsuarioId INTEGER, Nombre TEXT, LimiteCredito REAL DEFAULT 0, SaldoUsado REAL DEFAULT 0, DiaCierre INTEGER DEFAULT 15, DiaPago INTEGER DEFAULT 5, FechaUltimoCorte TEXT DEFAULT '');
CREATE TABLE Transacciones (Id INTEGER PRIMARY KEY AUTOINCREMENT, UsuarioId INTEGER, Tipo TEXT, Monto REAL, Categoria TEXT, Descripcion TEXT DEFAULT '', Fecha TEXT, Notas TEXT DEFAULT '', CuentaNombre TEXT DEFAULT 'Efectivo', TarjetaCreditoId INTEGER, CuentaId INTEGER);
CREATE TABLE GastosFijos (Id INTEGER PRIMARY KEY AUTOINCREMENT, UsuarioId INTEGER, Nombre TEXT, Monto REAL, DiaVencimiento INTEGER, DiaVencimiento2 INTEGER DEFAULT 0, Activo INTEGER DEFAULT 1);
CREATE TABLE PagosMensuales (Id INTEGER PRIMARY KEY AUTOINCREMENT, UsuarioId INTEGER, TipoFijo TEXT, FijoId INTEGER, Anio INTEGER, Mes INTEGER, Dia INTEGER DEFAULT 1, Pagado INTEGER DEFAULT 0, MetodoPago TEXT DEFAULT '', TransaccionId INTEGER, UNIQUE(UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia));
CREATE TABLE ResumenMensual (Id INTEGER PRIMARY KEY AUTOINCREMENT, UsuarioId INTEGER, Anio INTEGER, Mes INTEGER, Ingresos REAL DEFAULT 0, Gastos REAL DEFAULT 0, Balance REAL DEFAULT 0, BalanceAcumulado REAL DEFAULT 0, NumTransacciones INTEGER DEFAULT 0, PeriodoLaboral INTEGER DEFAULT 0, FechaCierre TEXT DEFAULT '', Notas TEXT DEFAULT '', UNIQUE(UsuarioId, Anio, Mes));

-- Insertar datos de prueba
INSERT INTO Cuentas VALUES (1, 1, 'BAC Colones', 100000, 'Débito', 'BAC', 1);
INSERT INTO TarjetasCredito VALUES (1, 1, 'BAC Visa', 500000, 0, 15, 5, '');

-- TEST 5a: Insertar transacción de gasto con cuenta y verificar saldo
INSERT INTO Transacciones (UsuarioId, Tipo, Monto, Categoria, Descripcion, Fecha, CuentaNombre, CuentaId) VALUES (1, 'Gasto', 25000, 'Alimentación', 'Supermercado', date('now'), 'BAC Colones', 1);
UPDATE Cuentas SET SaldoActual = SaldoActual - 25000 WHERE Id = 1;
SELECT 'TEST_5A', SaldoActual, CASE WHEN SaldoActual = 75000 THEN 'PASS' ELSE 'FAIL' END FROM Cuentas WHERE Id = 1;

-- TEST 5b: Eliminar la transacción y verificar reversión del saldo
DELETE FROM Transacciones WHERE Id = last_insert_rowid();
UPDATE Cuentas SET SaldoActual = SaldoActual + 25000 WHERE Id = 1;
SELECT 'TEST_5B', SaldoActual, CASE WHEN SaldoActual = 100000 THEN 'PASS' ELSE 'FAIL' END FROM Cuentas WHERE Id = 1;

-- TEST 5c: Marcar gasto fijo como pagado con cuenta bancaria
INSERT INTO GastosFijos VALUES (1, 1, 'Internet', 30000, 5, 0, 1);
INSERT INTO PagosMensuales (UsuarioId, TipoFijo, FijoId, Anio, Mes, Dia, Pagado) VALUES (1, 'Gasto', 1, strftime('%Y', 'now'), strftime('%m', 'now'), 5, 0);
-- Simular MarcarGastoFijoPagado: crear transacción + guardar TransaccionId + actualizar saldo
INSERT INTO Transacciones (UsuarioId, Tipo, Monto, Categoria, Descripcion, Fecha, CuentaNombre, CuentaId) VALUES (1, 'Gasto', 30000, 'Gastos Fijos', 'Internet', date('now'), 'BAC Colones', 1);
UPDATE PagosMensuales SET Pagado=1, MetodoPago='BAC Colones', TransaccionId=last_insert_rowid() WHERE FijoId=1;
UPDATE Cuentas SET SaldoActual = SaldoActual - 30000 WHERE Id = 1;
SELECT 'TEST_5C_SALDO', SaldoActual, CASE WHEN SaldoActual = 70000 THEN 'PASS' ELSE 'FAIL' END FROM Cuentas WHERE Id = 1;
SELECT 'TEST_5C_TRANSID', TransaccionId IS NOT NULL, CASE WHEN TransaccionId IS NOT NULL THEN 'PASS' ELSE 'FAIL' END FROM PagosMensuales WHERE FijoId=1;

-- TEST 5d: Desmarcar gasto fijo y verificar reversión
SELECT @tid := TransaccionId FROM PagosMensuales WHERE FijoId=1;
-- SQLite no tiene variables, usamos subquery:
DELETE FROM Transacciones WHERE Id = (SELECT TransaccionId FROM PagosMensuales WHERE FijoId=1);
UPDATE Cuentas SET SaldoActual = SaldoActual + 30000 WHERE Id = 1;
UPDATE PagosMensuales SET Pagado=0, MetodoPago='', TransaccionId=NULL WHERE FijoId=1;
SELECT 'TEST_5D_SALDO', SaldoActual, CASE WHEN SaldoActual = 100000 THEN 'PASS' ELSE 'FAIL' END FROM Cuentas WHERE Id = 1;
SELECT 'TEST_5D_TRANSID', TransaccionId IS NULL, CASE WHEN TransaccionId IS NULL THEN 'PASS' ELSE 'FAIL' END FROM PagosMensuales WHERE FijoId=1;

-- TEST 5e: Cierre de mes
INSERT INTO Transacciones (UsuarioId, Tipo, Monto, Categoria, Descripcion, Fecha, CuentaNombre) VALUES (1, 'Ingreso', 500000, 'Salario', 'Salario enero', '2026-01-15', 'Efectivo');
INSERT INTO Transacciones (UsuarioId, Tipo, Monto, Categoria, Descripcion, Fecha, CuentaNombre) VALUES (1, 'Gasto', 120000, 'Alimentación', 'Supermercado', '2026-01-20', 'Efectivo');
INSERT INTO ResumenMensual (UsuarioId, Anio, Mes, Ingresos, Gastos, Balance, BalanceAcumulado, NumTransacciones, FechaCierre)
  VALUES (1, 2026, 1,
    (SELECT SUM(Monto) FROM Transacciones WHERE UsuarioId=1 AND Tipo='Ingreso' AND strftime('%Y-%m', Fecha)='2026-01' AND TarjetaCreditoId IS NULL),
    (SELECT SUM(Monto) FROM Transacciones WHERE UsuarioId=1 AND Tipo='Gasto'   AND strftime('%Y-%m', Fecha)='2026-01' AND TarjetaCreditoId IS NULL),
    380000, 380000,
    (SELECT COUNT(*) FROM Transacciones WHERE UsuarioId=1 AND strftime('%Y-%m', Fecha)='2026-01'),
    datetime('now'));
SELECT 'TEST_5E_CIERRE', Ingresos, Gastos, Balance,
  CASE WHEN Ingresos=500000 AND Gastos=120000 AND Balance=380000 THEN 'PASS' ELSE 'FAIL' END
FROM ResumenMensual WHERE Anio=2026 AND Mes=1;
-- Test idempotencia: insertar de nuevo debe ignorar
INSERT OR IGNORE INTO ResumenMensual (UsuarioId, Anio, Mes, Ingresos, Gastos, Balance, BalanceAcumulado, NumTransacciones, FechaCierre)
  VALUES (1, 2026, 1, 0, 0, 0, 0, 0, datetime('now'));
SELECT 'TEST_5E_IDEMPOTENTE', COUNT(*), CASE WHEN COUNT(*)=1 THEN 'PASS' ELSE 'FAIL' END FROM ResumenMensual WHERE Anio=2026 AND Mes=1;
```

Para cada TEST_5x reporta PASS → ✅ o FAIL → ❌ con el valor obtenido vs esperado.

---

## PASO 6 — Reporte final

Al finalizar todos los pasos, muestra un resumen con este formato exacto:

```
═══════════════════════════════════════════════
  DMT Finance — QA Report
═══════════════════════════════════════════════

BUILD
  [✅/❌] Compilación .NET

EJECUTABLE
  [✅/❌] FinanzasPersonales.exe presente

XAML BINDINGS
  [✅/⚠️/❌] Comandos enlazados correctamente
  [✅/⚠️/❌] Propiedades sin typos detectados

ESQUEMA DE BASE DE DATOS
  [✅/❌] Tabla Configuracion
  [✅/❌] Tabla Usuarios
  [✅/❌] Tabla Transacciones
  [✅/❌] Tabla MetasAhorro
  [✅/❌] Tabla GastosFijos
  [✅/❌] Tabla IngresosFijos
  [✅/❌] Tabla Categorias
  [✅/❌] Tabla Cuentas
  [✅/❌] Tabla TarjetasCredito
  [✅/❌] Tabla PagosMensuales
  [✅/❌] Tabla ConfiguracionLaboral
  [✅/❌] Tabla RegistrosDiasLaborales
  [✅/❌] Tabla PeriodosLaborales
  [✅/❌] Tabla ResumenMensual
  [✅/❌] Restricciones UNIQUE
  [✅/❌] Migraciones de columnas
  [✅/❌] Semilla de Configuracion

OPERACIONES CRÍTICAS
  [✅/❌] 5a — Insertar transacción actualiza SaldoActual
  [✅/❌] 5b — Eliminar transacción revierte SaldoActual
  [✅/❌] 5c — Gasto fijo pagado: saldo descontado + TransaccionId guardado
  [✅/❌] 5d — Gasto fijo desmarcado: saldo revertido + TransaccionId limpiado
  [✅/❌] 5e — Cierre de mes genera ResumenMensual correcto
  [✅/❌] 5e — Cierre de mes es idempotente

───────────────────────────────────────────────
  ✅ X  ⚠️ X  ❌ X
───────────────────────────────────────────────
```

Si hay ❌ o ⚠️, lista debajo del reporte los detalles de cada problema con el archivo y número de línea donde se debe corregir.
