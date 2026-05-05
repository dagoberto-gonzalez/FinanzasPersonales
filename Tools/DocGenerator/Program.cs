using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

// ── Ruta de salida ────────────────────────────────────────────────────────────
var outputPath = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "FinanzasPersonales-Arquitectura.docx");

Console.WriteLine($"Generando documento: {outputPath}");

using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document, true);

// ── Partes del documento ──────────────────────────────────────────────────────
var mainPart = doc.AddMainDocumentPart();
AddStyles(mainPart);
AddNumbering(mainPart);

var body = new Body();
mainPart.Document = new Document(body);

// ═══════════════════════════════════════════════════════════════════════════════
//  CONTENIDO DEL DOCUMENTO
// ═══════════════════════════════════════════════════════════════════════════════

// ── Portada ───────────────────────────────────────────────────────────────────
H1(body, "Arquitectura y Estructura del Proyecto");
H1(body, "FinanzasPersonales");
P(body, "");
P(body, "Este documento explica cómo está organizado el proyecto FinanzasPersonales: " +
        "qué hace cada parte del código, qué tecnologías se usaron y cómo fluye la " +
        "información de la pantalla hasta la base de datos.", italic: true);
P(body, "");
Separator(body);
P(body, "");

// ── 1. ¿Qué es este proyecto? ─────────────────────────────────────────────────
H2(body, "1. ¿Qué es este proyecto?");
P(body, "FinanzasPersonales es una aplicación de escritorio para Windows que permite " +
        "a una persona llevar el control de sus finanzas personales desde su computadora. " +
        "No necesita conexión a Internet; toda la información se guarda localmente.");
P(body, "");
P(body, "Funcionalidades principales:");
Bullet(body, "Registrar ingresos y gastos (transacciones).");
Bullet(body, "Definir gastos fijos mensuales (alquiler, servicios, etc.) y marcarlos como pagados.");
Bullet(body, "Definir ingresos fijos (salario, comisiones) y marcarlos como recibidos.");
Bullet(body, "Administrar tarjetas de crédito: saldo, límite y movimientos del período.");
Bullet(body, "Crear metas de ahorro y agregar dinero gradualmente.");
Bullet(body, "Ver reportes visuales (gráficos de torta y barras) de gastos por categoría y mes.");
Bullet(body, "Personalizar categorías de ingresos y gastos.");
P(body, "");

// ── 2. Tecnologías ────────────────────────────────────────────────────────────
H2(body, "2. Tecnologías utilizadas");

H3(body, "2.1 .NET 10 + WPF");
P(body, ".NET 10 es la plataforma de desarrollo de Microsoft para aplicaciones modernas. " +
        "WPF (Windows Presentation Foundation) es el framework incluido en .NET para crear " +
        "interfaces gráficas en Windows. Se usa XAML (un lenguaje parecido al HTML) para " +
        "describir la apariencia de cada pantalla, y C# para la lógica del programa.");

H3(body, "2.2 SQLite + Microsoft.Data.Sqlite");
P(body, "SQLite es una base de datos que vive dentro de un solo archivo en el disco duro " +
        "(finanzas.db). No requiere instalar ningún servidor. Microsoft.Data.Sqlite es el " +
        "paquete NuGet oficial que permite ejecutar consultas SQL desde C#.");

H3(body, "2.3 Patrón MVVM");
P(body, "MVVM (Model–View–ViewModel) es el patrón de diseño que separa el código en tres capas:");
Bullet(body, "Model: las clases de datos puras (Transaccion, GastoFijo, TarjetaCredito…).");
Bullet(body, "View: los archivos XAML que describen la pantalla.");
Bullet(body, "ViewModel: la capa de conexión entre datos y pantalla. Contiene la lógica " +
             "y expone propiedades que la vista puede mostrar en tiempo real.");
P(body, "Gracias a este patrón, la pantalla se actualiza automáticamente cuando cambian " +
        "los datos, sin necesidad de actualizar controles manualmente.");

H3(body, "2.4 QuestPDF");
P(body, "QuestPDF es una librería para generar archivos PDF desde C#. Se usa en el módulo " +
        "de Reportes para exportar los resúmenes financieros a un archivo PDF imprimible.");
P(body, "");

// ── 3. Estructura de carpetas ─────────────────────────────────────────────────
H2(body, "3. Estructura de carpetas");
P(body, "El proyecto está organizado en carpetas, cada una con una responsabilidad clara:");
P(body, "");

H3(body, "📁 Models/");
P(body, "Contiene las clases de datos puros. Cada clase representa una entidad del sistema " +
        "y coincide con una tabla de la base de datos. No tienen lógica de negocio ni " +
        "conocen la interfaz gráfica.");
Bullet(body, "Transaccion.cs — un movimiento de dinero (ingreso o gasto).");
Bullet(body, "GastoFijo.cs — un gasto que se repite cada mes (ej. electricidad).");
Bullet(body, "IngresoFijo.cs — un ingreso mensual recurrente (ej. salario).");
Bullet(body, "TarjetaCredito.cs — una tarjeta con límite, día de cierre y saldo.");
Bullet(body, "MetaAhorro.cs — una meta con monto objetivo y monto acumulado.");
Bullet(body, "Categoria.cs — una categoría de gasto o ingreso personalizable.");
Bullet(body, "PieSlice.cs / BarMonth.cs — modelos auxiliares para los gráficos.");

H3(body, "📁 Data/");
P(body, "Contiene una sola clase: AppDatabase.cs. Esta clase maneja toda la comunicación " +
        "con SQLite: crea las tablas si no existen, inserta, actualiza, elimina y consulta datos. " +
        "No hay ORM (como Entity Framework); todo se hace con SQL directo, lo que hace el " +
        "código muy fácil de leer y depurar.");
P(body, "Cada operación abre una conexión nueva, hace lo que necesita y la cierra " +
        "(sin pool de conexiones), lo que simplifica el manejo de la base de datos.");

H3(body, "📁 ViewModels/");
P(body, "Contiene un ViewModel por cada vista (pantalla). Los ViewModels heredan de " +
        "BaseViewModel, que implementa la interfaz INotifyPropertyChanged de forma manual " +
        "(sin librerías externas como CommunityToolkit).");
Bullet(body, "BaseViewModel.cs — implementa INotifyPropertyChanged para notificar cambios.");
Bullet(body, "RelayCommand.cs — implementa ICommand para conectar botones a métodos.");
Bullet(body, "DashboardViewModel.cs — datos del panel principal.");
Bullet(body, "TransaccionesViewModel.cs — lista y filtrado de transacciones.");
Bullet(body, "GastosFijosViewModel.cs — gastos fijos, agrupados por día de vencimiento.");
Bullet(body, "IngresosFijosViewModel.cs — ingresos fijos, agrupados por día de cobro.");
Bullet(body, "TarjetasViewModel.cs — tarjetas de crédito y sus movimientos.");
Bullet(body, "MetasViewModel.cs — metas de ahorro con wrapper MetaAhorroVm por ítem.");
Bullet(body, "ReportesViewModel.cs — datos para gráficos y exportación PDF.");
Bullet(body, "CategoriasViewModel.cs — gestión de categorías personalizadas.");
Bullet(body, "GrupoDia.cs — clase auxiliar para agrupar ítems fijos por día.");

H3(body, "📁 Views/");
P(body, "Contiene los archivos XAML (y sus .xaml.cs) de cada pantalla. Cada vista es un " +
        "UserControl que recibe su ViewModel en el constructor. Las vistas no contienen " +
        "lógica de negocio; solo describen la apariencia y enlazan controles a propiedades " +
        "del ViewModel mediante bindings.");

H3(body, "📁 Controls/");
P(body, "Contiene controles WPF personalizados reutilizables:");
Bullet(body, "PieChartControl — gráfico de dona (donut chart) dibujado sobre un Canvas. " +
             "Tiene una propiedad Slices que recibe una lista de PieSlice y redibuja " +
             "automáticamente cada vez que cambia.");
Bullet(body, "BarChartControl — gráfico de barras verticales para comparar gastos por mes.");

H3(body, "📁 Services/");
P(body, "Contiene servicios auxiliares que realizan tareas específicas, como la generación " +
        "del reporte PDF usando QuestPDF.");

H3(body, "📁 Windows/");
P(body, "Contiene ventanas secundarias (Window, no UserControl), como la ventana de login " +
        "o cambio de contraseña, que se abren de forma modal.");

H3(body, "Archivos raíz importantes");
Bullet(body, "App.xaml — define los estilos y recursos globales: colores, botones, " +
             "DataGrid, ComboBox y toda la paleta del tema oscuro.");
Bullet(body, "MainWindow.xaml — la ventana principal con la barra lateral de navegación " +
             "y el área de contenido central.");
P(body, "");

// ── 4. Base de datos ──────────────────────────────────────────────────────────
H2(body, "4. Cómo funciona la base de datos");

H3(body, "Ubicación del archivo");
P(body, "La base de datos se guarda en:");
P(body, "    %APPDATA%\\FinanzasPersonales\\finanzas.db", mono: true);
P(body, "Esto significa que cada usuario de Windows tiene su propia base de datos, " +
        "separada de los archivos del programa.");

H3(body, "Tablas principales");
Bullet(body, "Transacciones — Id, Tipo (\"Ingreso\"/\"Gasto\"), Monto, Categoria, " +
             "Descripcion, Notas, Fecha, CuentaNombre, TarjetaCreditoId.");
Bullet(body, "GastosFijos — Id, Nombre, Monto, DiaVencimiento, DiaVencimiento2, Activo.");
Bullet(body, "IngresosFijos — Id, Nombre, Monto, DiaIngreso, DiaIngreso2, Activo.");
Bullet(body, "PagosMensuales — registra si un gasto/ingreso fijo fue pagado en un mes " +
             "específico (FijoId, TipoFijo, Anio, Mes, Dia, Pagado, MetodoPago).");
Bullet(body, "TarjetasCredito — Id, Nombre, LimiteCredito, DiaCierre, DiaPago, SaldoUsado.");
Bullet(body, "MetasAhorro — Id, Nombre, MontoObjetivo, MontoActual, FechaLimite.");
Bullet(body, "Categorias — Id, Nombre, Tipo (\"Ingreso\"/\"Gasto\"), Color.");
Bullet(body, "Usuarios — Id, Username, PasswordHash (contraseña hasheada con SHA-256).");

H3(body, "Patrón de acceso");
P(body, "La clase AppDatabase centraliza todo el acceso a datos. Cada método abre una " +
        "conexión nueva, ejecuta la operación y la cierra. Por ejemplo:");
Bullet(body, "InsertarTransaccion(Transaccion t) — inserta una fila en Transacciones.");
Bullet(body, "ObtenerGastosFijosConEstado(anio, mes) — hace un JOIN entre GastosFijos " +
             "y PagosMensuales para saber qué gastos ya fueron pagados este mes.");
Bullet(body, "ObtenerTransaccionesPorTarjeta(tarjetaId) — devuelve los movimientos " +
             "del período actual de una tarjeta.");
P(body, "");

// ── 5. Navegación ─────────────────────────────────────────────────────────────
H2(body, "5. Navegación de la aplicación");
P(body, "La navegación está manejada enteramente en MainWindow.xaml.cs. Funciona así:");
Bullet(body, "La ventana principal tiene una barra lateral con botones de navegación " +
             "(NavButton) y un ContentControl en el área central.");
Bullet(body, "Cuando el usuario hace clic en un botón de la barra lateral, se llama " +
             "al método correspondiente (MostrarDashboard, MostrarTransacciones, etc.).");
Bullet(body, "Las vistas se crean la primera vez que se usan (lazy initialization) y " +
             "se guardan en caché. Cambiar de vista simplemente reasigna el " +
             "Content del ContentControl.");
Bullet(body, "Al mostrarse una vista, se llama su método Actualizar() para recargar " +
             "los datos desde SQLite.");
Bullet(body, "El botón activo se marca con IsEnabled=false, lo que activa un estilo " +
             "visual diferente (fondo destacado) definido en App.xaml.");
P(body, "");

// ── 6. Módulos ────────────────────────────────────────────────────────────────
H2(body, "6. Módulos de la aplicación");

H3(body, "6.1 Dashboard");
P(body, "Vista de inicio que muestra un resumen del estado financiero actual:");
Bullet(body, "Saldo total (ingresos − gastos del mes).");
Bullet(body, "Total de ingresos y gastos del mes en curso.");
Bullet(body, "Gráfico de dona con distribución de gastos por categoría.");
Bullet(body, "Gráfico de barras con comparación de ingresos y gastos de los últimos meses.");
Bullet(body, "Lista de las transacciones más recientes.");

H3(body, "6.2 Transacciones");
P(body, "Permite ver, filtrar y agregar movimientos de dinero:");
Bullet(body, "Formulario para registrar ingresos y gastos con categoría, descripción, " +
             "monto y cuenta o tarjeta de crédito asociada.");
Bullet(body, "Tabla con todas las transacciones, con el monto en rojo (gasto) o verde (ingreso).");
Bullet(body, "Filtros por tipo, categoría y rango de fechas.");

H3(body, "6.3 Gastos Fijos");
P(body, "Administra los gastos que se repiten cada mes (servicios, suscripciones, etc.):");
Bullet(body, "Se pueden definir con uno o dos días de vencimiento al mes.");
Bullet(body, "Los gastos se muestran agrupados por día de vencimiento (Día 5, Día 15, etc.).");
Bullet(body, "Cada gasto muestra si ya fue pagado este mes, cuántos días faltan para vencer, " +
             "y cambia de color si está vencido o por vencer.");
Bullet(body, "Se puede registrar el método de pago (efectivo, banco, tarjeta débito, etc.).");

H3(body, "6.4 Ingresos Fijos");
P(body, "Similar a Gastos Fijos pero para ingresos recurrentes:");
Bullet(body, "Se definen con uno o dos días de cobro al mes.");
Bullet(body, "Se muestran agrupados por día de cobro.");
Bullet(body, "Se pueden marcar como recibidos.");
Bullet(body, "Muestra el total pendiente y el total ya recibido del mes.");

H3(body, "6.5 Tarjetas de Crédito");
P(body, "Administra tarjetas de crédito con su ciclo de facturación:");
Bullet(body, "Cada tarjeta muestra su límite, saldo usado, saldo disponible y días hasta el cierre.");
Bullet(body, "Se puede registrar un pago a la tarjeta (abono) que reduce el saldo usado.");
Bullet(body, "Al seleccionar una tarjeta se ven sus movimientos del período actual.");
Bullet(body, "Hacer clic en una tarjeta ya seleccionada la deselecciona (oculta los detalles).");

H3(body, "6.6 Metas de Ahorro");
P(body, "Permite definir objetivos de ahorro (vacaciones, emergencias, etc.):");
Bullet(body, "Cada meta tiene un nombre, un monto objetivo y una fecha límite opcional.");
Bullet(body, "Se muestra una barra de progreso visual.");
Bullet(body, "Cada tarjeta de meta tiene su propio campo para agregar dinero incrementalmente.");

H3(body, "6.7 Reportes");
P(body, "Genera reportes visuales y exportables:");
Bullet(body, "Gráfico de dona por categoría de gastos en el mes seleccionado.");
Bullet(body, "Tabla con el desglose de gastos por categoría.");
Bullet(body, "Botón para exportar el reporte a un archivo PDF (usando QuestPDF).");

H3(body, "6.8 Categorías");
P(body, "Permite personalizar las categorías de ingresos y gastos:");
Bullet(body, "Se pueden crear categorías propias con nombre, tipo y color.");
Bullet(body, "Las categorías personalizadas aparecen en el formulario de Transacciones.");
Bullet(body, "Se pueden eliminar categorías que ya no se usen.");
P(body, "");

// ── 7. Tema visual ────────────────────────────────────────────────────────────
H2(body, "7. Tema visual (paleta de colores)");
P(body, "Toda la aplicación usa un tema oscuro definido en App.xaml. Los colores principales son:");
Bullet(body, "BgBrush (#1E1E2E) — fondo de la ventana principal.");
Bullet(body, "SurfaceBrush (#24273A) — barra lateral y campos de texto.");
Bullet(body, "CardBrush (#2A2D3E) — tarjetas de contenido.");
Bullet(body, "AccentBrush (#7C3AED) — botones primarios y elementos destacados (violeta).");
Bullet(body, "GreenBrush (#A6E3A1) — ingresos, montos positivos, estados \"pagado\".");
Bullet(body, "RedBrush (#F38BA8) — gastos, montos negativos, alertas de error.");
Bullet(body, "YellowBrush — alertas de vencimiento próximo.");
P(body, "Los estilos de botones (PrimaryButton, DangerButton), inputs (TextBox, ComboBox) " +
        "y DataGrid están todos definidos como recursos globales en App.xaml, lo que " +
        "asegura consistencia visual en toda la aplicación.");
P(body, "");

// ── 8. Cómo compilar ──────────────────────────────────────────────────────────
H2(body, "8. Cómo compilar y ejecutar");

H3(body, "Requisitos");
Bullet(body, "Windows 10 o superior (para desarrollo y ejecución).");
Bullet(body, ".NET 10 SDK instalado (dotnet.microsoft.com/download).");

H3(body, "Ejecutar en modo desarrollo");
P(body, "Desde la carpeta raíz del proyecto:");
P(body, "    dotnet run", mono: true);

H3(body, "Generar el instalador");
P(body, "    1. Ejecuta build-installer.bat", mono: true);
P(body, "    2. Instala Inno Setup 6 si se te solicita (https://jrsoftware.org/isdl.php)", mono: true);
P(body, "    3. El instalador se genera en: Installer\\Output\\FinanzasPersonales-Setup.exe", mono: true);
P(body, "");
P(body, "El instalador generado es un .exe autónomo: incluye el runtime de .NET y no " +
        "requiere que la computadora destino tenga .NET instalado.");
P(body, "");
Separator(body);
P(body, "");
P(body, "Documento generado automáticamente · FinanzasPersonales v1.0 · " + DateTime.Today.ToString("dd/MM/yyyy"),
        italic: true);

// ── Guardar ───────────────────────────────────────────────────────────────────
mainPart.Document.Save();
doc.Dispose();

Console.WriteLine("Documento generado correctamente.");

// ═══════════════════════════════════════════════════════════════════════════════
//  HELPERS
// ═══════════════════════════════════════════════════════════════════════════════

static void AddStyles(MainDocumentPart mainPart)
{
    var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();

    var styles = new Styles();

    // ── Normal ─────────────────────────────────────────────────────────────────
    var normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
    normal.Append(new StyleName { Val = "Normal" });
    normal.Append(new StyleRunProperties(
        new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" },
        new FontSize { Val = "22" }           // 11 pt
    ));
    styles.Append(normal);

    // ── Heading 1 ──────────────────────────────────────────────────────────────
    var h1 = new Style { Type = StyleValues.Paragraph, StyleId = "Heading1" };
    h1.Append(new StyleName { Val = "heading 1" });
    h1.Append(new BasedOn { Val = "Normal" });
    h1.Append(new StyleParagraphProperties(
        new SpacingBetweenLines { Before = "240", After = "120" },
        new KeepNext()
    ));
    h1.Append(new StyleRunProperties(
        new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" },
        new Bold(),
        new FontSize { Val = "48" },          // 24 pt
        new Color { Val = "1F3864" }          // azul oscuro
    ));
    styles.Append(h1);

    // ── Heading 2 ──────────────────────────────────────────────────────────────
    var h2 = new Style { Type = StyleValues.Paragraph, StyleId = "Heading2" };
    h2.Append(new StyleName { Val = "heading 2" });
    h2.Append(new BasedOn { Val = "Normal" });
    h2.Append(new StyleParagraphProperties(
        new SpacingBetweenLines { Before = "360", After = "120" },
        new KeepNext()
    ));
    h2.Append(new StyleRunProperties(
        new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" },
        new Bold(),
        new FontSize { Val = "36" },          // 18 pt
        new Color { Val = "2E4699" }          // azul medio
    ));
    styles.Append(h2);

    // ── Heading 3 ──────────────────────────────────────────────────────────────
    var h3 = new Style { Type = StyleValues.Paragraph, StyleId = "Heading3" };
    h3.Append(new StyleName { Val = "heading 3" });
    h3.Append(new BasedOn { Val = "Normal" });
    h3.Append(new StyleParagraphProperties(
        new SpacingBetweenLines { Before = "240", After = "80" },
        new KeepNext()
    ));
    h3.Append(new StyleRunProperties(
        new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" },
        new Bold(),
        new FontSize { Val = "28" },          // 14 pt
        new Color { Val = "4472C4" }          // azul claro
    ));
    styles.Append(h3);

    // ── Bullet ─────────────────────────────────────────────────────────────────
    var bullet = new Style { Type = StyleValues.Paragraph, StyleId = "Bullet" };
    bullet.Append(new StyleName { Val = "Bullet" });
    bullet.Append(new BasedOn { Val = "Normal" });
    bullet.Append(new StyleParagraphProperties(
        new Indentation { Left = "720", Hanging = "360" },
        new SpacingBetweenLines { After = "40" }
    ));
    bullet.Append(new StyleRunProperties(
        new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" },
        new FontSize { Val = "22" }
    ));
    styles.Append(bullet);

    // ── Mono ───────────────────────────────────────────────────────────────────
    var mono = new Style { Type = StyleValues.Paragraph, StyleId = "Mono" };
    mono.Append(new StyleName { Val = "Mono" });
    mono.Append(new BasedOn { Val = "Normal" });
    mono.Append(new StyleParagraphProperties(
        new Indentation { Left = "720" },
        new SpacingBetweenLines { After = "40" }
    ));
    mono.Append(new StyleRunProperties(
        new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
        new FontSize { Val = "20" },
        new Color { Val = "C7254E" }
    ));
    styles.Append(mono);

    stylesPart.Styles = styles;
    stylesPart.Styles.Save();
}

static void AddNumbering(MainDocumentPart mainPart)
{
    // No usamos numeración automática de Word; usamos estilo propio con viñeta manual.
}

static void H1(Body body, string text)
{
    var para = new Paragraph();
    para.Append(new ParagraphProperties(
        new ParagraphStyleId { Val = "Heading1" }
    ));
    para.Append(new Run(new Text(text)));
    body.Append(para);
}

static void H2(Body body, string text)
{
    var para = new Paragraph();
    para.Append(new ParagraphProperties(
        new ParagraphStyleId { Val = "Heading2" }
    ));
    para.Append(new Run(new Text(text)));
    body.Append(para);
}

static void H3(Body body, string text)
{
    var para = new Paragraph();
    para.Append(new ParagraphProperties(
        new ParagraphStyleId { Val = "Heading3" }
    ));
    para.Append(new Run(new Text(text)));
    body.Append(para);
}

static void P(Body body, string text, bool italic = false, bool mono = false)
{
    var para = new Paragraph();

    if (mono)
    {
        para.Append(new ParagraphProperties(
            new ParagraphStyleId { Val = "Mono" }
        ));
    }
    else
    {
        para.Append(new ParagraphProperties(
            new SpacingBetweenLines { After = "100" }
        ));
    }

    if (string.IsNullOrEmpty(text))
    {
        body.Append(para);
        return;
    }

    var rPr = new RunProperties();
    if (italic) rPr.Append(new Italic());
    if (mono)
    {
        rPr.Append(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" });
        rPr.Append(new Color { Val = "C7254E" });
    }

    var run = new Run();
    if (rPr.HasChildren) run.Append(rPr);
    run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    para.Append(run);
    body.Append(para);
}

static void Bullet(Body body, string text)
{
    var para = new Paragraph();
    para.Append(new ParagraphProperties(
        new ParagraphStyleId { Val = "Bullet" },
        new SpacingBetweenLines { After = "40" }
    ));
    var run = new Run(new Text("•  " + text) { Space = SpaceProcessingModeValues.Preserve });
    para.Append(run);
    body.Append(para);
}

static void Separator(Body body)
{
    var para = new Paragraph();
    para.Append(new ParagraphProperties(
        new ParagraphBorders(
            new BottomBorder
            {
                Val   = BorderValues.Single,
                Color = "4472C4",
                Size  = 6,
                Space = 1
            }
        )
    ));
    body.Append(para);
}
