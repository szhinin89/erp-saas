using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;

namespace ERP.Infrastructure.InitialLoad;

/// <summary>
/// Lectura/escritura del .xlsx de la plantilla de Clientes vía ClosedXML — único punto del
/// backend que conoce ClosedXML para este import type (regla: mapeo de formato externo →
/// dominio vive en Infrastructure). Lee por nombre de columna (fila 1), no por índice, para
/// tolerar reordenamiento de columnas en el archivo subido.
/// </summary>
public sealed class ClosedXmlCustomerImportSheetReader : ICustomerImportSheetReader
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
                foreach (var column in CustomerImportColumns.All)
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
        var sheet = workbook.Worksheets.Add("Clientes");

        for (var i = 0; i < CustomerImportColumns.All.Count; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = CustomerImportColumns.All[i];
            cell.Style.Font.Bold = true;
        }

        sheet.Cell(2, 1).Value = "04";
        sheet.Cell(2, 2).Value = "1790012345001";
        sheet.Cell(2, 3).Value = "Comercial Ejemplo S.A.";
        sheet.Cell(2, 4).Value = "Comercial Ejemplo";
        sheet.Cell(2, 5).Value = "EC";
        sheet.Cell(2, 6).Value = "contacto@ejemplo.com";
        sheet.Cell(2, 7).Value = "0999999999";
        sheet.Cell(2, 8).Value = "Retail";
        sheet.Cell(2, 9).Value = "SMB";
        sheet.Cell(2, 10).Value = "Norte";
        sheet.Cell(2, 11).Value = 1000;
        sheet.Cell(2, 12).Value = 30;

        sheet.Columns().AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instrucciones");
        instructions.Cell(1, 1).Value = "Cómo llenar esta plantilla";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        instructions.Cell(3, 1).Value = "Tipo Identificación / Número Identificación / Razón Social son obligatorios.";
        instructions.Cell(4, 1).Value = "Tipo Identificación: código SRI — 04 = RUC, 05 = Cédula, 06 = Pasaporte, 07 = Consumidor Final, 08 = Exterior.";
        instructions.Cell(5, 1).Value = "Límite de Crédito y Días de Pago son numéricos, sin símbolos.";
        instructions.Cell(6, 1).Value = "No modifique los encabezados de la fila 1.";
        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    private static bool IsInstructionsSheet(string name) =>
        string.Equals(name, "Instrucciones", StringComparison.OrdinalIgnoreCase);
}
