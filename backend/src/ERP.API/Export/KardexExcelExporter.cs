using ClosedXML.Excel;
using ERP.Application.Inventory.DTOs;

namespace ERP.API.Export;

/// <summary>
/// Genera un libro Excel (.xlsx) con el Kardex valorizado.
/// DiseÃ±o: encabezado informativo â†’ inventario inicial â†’ tabla de movimientos â†’ resumen.
/// </summary>
public static class KardexExcelExporter
{
    // â”€â”€ Paleta â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static readonly XLColor ColHeaderBg  = XLColor.FromArgb(0x1F, 0x49, 0x7D); // azul corporativo
    private static readonly XLColor ColHeaderFg  = XLColor.White;
    private static readonly XLColor ColEntrada   = XLColor.FromArgb(0xE2, 0xEF, 0xDA); // verde suave
    private static readonly XLColor ColSalida    = XLColor.FromArgb(0xFF, 0xE6, 0xE6); // rojo suave
    private static readonly XLColor ColSaldo     = XLColor.FromArgb(0xDD, 0xEB, 0xF7); // azul suave
    private static readonly XLColor ColResumenBg = XLColor.FromArgb(0xFF, 0xFE, 0xCD); // amarillo suave
    private static readonly XLColor ColAltRow    = XLColor.FromArgb(0xF7, 0xF7, 0xF7); // gris muy claro

    public static byte[] Generate(KardexResponse kardex, DateTime? fechaInicio, DateTime? fechaFin)
    {
        using var wb  = new XLWorkbook();
        var ws = wb.Worksheets.Add("Kardex");

        var row = 1;

        // â”€â”€ BLOQUE DE ENCABEZADO â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        row = WriteHeader(ws, kardex, fechaInicio, fechaFin, row);

        // â”€â”€ SALDO INICIAL (solo si hay filtro de fecha) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (kardex.Resumen.OpeningQuantity != 0 || fechaInicio.HasValue)
        {
            row = WriteInicialBalance(ws, kardex.Resumen, row);
        }

        // â”€â”€ TABLA DE MOVIMIENTOS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        int tableHeaderRow = row;
        row = WriteTableHeader(ws, row);
        row = WriteMovimientos(ws, kardex.Rows, row, tableHeaderRow);

        // â”€â”€ FILA DE RESUMEN â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        row = WriteResumen(ws, kardex.Resumen, row);

        // â”€â”€ AJUSTE DE COLUMNAS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        ws.Columns().AdjustToContents(minWidth: 10, maxWidth: 30);
        ws.Column(1).Width = 14;  // Fecha
        ws.Column(2).Width = 22;  // Tipo movimiento
        ws.Column(3).Width = 22;  // Referencia

        // â”€â”€ CONGELAR ENCABEZADO â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        ws.SheetView.FreezeRows(tableHeaderRow);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Secciones privadas
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static int WriteHeader(
        IXLWorksheet ws, KardexResponse k,
        DateTime? fechaInicio, DateTime? fechaFin, int startRow)
    {
        var r = startRow;

        // TÃ­tulo
        var titleCell = ws.Cell(r, 1);
        titleCell.Value = "KARDEX VALORIZADO â€” PROMEDIO PONDERADO MÃ“VIL";
        titleCell.Style.Font.Bold      = true;
        titleCell.Style.Font.FontSize  = 14;
        titleCell.Style.Font.FontColor = XLColor.FromArgb(0x1F, 0x49, 0x7D);
        ws.Range(r, 1, r, 12).Merge();
        r++;

        // Empresa / producto / bodega
        SetInfoRow(ws, r++, "Producto", $"{k.Producto.Name}  [{k.Producto.Codigo}]");
        SetInfoRow(ws, r++, "Bodega",   k.Warehouse.Name);

        var periodoTexto = fechaInicio.HasValue || fechaFin.HasValue
            ? $"{fechaInicio?.ToString("dd/MM/yyyy") ?? "inicio"} al {fechaFin?.ToString("dd/MM/yyyy") ?? "hoy"}"
            : "Todo el historial";
        SetInfoRow(ws, r++, "PerÃ­odo",  periodoTexto);
        SetInfoRow(ws, r++, "Emitido",  DateTime.Now.ToString("dd/MM/yyyy HH:mm"));

        r++; // fila en blanco separadora
        return r;
    }

    private static void SetInfoRow(IXLWorksheet ws, int row, string label, string value)
    {
        ws.Cell(row, 1).Value              = label + ":";
        ws.Cell(row, 1).Style.Font.Bold    = true;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromArgb(0x44, 0x72, 0xC4);
        ws.Cell(row, 2).Value              = value;
        ws.Range(row, 2, row, 12).Merge();
    }

    private static int WriteInicialBalance(IXLWorksheet ws, KardexSummaryDto r, int startRow)
    {
        var row = startRow;
        var range = ws.Range(row, 1, row, 12);
        range.Style.Fill.BackgroundColor = XLColor.FromArgb(0xED, 0xED, 0xED);
        range.Style.Font.Bold            = true;

        ws.Cell(row, 1).Value  = "SALDO INICIAL (antes del perÃ­odo)";
        ws.Cell(row, 8).Value  = r.OpeningQuantity;
        ws.Cell(row, 9).Value  = r.OpeningValue;
        ws.Cell(row, 10).Value = r.OpeningQuantity > 0
            ? r.OpeningValue / r.OpeningQuantity
            : 0m;

        ApplyNumberFormat(ws, row, 4, 12);
        row++;
        return row;
    }

    private static int WriteTableHeader(IXLWorksheet ws, int startRow)
    {
        var headers = new[]
        {
            "Fecha",
            "Tipo Movimiento",
            "Referencia",
            "E. Cantidad",
            "E. Valor",
            "S. Cantidad",
            "S. Valor",
            "Saldo Cantidad",
            "Saldo Valor",
            "Costo Promedio",
        };

        // Grupos de encabezado (nivel superior)
        SetGroupHeader(ws, startRow,     4, 5, "ENTRADA",       ColEntrada);
        SetGroupHeader(ws, startRow,     6, 7, "SALIDA",        ColSalida);
        SetGroupHeader(ws, startRow,     8, 10, "SALDO / COSTO", ColSaldo);

        // Encabezados de columna (nivel inferior)
        var detailRow = startRow + 1;
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(detailRow, i + 1);
            cell.Value                          = headers[i];
            cell.Style.Fill.BackgroundColor     = ColHeaderBg;
            cell.Style.Font.FontColor           = ColHeaderFg;
            cell.Style.Font.Bold                = true;
            cell.Style.Alignment.Horizontal     = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.BottomBorder      = XLBorderStyleValues.Medium;
        }

        // Columnas de texto sin fondo de grupo â€” aplicar fondo de encabezado tambiÃ©n al nivel superior
        foreach (var col in new[] { 1, 2, 3 })
        {
            ws.Cell(startRow, col).Style.Fill.BackgroundColor = ColHeaderBg;
            ws.Range(startRow, col, startRow, col).Merge();
        }

        return detailRow + 1;
    }

    private static void SetGroupHeader(
        IXLWorksheet ws, int row, int colFrom, int colTo, string label, XLColor bg)
    {
        var range = ws.Range(row, colFrom, row, colTo);
        range.Merge();
        range.Value                          = label;
        range.Style.Fill.BackgroundColor     = bg;
        range.Style.Font.Bold                = true;
        range.Style.Alignment.Horizontal     = XLAlignmentHorizontalValues.Center;
        range.Style.Border.OutsideBorder     = XLBorderStyleValues.Medium;
    }

    private static int WriteMovimientos(
        IXLWorksheet ws,
        IReadOnlyList<KardexMovementDto> movimientos,
        int startRow,
        int tableHeaderRow)
    {
        var row = startRow;

        for (var i = 0; i < movimientos.Count; i++)
        {
            var m   = movimientos[i];
            var alt = i % 2 == 1;

            ws.Cell(row, 1).Value  = m.Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            ws.Cell(row, 2).Value  = m.MovementType;
            ws.Cell(row, 3).Value  = m.Referencia ?? "";
            ws.Cell(row, 4).Value  = m.InboundQuantity;
            ws.Cell(row, 5).Value  = m.InboundValue;
            ws.Cell(row, 6).Value  = m.OutboundQuantity;
            ws.Cell(row, 7).Value  = m.OutboundValue;
            ws.Cell(row, 8).Value  = m.BalanceQuantity;
            ws.Cell(row, 9).Value  = m.BalanceValue;
            ws.Cell(row, 10).Value = m.AverageUnitCost;

            // Fondo alternado
            if (alt)
                ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = ColAltRow;

            // Colorear columnas de entrada/salida/saldo suavemente
            ws.Range(row, 4, row, 5).Style.Fill.BackgroundColor = m.InboundQuantity > 0
                ? XLColor.FromArgb(0xD6, 0xE8, 0xCE) : (alt ? ColAltRow : XLColor.White);
            ws.Range(row, 6, row, 7).Style.Fill.BackgroundColor = m.OutboundQuantity > 0
                ? XLColor.FromArgb(0xF4, 0xCC, 0xCC) : (alt ? ColAltRow : XLColor.White);

            // Ceros en gris
            ZeroToGray(ws, row, 4); ZeroToGray(ws, row, 5);
            ZeroToGray(ws, row, 6); ZeroToGray(ws, row, 7);

            ApplyNumberFormat(ws, row, 4, 10);
            row++;
        }

        // Borde de tabla
        if (movimientos.Count > 0)
        {
            ws.Range(tableHeaderRow + 1, 1, row - 1, 10)
              .Style.Border.OutsideBorder     = XLBorderStyleValues.Medium;
            ws.Range(tableHeaderRow + 1, 1, row - 1, 10)
              .Style.Border.InsideBorder      = XLBorderStyleValues.Hair;
        }

        return row;
    }

    private static int WriteResumen(IXLWorksheet ws, KardexSummaryDto r, int startRow)
    {
        var row = startRow + 1; // lÃ­nea en blanco

        var range = ws.Range(row, 1, row, 10);
        range.Style.Fill.BackgroundColor = ColResumenBg;
        range.Style.Font.Bold            = true;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

        ws.Cell(row, 1).Value  = "RESUMEN DEL PERÃODO";
        ws.Cell(row, 4).Value  = r.TotalInboundQuantity;
        ws.Cell(row, 5).Value  = r.TotalInboundValue;
        ws.Cell(row, 6).Value  = r.TotalOutboundQuantity;
        ws.Cell(row, 7).Value  = r.TotalOutboundValue;
        ws.Cell(row, 8).Value  = r.ClosingQuantity;
        ws.Cell(row, 9).Value  = r.ClosingValue;
        ws.Cell(row, 10).Value = r.FinalAverageCost;

        ApplyNumberFormat(ws, row, 4, 10);
        return row + 1;
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static void ApplyNumberFormat(IXLWorksheet ws, int row, int colFrom, int colTo)
    {
        for (var c = colFrom; c <= colTo; c++)
            ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000000;-#,##0.000000;â€”";
    }

    private static void ZeroToGray(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        if (cell.Value.IsNumber && cell.GetDouble() == 0)
            cell.Style.Font.FontColor = XLColor.LightGray;
    }
}


