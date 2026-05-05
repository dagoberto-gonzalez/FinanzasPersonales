using System.IO;
using FinanzasPersonales.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinanzasPersonales.Services;

public static class PdfLaboralService
{
    public static string Exportar(
        string                    periodoLabel,
        decimal                   salarioPorHora,
        decimal                   jornadaSemanal,
        List<RegistroDiaLaboral>  registros,
        decimal                   salarioBruto,
        decimal                   deducciones,
        decimal                   salarioNeto)
    {
        var ruta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            $"ControlLaboral_{periodoLabel.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        QuestPDF.Settings.License = LicenseType.Community;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));
                page.Background().Background(Colors.White);

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("DMT Finance — Control Laboral")
                                .FontSize(18).Bold().FontColor(Colors.Purple.Darken2);
                            c.Item().Text($"Período: {periodoLabel}")
                                .FontSize(12).FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantItem(120).AlignRight().AlignMiddle()
                            .Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Purple.Lighten2);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(14);

                    // ── Resumen financiero ────────────────────────────────────
                    col.Item().Text("Resumen del Período").Bold().FontSize(13);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                        });

                        void Fila(string etiqueta, string valor, string? color = null)
                        {
                            table.Cell().Padding(5).Text(etiqueta).FontColor(Colors.Grey.Darken2);
                            var cell = table.Cell().Padding(5).AlignRight();
                            if (color is not null)
                                cell.Text(valor).Bold().FontColor(color);
                            else
                                cell.Text(valor).Bold();
                        }

                        Fila("Salario por hora",   $"₡{salarioPorHora:N0}");
                        Fila("Jornada semanal",    $"{jornadaSemanal} horas");
                        Fila("Salario bruto",       $"₡{salarioBruto:N0}", Colors.Black);
                        Fila("Deducción CCSS (10.67%)", $"₡{deducciones:N0}", Colors.Red.Darken1);
                        Fila("Salario neto",        $"₡{salarioNeto:N0}", Colors.Green.Darken1);
                    });

                    // ── Detalle de horas ──────────────────────────────────────
                    col.Item().Text("Detalle de Horas").Bold().FontSize(13);

                    double horasNorm   = registros.Where(r => !r.EsAusencia).Sum(r => r.HorasNormales);
                    double horasExtraD = registros.Sum(r => r.HorasExtraDiurnas);
                    double horasExtraN = registros.Sum(r => r.HorasExtraNocturnas);
                    double horasDobles = registros.Sum(r => r.HorasDobles);
                    int diasAusGoce    = registros.Count(r => r.EsAusencia && r.TieneGoceSalario);
                    int diasAusSin     = registros.Count(r => r.EsAusencia && !r.TieneGoceSalario);
                    decimal viaticos   = registros.Sum(r => r.Viaticos);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                        });

                        void Fila(string etiqueta, string valor)
                        {
                            table.Cell().Padding(4).Text(etiqueta).FontColor(Colors.Grey.Darken2);
                            table.Cell().Padding(4).AlignRight().Text(valor);
                        }

                        Fila("Horas normales",             $"{horasNorm:N1} h");
                        Fila("Horas extra diurnas (+50%)",  $"{horasExtraD:N1} h");
                        Fila("Horas extra nocturnas (+75%)", $"{horasExtraN:N1} h");
                        Fila("Horas dobles (+100%)",        $"{horasDobles:N1} h");
                        Fila("Días ausencia con goce",      $"{diasAusGoce}");
                        Fila("Días ausencia sin goce",      $"{diasAusSin}");
                        Fila("Viáticos",                   $"₡{viaticos:N0}");
                    });

                    // ── Registros diarios ─────────────────────────────────────
                    if (registros.Count > 0)
                    {
                        col.Item().Text("Registro Diario").Bold().FontSize(13);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(70);
                                c.ConstantColumn(80);
                                c.ConstantColumn(50);
                                c.ConstantColumn(50);
                                c.ConstantColumn(50);
                                c.ConstantColumn(50);
                                c.RelativeColumn();
                                c.ConstantColumn(60);
                            });

                            table.Header(h =>
                            {
                                void Hdr(string t)
                                    => h.Cell().Background(Colors.Purple.Lighten3)
                                        .Padding(4).Text(t).Bold().FontSize(8);
                                Hdr("Fecha");
                                Hdr("Tipo");
                                Hdr("H.Norm");
                                Hdr("H.ExtrD");
                                Hdr("H.ExtrN");
                                Hdr("H.Dob");
                                Hdr("Viáticos");
                                Hdr("Total H");
                            });

                            bool alt = false;
                            foreach (var r in registros)
                            {
                                var bg = alt ? Colors.Grey.Lighten4 : Colors.White;
                                alt = !alt;

                                void Cell(string t)
                                    => table.Cell().Background(bg).Padding(3).Text(t).FontSize(8);

                                Cell(r.FechaDisplay);
                                Cell(r.TipoDisplay);
                                Cell($"{r.HorasNormales:N1}");
                                Cell($"{r.HorasExtraDiurnas:N1}");
                                Cell($"{r.HorasExtraNocturnas:N1}");
                                Cell($"{r.HorasDobles:N1}");
                                Cell($"₡{r.Viaticos:N0}");
                                Cell($"{r.TotalHoras:N1}");
                            }
                        });
                    }

                    // ── Nota legal ────────────────────────────────────────────
                    col.Item().PaddingTop(8)
                        .Text("* Cálculos basados en Ley de Trabajo de Costa Rica. " +
                              "CCSS 10.67% (cuota obrero). Aguinaldo y vacaciones son estimaciones proporcionales.")
                        .FontSize(7).Italic().FontColor(Colors.Grey.Medium);
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium));
                    x.Span("Página "); x.CurrentPageNumber();
                    x.Span(" de ");    x.TotalPages();
                });
            });
        });

        doc.GeneratePdf(ruta);
        return ruta;
    }
}
