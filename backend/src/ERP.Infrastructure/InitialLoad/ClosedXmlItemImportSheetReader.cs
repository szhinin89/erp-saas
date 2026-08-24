using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;

namespace ERP.Infrastructure.InitialLoad;

/// <summary>
/// Lectura/escritura del .xlsx de la plantilla de Catálogo de Productos vía ClosedXML — mismo
/// patrón que los demás readers de INITIAL-LOAD-ARCH-01. Una sola hoja de datos ("Productos") +
/// una hoja de instrucciones — nunca varias hojas relacionadas para la carga principal (regla
/// explícita del rediseño "importación inteligente").
/// </summary>
public sealed class ClosedXmlItemImportSheetReader : IItemImportSheetReader
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
                foreach (var column in ItemImportColumns.All)
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
        var sheet = workbook.Worksheets.Add("Productos");

        for (var i = 0; i < ItemImportColumns.All.Count; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = ItemImportColumns.All[i];
            cell.Style.Font.Bold = true;
        }

        sheet.Cell(2, 1).Value = "PROD-0001";
        sheet.Cell(2, 2).Value = "Producto Ejemplo";
        sheet.Cell(2, 3).Value = "Physical";
        sheet.Cell(2, 4).Value = "19";
        sheet.Cell(2, 5).Value = "2";
        sheet.Cell(2, 6).Value = "Bebidas";
        sheet.Cell(2, 7).Value = "Marca Ejemplo";
        sheet.Cell(2, 8).Value = "7861234567890";
        sheet.Cell(2, 9).Value = "";
        sheet.Cell(2, 10).Value = "";
        sheet.Cell(2, 11).Value = 9.99;
        sheet.Cell(2, 12).Value = "SI";
        sheet.Cell(2, 13).Value = "";
        sheet.Cell(2, 14).Value = "";
        sheet.Cell(2, 15).Value = "";
        sheet.Cell(2, 16).Value = "Producto cargado desde plantilla de Carga Inicial.";

        sheet.Columns().AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instrucciones");
        instructions.Cell(1, 1).Value = "Cómo llenar esta plantilla";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        instructions.Cell(3, 1).Value =
            "Una fila = un producto principal. SKU / Nombre / Tipo de Ítem / Unidad Base / Categoría / Marca / al menos un Código de Barra son obligatorios.";
        instructions.Cell(4, 1).Value =
            "Tipo de Ítem: código exacto de un tipo ya configurado en Ítems (ej. Physical, Service, Kit, Bundle, Digital).";
        instructions.Cell(5, 1).Value =
            "Unidad Base: código del catálogo SRI de unidades de medida (ej. 19 = Unidad, 07 = Kilogramo).";
        instructions.Cell(6, 1).Value =
            "IVA: código del catálogo SRI de tarifas de IVA — opcional, déjelo vacío si no aplica.";
        instructions.Cell(7, 1).Value =
            "Categoría / Marca: escriba el NOMBRE (no el código). Si no existe, la fila se bloquea salvo que active " +
            "\"Crear categorías/marcas nuevas si no existen\" al subir el archivo — en ese caso se crean automáticamente al confirmar.";
        instructions.Cell(8, 1).Value =
            "Código Barra 1/2/3: hasta 3 códigos de barras por producto. Al menos uno es obligatorio.";
        instructions.Cell(9, 1).Value =
            "PVP es opcional — sin PVP (o con un valor inválido), el producto se importa igual pero no queda disponible en POS.";
        instructions.Cell(10, 1).Value =
            "Disponible POS: SI/NO — se ignora y queda en NO si no hay PVP válido.";
        instructions.Cell(11, 1).Value =
            "Proveedor / Código Proveedor: opcionales. Proveedor debe coincidir con un único proveedor activo existente " +
            "(por nombre o identificación) para vincular el Código Proveedor — si no hay coincidencia única, el producto se " +
            "importa igual sin ese vínculo.";
        instructions.Cell(12, 1).Value =
            "Costo: se lee pero no se importa en esta versión — no afecta Kardex ni costeo.";
        instructions.Cell(13, 1).Value = "No modifique los encabezados de la fila 1.";
        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    private static bool IsInstructionsSheet(string name) =>
        string.Equals(name, "Instrucciones", StringComparison.OrdinalIgnoreCase);
}
