using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ERP.Application.Inventory.DTOs;

namespace ERP.API.Export;

/// <summary>
/// Genera un PDF del Kardex valorizado usando QuestPDF.
/// Licencia Community (libre para proyectos con ingresos anuales &lt; 1 M USD).
/// </summary>
public static class KardexPdfExporter
{
    private static readonly string ColPrimario  = "#1F497D";
    private static readonly string ColEntrada   = "#D6E8CE";
    private static readonly string ColSalida    = "#F4CCCC";
    private static readonly string ColSaldo     = "#D9E6F3";
    private static readonly string ColResumen   = "#FFFECB";
    private static readonly string ColAltRow    = "#F7F7F7";
    private static readonly string ColHeader    = "#344F78";
    private static readonly string ColWhite     = "#FFFFFF";
    private static readonly string ColBlack     = "#000000";
    private static readonly string ColGrayLight = "#E0E0E0";
    private static readonly string ColGrayMid   = "#9E9E9E";

    public static byte[] Generate(KardexResponse kardex, DateTime? fechaInicio, DateTime? fechaFin)
    {
        var periodo = fechaInicio.HasValue || fechaFin.HasValue
            ? $"{fechaInicio?.ToString("dd/MM/yyyy") ?? "inicio"} al {fechaFin?.ToString("dd/MM/yyyy") ?? "hoy"}"
            : "Todo el historial";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(8).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    // TÃ­tulo
                    col.Item()
                       .Background(ColPrimario).Padding(8)
                       .Text("KARDEX VALORIZADO â€” PROMEDIO PONDERADO MÃ“VIL")
                       .Bold().FontSize(11).FontColor(ColWhite);

                    // Ficha informativa
                    col.Item().Padding(4).Row(row =>
                    {
                        row.RelativeItem().Column(info =>
                        {
                            info.Item().Row(r => { r.ConstantItem(55).Text("Producto:").Bold().FontColor(ColPrimario); r.RelativeItem().Text($"{kardex.Producto.Name}  [{kardex.Producto.Codigo}]"); });
                            info.Item().Row(r => { r.ConstantItem(55).Text("Bodega:").Bold().FontColor(ColPrimario);   r.RelativeItem().Text(kardex.Warehouse.Name); });
                        });
                        row.RelativeItem().Column(info =>
                        {
                            info.Item().Row(r => { r.ConstantItem(55).Text("PerÃ­odo:").Bold().FontColor(ColPrimario);  r.RelativeItem().Text(periodo); });
                            info.Item().Row(r => { r.ConstantItem(55).Text("Emitido:").Bold().FontColor(ColPrimario);  r.RelativeItem().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")); });
                        });
                    });

                    col.Item().BorderBottom(1).BorderColor(ColPrimario).PaddingBottom(4);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(6);

                    // Saldo inicial
                    if (kardex.Resumen.OpeningQuantity != 0)
                    {
                        var cpa = kardex.Resumen.OpeningQuantity > 0
                            ? kardex.Resumen.OpeningValue / kardex.Resumen.OpeningQuantity
                            : 0m;
                        col.Item()
                           .Background("#EBEBEB").Border(1).BorderColor("#AAAAAA").Padding(5)
                           .Row(row =>
                           {
                               row.RelativeItem().Text("SALDO INICIAL (antes del perÃ­odo)").Bold();
                               row.ConstantItem(105).AlignRight().Text($"Cant: {kardex.Resumen.OpeningQuantity:N4}");
                               row.ConstantItem(110).AlignRight().Text($"Valor: {kardex.Resumen.OpeningValue:N4}");
                               row.ConstantItem(110).AlignRight().Text($"Promedio: {cpa:N4}");
                           });
                    }

                    // Tabla de movimientos
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(60);  // Fecha
                            cols.RelativeColumn(2);   // Tipo
                            cols.RelativeColumn(2);   // Referencia
                            cols.ConstantColumn(52);  // E.Cant
                            cols.ConstantColumn(56);  // E.Valor
                            cols.ConstantColumn(52);  // S.Cant
                            cols.ConstantColumn(56);  // S.Valor
                            cols.ConstantColumn(56);  // Saldo Cant
                            cols.ConstantColumn(60);  // Saldo Valor
                            cols.ConstantColumn(60);  // Costo Prom
                        });

                        table.Header(h =>
                        {
                            // Fila 1: grupos
                            h.Cell().ColumnSpan(3).Background(ColHeader).Border(0.5f).BorderColor(ColWhite).Padding(3).AlignCenter().Text("").FontColor(ColWhite).Bold();
                            h.Cell().ColumnSpan(2).Background(ColEntrada).Border(0.5f).BorderColor(ColWhite).Padding(3).AlignCenter().Text("ENTRADA").Bold().FontSize(8);
                            h.Cell().ColumnSpan(2).Background(ColSalida).Border(0.5f).BorderColor(ColWhite).Padding(3).AlignCenter().Text("SALIDA").Bold().FontSize(8);
                            h.Cell().ColumnSpan(3).Background(ColSaldo).Border(0.5f).BorderColor(ColWhite).Padding(3).AlignCenter().Text("SALDO / COSTO").Bold().FontSize(8);

                            // Fila 2: columnas
                            foreach (var (label, bg) in new[]
                            {
                                ("Fecha",       ColHeader),
                                ("Tipo",        ColHeader),
                                ("Referencia",  ColHeader),
                                ("E. Cant.",    ColEntrada),
                                ("E. Valor",    ColEntrada),
                                ("S. Cant.",    ColSalida),
                                ("S. Valor",    ColSalida),
                                ("Saldo Cant.", ColSaldo),
                                ("Saldo Valor", ColSaldo),
                                ("Costo Prom.", ColSaldo),
                            })
                            {
                                var isHeaderCol = bg == ColHeader;
                                h.Cell()
                                 .Background(bg).Border(0.5f).BorderColor(ColWhite)
                                 .Padding(3).AlignCenter()
                                 .Text(label).Bold().FontSize(7)
                                 .FontColor(isHeaderCol ? ColWhite : ColBlack);
                            }
                        });

                        // Filas de datos
                        for (var i = 0; i < kardex.Rows.Count; i++)
                        {
                            var m   = kardex.Rows[i];
                            var alt = i % 2 == 1 ? ColAltRow : ColWhite;
                            var entBg = m.InboundQuantity > 0 ? ColEntrada : alt;
                            var salBg = m.OutboundQuantity  > 0 ? ColSalida  : alt;

                            Cell(table, m.Fecha.ToLocalTime().ToString("dd/MM/yy HH:mm"), alt,   left: true);
                            Cell(table, m.MovementType,               alt,    left: true);
                            Cell(table, m.Referencia ?? "",             alt,    left: true);
                            Cell(table, Fmt(m.InboundQuantity), entBg, zero: m.InboundQuantity == 0);
                            Cell(table, Fmt(m.InboundValue),   entBg, zero: m.InboundValue    == 0);
                            Cell(table, Fmt(m.OutboundQuantity), salBg, zero: m.OutboundQuantity  == 0);
                            Cell(table, Fmt(m.OutboundValue),   salBg, zero: m.OutboundValue     == 0);
                            Cell(table, Fmt(m.BalanceQuantity),  ColSaldo);
                            Cell(table, Fmt(m.BalanceValue),     ColSaldo);
                            Cell(table, Fmt(m.AverageUnitCost), ColSaldo);
                        }
                    });

                    // Resumen
                    col.Item()
                       .Background(ColResumen).Border(1).BorderColor("#CCCC88").Padding(6)
                       .Column(resCol =>
                       {
                           resCol.Item().Text("RESUMEN DEL PERÃODO").Bold().FontSize(9).FontColor(ColPrimario);
                           resCol.Item().PaddingTop(4).Row(row =>
                           {
                               ResumenBox(row, "Inv. Inicial",  kardex.Resumen.OpeningQuantity, kardex.Resumen.OpeningValue, ColPrimario);
                               ResumenBox(row, "Inflows",       kardex.Resumen.TotalInboundQuantity,          kardex.Resumen.TotalInboundValue,          ColPrimario);
                               ResumenBox(row, "Outflows",        kardex.Resumen.TotalOutboundQuantity,           kardex.Resumen.TotalOutboundValue,           ColPrimario);
                               ResumenBox(row, "Inv. Final",     kardex.Resumen.ClosingQuantity,   kardex.Resumen.ClosingValue,   ColPrimario);
                               row.RelativeItem()
                                  .Border(1).BorderColor("#DDDDDD").Background(ColWhite).Padding(5)
                                  .Column(c2 =>
                                  {
                                      c2.Item().Text("Costo Prom. Final").Bold().FontSize(7).FontColor(ColPrimario);
                                      c2.Item().Text($"$ {kardex.Resumen.FinalAverageCost:N4}").Bold().FontSize(10).FontColor(ColPrimario);
                                  });
                           });
                       });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generado el ").FontSize(7).FontColor(ColGrayMid);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7).FontColor(ColGrayMid);
                    t.Span("  |  PÃ¡g. ").FontSize(7).FontColor(ColGrayMid);
                    t.CurrentPageNumber().FontSize(7).FontColor(ColGrayMid);
                    t.Span(" de ").FontSize(7).FontColor(ColGrayMid);
                    t.TotalPages().FontSize(7).FontColor(ColGrayMid);
                });
            });
        }).GeneratePdf();
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static void Cell(
        TableDescriptor t, string text, string bg,
        bool left = false, bool zero = false)
    {
        var cell = t.Cell()
                    .Background(bg)
                    .Border(0.3f).BorderColor(ColGrayLight)
                    .PaddingHorizontal(3).PaddingVertical(2);

        if (left)
            cell.AlignLeft().Text(text).FontSize(7)
                .FontColor(zero ? ColGrayMid : ColBlack);
        else
            cell.AlignRight().Text(text).FontSize(7)
                .FontColor(zero ? ColGrayMid : ColBlack);
    }

    private static void ResumenBox(
        RowDescriptor row, string label, decimal cant, decimal valor, string color)
        => row.RelativeItem()
              .Border(1).BorderColor("#DDDDDD").Background(ColWhite).Padding(5)
              .Column(c =>
              {
                  c.Item().Text(label).Bold().FontSize(7).FontColor(color);
                  c.Item().Text($"{cant:N4} uds").FontSize(7);
                  c.Item().Text($"$ {valor:N4}").Bold().FontSize(8);
              });

    private static string Fmt(decimal v) => v == 0 ? "â€”" : v.ToString("N4");
}


