using System.IO;
using FinanzasPersonales.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinanzasPersonales.Services;

public static class PdfExportService
{
    public static string Exportar(
        List<Transaccion>   transacciones,
        List<GastoFijo>     gastosFijos,
        List<TarjetaCredito> tarjetas,
        int anio)
    {
        var ruta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            $"Reporte_Finanzas_{anio}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));
                page.Background().Background(Colors.White);

                page.Header().Element(ComposeHeader);

                page.Content().Column(col =>
                {
                    col.Spacing(16);

                    // ── Resumen anual ──────────────────────────────────────────
                    col.Item().Text($"Reporte Financiero — {anio}")
                        .FontSize(18).Bold().FontColor(Colors.Grey.Darken3);

                    col.Item().Text($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);

                    // Mismo eje que la pantalla de Reportes (presupuesto). Antes el PDF no
                    // filtraba nada, así que el mismo año exportado daba otras cifras.
                    var transAnio = transacciones
                        .Where(t => t.Fecha.Year == anio && t.AfectaPresupuesto).ToList();
                    var ingresos  = transAnio.Where(t => t.Tipo == "Ingreso").Sum(t => t.Monto);
                    var gastos    = transAnio.Where(t => t.Tipo == "Gasto").Sum(t => t.Monto);
                    var balance   = ingresos - gastos;

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(12).Column(resumen =>
                    {
                        resumen.Item().Text("Resumen del Año").Bold().FontSize(13);
                        resumen.Spacing(6);
                        resumen.Item().Row(r =>
                        {
                            r.RelativeItem().Text($"Ingresos: ₡{ingresos:N0}").FontColor(Colors.Green.Darken1);
                            r.RelativeItem().Text($"Gastos: ₡{gastos:N0}").FontColor(Colors.Red.Darken1);
                            r.RelativeItem().Text($"Balance: ₡{balance:N0}")
                                .FontColor(balance >= 0 ? Colors.Green.Darken1 : Colors.Red.Darken1);
                        });
                    });

                    // ── Tarjetas de crédito ────────────────────────────────────
                    if (tarjetas.Count > 0)
                    {
                        col.Item().Text("Tarjetas de Crédito").Bold().FontSize(13);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Tarjeta").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Límite").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Usado").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Disponible").Bold();
                            });

                            foreach (var t in tarjetas)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(t.Nombre);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"₡{t.LimiteCredito:N0}");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                    .Text($"₡{t.SaldoUsado:N0}").FontColor(Colors.Red.Darken1);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                    .Text($"₡{t.SaldoDisponible:N0}").FontColor(Colors.Green.Darken1);
                            }
                        });
                    }

                    // ── Gastos fijos del mes ───────────────────────────────────
                    if (gastosFijos.Count > 0)
                    {
                        col.Item().Text("Gastos Fijos del Mes").Bold().FontSize(13);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(4);
                                c.RelativeColumn(2);
                                c.RelativeColumn(1);
                                c.RelativeColumn(2);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Nombre").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Monto").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Día").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Estado").Bold();
                            });

                            foreach (var g in gastosFijos)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(g.Nombre);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"₡{g.Monto:N0}");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(g.DiaVencimiento.ToString());
                                var estado = g.PagadoEsteMes ? "Pagado" : g.EsVencido ? "Vencido" : "Pendiente";
                                var color  = g.PagadoEsteMes ? Colors.Green.Darken1 : g.EsVencido ? Colors.Red.Darken1 : Colors.Orange.Darken1;
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(estado).FontColor(color);
                            }
                        });
                    }

                    // ── Transacciones del año ─────────────────────────────────
                    col.Item().Text($"Transacciones {anio}").Bold().FontSize(13);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(70);
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.ConstantColumn(55);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Fecha").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Descripción").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Categoría").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Monto").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Tipo").Bold();
                        });

                        bool alterno = false;
                        foreach (var t in transAnio.OrderBy(x => x.Fecha))
                        {
                            var bg = alterno ? Colors.Grey.Lighten4 : Colors.White;
                            alterno = !alterno;

                            table.Cell().Background(bg).Padding(4).Text(t.Fecha.ToString("dd/MM/yy"));
                            table.Cell().Background(bg).Padding(4).Text(t.EtiquetaDisplay);
                            table.Cell().Background(bg).Padding(4).Text(t.Categoria);
                            table.Cell().Background(bg).Padding(4)
                                .Text($"₡{t.Monto:N0}")
                                .FontColor(t.Tipo == "Ingreso" ? Colors.Green.Darken1 : Colors.Red.Darken1);
                            table.Cell().Background(bg).Padding(4).Text(t.Tipo);
                        }
                    });
                });

                page.Footer().AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Página ");
                        x.CurrentPageNumber();
                        x.Span(" de ");
                        x.TotalPages();
                    });
            });
        });

        doc.GeneratePdf(ruta);
        return ruta;
    }

    private static void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("DMT Finance").FontSize(22).Bold().FontColor(Colors.Purple.Darken2);
                col.Item().Text("Reporte Financiero").FontSize(11).FontColor(Colors.Grey.Medium);
            });
            row.ConstantItem(100).AlignRight().AlignMiddle()
                .Text(DateTime.Today.ToString("dd/MM/yyyy"))
                .FontColor(Colors.Grey.Medium);
        });
    }
}
