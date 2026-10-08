using System.Globalization;
using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.Exceptions;

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
                if (!string.IsNullOrEmpty(header))
                {
                    if (!columnIndexes.TryAdd(header, col))
                        throw new DomainRuleViolationException("Encabezado duplicado: " + header);
                }
            }

            if (columnIndexes.ContainsKey("IVA"))
                throw new DomainRuleViolationException("La columna IVA fue sustituida por IVA Venta e IVA Compra. Descargue la plantilla actual.");
            var missing = ItemImportColumns.All.Where(c => !columnIndexes.ContainsKey(c)).ToList();
            if (missing.Count > 0)
                throw new DomainRuleViolationException("Faltan encabezados de la plantilla: " + string.Join(", ", missing));

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
                    var value = columnIndexes.TryGetValue(column, out var colIndex)
                        ? column == ItemImportColumns.Pvp && row.Cell(colIndex).DataType == XLDataType.Number
                            ? row.Cell(colIndex).GetDouble().ToString("R", CultureInfo.InvariantCulture)
                            : row.Cell(colIndex).GetString().Trim()
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

        var example = new Dictionary<string, string>
        {
            [ItemImportColumns.Sku] = "PROD-0001",
            [ItemImportColumns.Name] = "Producto Ejemplo",
            [ItemImportColumns.ItemTypeCode] = "Physical",
            [ItemImportColumns.UomCode] = "19",
            [ItemImportColumns.SaleVatCode] = "2",
            [ItemImportColumns.PurchaseVatCode] = "2",
            [ItemImportColumns.CategoryName] = "No aplica",
            [ItemImportColumns.BrandName] = "No aplica",
            [ItemImportColumns.Barcode1] = "PROD-0001",
            [ItemImportColumns.BarcodeType1] = "Internal",
            [ItemImportColumns.Pvp] = "9.99",
            [ItemImportColumns.AvailableOnPos] = "SI",
            [ItemImportColumns.Observations] = "Producto cargado desde plantilla de Carga Inicial.",
        };
        for (var i = 0; i < ItemImportColumns.All.Count; i++)
            sheet.Cell(2, i + 1).Value = example.GetValueOrDefault(ItemImportColumns.All[i], "");

        sheet.Columns().AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instrucciones");
        instructions.Cell(1, 1).Value = "Cómo llenar esta plantilla";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        instructions.Cell(3, 1).Value =
            "Una fila = un producto principal. SKU / Nombre / Tipo de Ítem / Unidad Base / Categoría / Marca / Disponible POS / al menos un Código de Barra con su tipo son obligatorios.";
        instructions.Cell(4, 1).Value =
            "Tipo de Ítem: código exacto de un tipo activo ya configurado. Es clasificación; esta carga crea naturaleza Product.";
        instructions.Cell(5, 1).Value =
            "Unidad Base: código del catálogo SRI de unidades de medida (ej. 19 = Unidad, 07 = Kilogramo).";
        instructions.Cell(6, 1).Value =
            "IVA Venta / IVA Compra: códigos independientes del catálogo SRI existente. Vacío = no aplica; no se asume tarifa ni se copia entre compra y venta.";
        instructions.Cell(7, 1).Value =
            "Categoría / Marca: obligatorias por NOMBRE. Sin clasificación aplicable escriba explícitamente No aplica en ambas; vacío nunca se sustituye. Si no existe, la fila se bloquea salvo que active "
            + "\"Crear categorías/marcas nuevas si no existen\" al subir el archivo — en ese caso se crean automáticamente al confirmar.";
        instructions.Cell(8, 1).Value =
            "Código Barra 1/2/3: hasta 3 códigos, cada uno con Tipo Código Barra 1/2/3 explícito del catálogo activo. No se infiere el tipo; el primer código no vacío es principal. Guarde códigos como texto para conservar ceros iniciales.";
        instructions.Cell(9, 1).Value =
            "PVP: obligatorio y mayor que cero si Disponible POS=SI; opcional con NO. Si se informa debe ser un número no negativo con punto decimal y sin separador de miles.";
        instructions.Cell(10, 1).Value =
            "Disponible POS: únicamente SI/NO, obligatorio. SI sin PVP válido mayor que cero bloquea la fila; nunca se convierte a NO silenciosamente.";
        instructions.Cell(11, 1).Value =
            "Proveedor / Código Proveedor: opcionales. Proveedor debe coincidir con un único proveedor activo existente "
            + "(por nombre o identificación) para vincular el Código Proveedor — si no hay coincidencia única, el producto se "
            + "importa igual sin ese vínculo.";
        instructions.Cell(12, 1).Value =
            "Costo: fuera del importador; si se informa se advierte y no se aplica. Stock, Bodega y Kardex no se cargan desde esta plantilla.";
        instructions.Cell(13, 1).Value = "No modifique ni elimine encabezados. SKU, barcode y proveedor+código duplicados entre filas bloquean todas las filas implicadas; comparación con trim y mayúsculas.";
        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    private static bool IsInstructionsSheet(string name) =>
        string.Equals(name, "Instrucciones", StringComparison.OrdinalIgnoreCase);
}
