using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;

namespace ERP.Infrastructure.InitialLoad;

/// <summary>
/// Lectura/escritura del .xlsx de la plantilla de Proveedores vía ClosedXML — mismo patrón que
/// <see cref="ClosedXmlCustomerImportSheetReader"/> (INITIAL-LOAD-ARCH-01/SUPPLIERS-01).
/// </summary>
public sealed class ClosedXmlSupplierImportSheetReader : ISupplierImportSheetReader
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
                foreach (var column in SupplierImportColumns.All)
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
        var sheet = workbook.Worksheets.Add("Proveedores");

        for (var i = 0; i < SupplierImportColumns.All.Count; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = SupplierImportColumns.All[i];
            cell.Style.Font.Bold = true;
        }

        sheet.Cell(2, 1).Value = "04";
        sheet.Cell(2, 2).Value = "1790012345001";
        sheet.Cell(2, 3).Value = "Proveedor Ejemplo S.A.";
        sheet.Cell(2, 4).Value = "Proveedor Ejemplo";
        sheet.Cell(2, 5).Value = "EC";
        sheet.Cell(2, 6).Value = "contacto@proveedor-ejemplo.com";
        sheet.Cell(2, 7).Value = "0999999999";
        sheet.Cell(2, 8).Value = "CONTADO";
        sheet.Cell(2, 9).Value = "Manufacturer";
        sheet.Cell(2, 10).Value = "National";
        sheet.Cell(2, 11).Value = "Goods";
        sheet.Cell(2, 12).Value = "Strategic";

        sheet.Columns().AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instrucciones");
        instructions.Cell(1, 1).Value = "Cómo llenar esta plantilla";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        instructions.Cell(3, 1).Value =
            "Tipo Identificación / Número Identificación / Razón Social / Condición de Pago son obligatorios.";
        instructions.Cell(4, 1).Value =
            "Tipo Identificación: código SRI — 04 = RUC, 05 = Cédula, 06 = Pasaporte, 07 = Consumidor Final, 08 = Exterior.";
        instructions.Cell(5, 1).Value =
            "Condición de Pago: código exacto de una condición ya configurada en Configuración (ej. CONTADO, CREDITO30).";
        instructions.Cell(6, 1).Value =
            "Email y Teléfono son opcionales — si ambos faltan, la fila se importa igual con una advertencia.";
        instructions.Cell(7, 1).Value = "No modifique los encabezados de la fila 1.";
        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    private static bool IsInstructionsSheet(string name) =>
        string.Equals(name, "Instrucciones", StringComparison.OrdinalIgnoreCase);
}
