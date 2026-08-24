using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;

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
            throw new InvalidOperationException(
                $"El archivo no es un Excel (.xlsx) válido: {ex.Message}",
                ex
            );
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(w => !IsInstructionsSheet(w.Name));
            if (sheet is null)
                throw new InvalidOperationException("El archivo no contiene ninguna hoja de datos.");

            var headerRow = sheet.Row(1);
            var columnIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastUsedColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            for (var col = 1; col <= lastUsedColumn; col++)
            {
                var header = headerRow.Cell(col).GetString().Trim();
                if (!string.IsNullOrEmpty(header))
                    columnIndexes[header] = col;
            }

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
                    var value =
                        columnIndexes.TryGetValue(column, out var colIndex)
                            ? row.Cell(colIndex).GetString().Trim()
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

        sheet.Cell(2, 1).Value = "PROD-0001";
        sheet.Cell(2, 2).Value = "";
        sheet.Cell(2, 3).Value = "Bodega Principal";
        sheet.Cell(2, 4).Value = 100;
        sheet.Cell(2, 5).Value = 3.50;
        sheet.Cell(2, 6).Value = "";
        sheet.Cell(2, 7).Value = "Saldo inicial cargado desde Excel.";

        sheet.Columns().AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instrucciones");
        instructions.Cell(1, 1).Value = "Cómo llenar esta plantilla";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        instructions.Cell(3, 1).Value =
            "Una fila = existencia inicial de un producto en una bodega. El producto y la bodega deben existir previamente — esta plantilla nunca los crea.";
        instructions.Cell(4, 1).Value =
            "SKU o Código de barras: al menos uno de los dos debe identificar un ítem activo ya existente.";
        instructions.Cell(5, 1).Value = "Bodega: nombre exacto de una bodega activa ya existente.";
        instructions.Cell(6, 1).Value =
            "Cantidad y Costo unitario son obligatorios y deben ser mayores a cero — un ingreso de inventario siempre requiere costo.";
        instructions.Cell(7, 1).Value =
            "Fecha de corte es informativa — el movimiento de inventario se registra con la fecha de confirmación, no con esta fecha.";
        instructions.Cell(8, 1).Value =
            "No repita el mismo producto+bodega en más de una fila del mismo archivo.";
        instructions.Cell(9, 1).Value = "No modifique los encabezados de la fila 1.";
        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    private static bool IsInstructionsSheet(string name) =>
        string.Equals(name, "Instrucciones", StringComparison.OrdinalIgnoreCase);
}
