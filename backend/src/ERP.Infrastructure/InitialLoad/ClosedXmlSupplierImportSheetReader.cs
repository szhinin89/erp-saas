using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.Exceptions;

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

            // IL-3A: una plantilla anterior (sin Tipo Entidad Legal ni datos fiscales) se rechaza en
            // vez de leer esas columnas como vacías.
            var missing = SupplierImportColumns.All.Where(c => !columnIndexes.ContainsKey(c)).ToList();
            if (missing.Count > 0)
                throw new DomainRuleViolationException(
                    "Faltan encabezados de la plantilla: " + string.Join(", ", missing)
                        + ". Descargue la plantilla actual."
                );

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
                    var value = columnIndexes.TryGetValue(column, out var colIndex)
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

        var example = new Dictionary<string, string>
        {
            [SupplierImportColumns.IdentificationType] = "04",
            [SupplierImportColumns.IdentificationNumber] = "1790012345001",
            [SupplierImportColumns.LegalEntityTypeCode] = "",
            [SupplierImportColumns.LegalName] = "Proveedor Ejemplo S.A.",
            [SupplierImportColumns.TradeName] = "Proveedor Ejemplo",
            [SupplierImportColumns.CountryCode] = "EC",
            [SupplierImportColumns.Email] = "contacto@proveedor-ejemplo.com",
            [SupplierImportColumns.Phone] = "0999999999",
            [SupplierImportColumns.PaymentTermCode] = "CONTADO",
            [SupplierImportColumns.IsRequiredToKeepAccounting] = "SI",
            [SupplierImportColumns.IsRetentionExempt] = "NO",
        };
        for (var i = 0; i < SupplierImportColumns.All.Count; i++)
        {
            var column = SupplierImportColumns.All[i];
            // Texto: Excel no debe convertir RUC/teléfonos a número (pierden el 0 inicial).
            if (column is SupplierImportColumns.IdentificationType
                or SupplierImportColumns.IdentificationNumber
                or SupplierImportColumns.Phone)
                sheet.Column(i + 1).Style.NumberFormat.Format = "@";
            sheet.Cell(2, i + 1).Value = example[column];
        }

        sheet.Columns().AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instrucciones");
        instructions.Cell(1, 1).Value = "Cómo llenar esta plantilla";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        string[] lines =
        [
            "Obligatorios: Tipo Identificación, Número Identificación, Razón Social, Condición de Pago, "
                + "Obligado a llevar contabilidad y Exento de retención. Tipo Entidad Legal es obligatorio para Exterior.",
            "Tipo Identificación: solo los permitidos para proveedores — 04 = RUC, 08 = Exterior.",
            "Número Identificación: escriba el número como texto (columna formateada como Texto). Si Excel "
                + "quita el 0 inicial de un RUC, la fila se bloquea.",
            "Tipo Entidad Legal: código del catálogo — 1 = Persona Natural, 2 = Sociedad Privada, "
                + "3 = Institución Pública. Para RUC se deduce; si lo informa debe coincidir.",
            "Condición de Pago: código existente y activo (p. ej. CONTADO). No hay valor por defecto. "
                + "Se guarda solo para la empresa actual.",
            "Obligado a llevar contabilidad / Exento de retención: únicamente SI o NO, sin valor por defecto. "
                + "Se aplican solo al registrar el rol Proveedor; nunca modifican un proveedor existente.",
            "Tercero ya existente: no se duplica. Si no es proveedor se le asigna el rol Proveedor; si ya es "
                + "proveedor la fila es idempotente. Su ficha maestra (nombre, contacto) no se modifica.",
            "Proveedor con rol revocado: se reactiva conservando sus datos fiscales previos (el SI/NO del archivo no "
                + "se aplica). Si no tiene datos fiscales registrados, la fila se bloquea hasta corregirlos en su ficha.",
            "Email / Teléfono (opcionales): crean el contacto de compras solo para proveedores nuevos.",
            "Una identificación no puede repetirse en el archivo. Si cualquier fila tiene error, el lote no se confirma.",
            "No modifique los encabezados de la fila 1.",
        ];
        for (var i = 0; i < lines.Length; i++)
            instructions.Cell(i + 3, 1).Value = lines[i];
        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    private static bool IsInstructionsSheet(string name) =>
        string.Equals(name, "Instrucciones", StringComparison.OrdinalIgnoreCase);
}
