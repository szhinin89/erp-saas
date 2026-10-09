using System.Globalization;
using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.Exceptions;

namespace ERP.Infrastructure.InitialLoad;

/// <summary>
/// Lectura/escritura del .xlsx de la plantilla de Stock Inicial vía ClosedXML — mismo patrón que
/// los demás readers de INITIAL-LOAD-ARCH-01. Archivo separado del Catálogo de Productos a
/// propósito (INITIAL-LOAD-INITIAL-STOCK-01).
/// </summary>
public sealed class ClosedXmlInitialStockImportSheetReader : IInitialStockImportSheetReader
{
    public Task<ImportReadResult> ReadAsync(Stream fileContent, CancellationToken ct)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(fileContent);
        }
        catch (Exception ex)
        {
            // ZH-DOMAIN-RULE-ERROR-SSOT-01: archivo inválido es una regla pública; el detalle
            // técnico de ClosedXML queda en la InnerException (log), nunca en el mensaje.
            throw new DomainRuleViolationException("El archivo no es un Excel (.xlsx) válido.", ex);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(w => !IsInstructionsSheet(w.Name));
            if (sheet is null)
                throw new DomainRuleViolationException(
                    "El archivo no contiene ninguna hoja de datos."
                );

            var headerRow = sheet.Row(1);
            var columnIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastUsedColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            for (var col = 1; col <= lastUsedColumn; col++)
            {
                var header = headerRow.Cell(col).GetString().Trim();
                if (!string.IsNullOrEmpty(header) && !columnIndexes.TryAdd(header, col))
                    throw new DomainRuleViolationException("Encabezado duplicado: " + header);
            }

            var obsolete = InitialStockImportColumns.Obsolete.Where(columnIndexes.ContainsKey).ToList();
            if (obsolete.Count > 0)
                throw new DomainRuleViolationException(
                    "La plantilla ya no admite: " + string.Join(", ", obsolete)
                        + ". La bodega se identifica por Código Bodega. Descargue la plantilla actual."
                );
            var missing = InitialStockImportColumns.All.Where(c => !columnIndexes.ContainsKey(c)).ToList();
            if (missing.Count > 0)
                throw new DomainRuleViolationException(
                    "Faltan encabezados de la plantilla: " + string.Join(", ", missing)
                );

            var rows = new List<IReadOnlyDictionary<string, string?>>();
            var lastUsedRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= lastUsedRow; r++)
            {
                var row = sheet.Row(r);
                if (row.IsEmpty())
                    continue;

                var values = new Dictionary<string, string?>();
                foreach (var column in InitialStockImportColumns.All)
                {
                    var value = columnIndexes.TryGetValue(column, out var colIndex)
                        ? InvariantText(row.Cell(colIndex))
                        : null;
                    values[column] = string.IsNullOrEmpty(value) ? null : value;
                }
                rows.Add(values);
            }

            return Task.FromResult(new ImportReadResult(rows));
        }
    }

    public Task<byte[]> BuildTemplateAsync(CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Stock Inicial");

        for (var i = 0; i < InitialStockImportColumns.All.Count; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = InitialStockImportColumns.All[i];
            cell.Style.Font.Bold = true;
        }

        var example = new Dictionary<string, string>
        {
            [InitialStockImportColumns.Sku] = "PROD-0001",
            [InitialStockImportColumns.Barcode] = "",
            [InitialStockImportColumns.WarehouseCode] = "BOD-01",
            [InitialStockImportColumns.Quantity] = "100",
            [InitialStockImportColumns.UnitCost] = "3.50",
            [InitialStockImportColumns.CutoffDate] = "2026-09-30",
            [InitialStockImportColumns.Observation] = "Saldo inicial al corte.",
        };
        for (var i = 0; i < InitialStockImportColumns.All.Count; i++)
        {
            // Texto en todas las columnas: Excel no debe convertir códigos, cantidades, costos ni
            // fechas según la configuración regional del equipo.
            sheet.Column(i + 1).Style.NumberFormat.Format = "@";
            sheet.Cell(2, i + 1).Value = example[InitialStockImportColumns.All[i]];
        }

        sheet.Columns().AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instrucciones");
        instructions.Cell(1, 1).Value = "Cómo llenar esta plantilla";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        string[] lines =
        [
            "Una fila = saldo inicial de un producto en una bodega al corte. El producto y la bodega deben existir; "
                + "esta plantilla nunca los crea ni registra compras.",
            "Obligatorios: SKU o Código de barras, Código Bodega, Cantidad, Costo unitario y Fecha de corte.",
            "SKU / Código de barras: si informa ambos deben corresponder al mismo producto activo. Productos de "
                + "servicio o con control de lotes/series no se admiten.",
            "Código Bodega: código de una bodega activa de la sucursal activa. Para otra sucursal, cambie de "
                + "sucursal y cargue otro archivo.",
            "Cantidad y Costo unitario: mayores a cero, con punto decimal (p. ej. 3.50), sin separador de miles. "
                + "Si exceden los decimales configurados para la empresa la fila se bloquea (no se redondea). "
                + "La cantidad debe ser entera si el producto no admite decimales.",
            "Fecha de corte: AAAA-MM-DD, no futura y la misma para todo el archivo. Es la fecha efectiva del saldo inicial.",
            "Solo para producto+bodega sin stock ni movimientos previos; si ya tiene historia, use un ajuste de inventario.",
            "No repita el mismo producto+bodega. Si cualquier fila tiene error, el lote no se confirma.",
            "No modifique los encabezados de la fila 1.",
        ];
        for (var i = 0; i < lines.Length; i++)
            instructions.Cell(i + 3, 1).Value = lines[i];
        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    /// <summary>
    /// IL-4A: el texto de una celda nunca depende de la cultura del servidor — un número de Excel se
    /// convierte con punto decimal invariante y una fecha a AAAA-MM-DD; el texto se lee tal cual.
    /// </summary>
    private static string InvariantText(IXLCell cell) => cell.DataType switch
    {
        XLDataType.Number => ((decimal)cell.GetDouble()).ToString(CultureInfo.InvariantCulture),
        XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => cell.GetString().Trim(),
    };

    private static bool IsInstructionsSheet(string name) =>
        string.Equals(name, "Instrucciones", StringComparison.OrdinalIgnoreCase);
}
