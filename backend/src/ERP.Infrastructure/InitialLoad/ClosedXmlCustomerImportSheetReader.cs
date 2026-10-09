using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.Exceptions;

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

            var obsolete = CustomerImportColumns.Obsolete.Where(columnIndexes.ContainsKey).ToList();
            if (obsolete.Count > 0)
                throw new DomainRuleViolationException(
                    "La plantilla ya no admite: " + string.Join(", ", obsolete)
                        + ". Las condiciones comerciales se configuran por empresa. Descargue la plantilla actual."
                );
            var missing = CustomerImportColumns.All.Where(c => !columnIndexes.ContainsKey(c)).ToList();
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
                foreach (var column in CustomerImportColumns.All)
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
        var sheet = workbook.Worksheets.Add("Clientes");

        for (var i = 0; i < CustomerImportColumns.All.Count; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = CustomerImportColumns.All[i];
            cell.Style.Font.Bold = true;
        }

        var example = new Dictionary<string, string>
        {
            [CustomerImportColumns.IdentificationType] = "04",
            [CustomerImportColumns.IdentificationNumber] = "1790012345001",
            [CustomerImportColumns.LegalEntityTypeCode] = "",
            [CustomerImportColumns.LegalName] = "Comercial Ejemplo S.A.",
            [CustomerImportColumns.TradeName] = "Comercial Ejemplo",
            [CustomerImportColumns.CountryCode] = "EC",
            [CustomerImportColumns.Email] = "contacto@ejemplo.com",
            [CustomerImportColumns.Phone] = "0999999999",
            [CustomerImportColumns.PaymentTermCode] = "CONTADO",
        };
        for (var i = 0; i < CustomerImportColumns.All.Count; i++)
        {
            var column = CustomerImportColumns.All[i];
            // Texto: Excel no debe convertir cédulas/RUC/teléfonos a número (pierden el 0 inicial).
            if (column is CustomerImportColumns.IdentificationType
                or CustomerImportColumns.IdentificationNumber
                or CustomerImportColumns.Phone)
                sheet.Column(i + 1).Style.NumberFormat.Format = "@";
            sheet.Cell(2, i + 1).Value = example[column];
        }

        sheet.Columns().AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instrucciones");
        instructions.Cell(1, 1).Value = "Cómo llenar esta plantilla";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        string[] lines =
        [
            "Obligatorios: Tipo Identificación, Número Identificación, Razón Social y Condición de Pago. "
                + "Tipo Entidad Legal es obligatorio para Pasaporte, Exterior y Placa.",
            "Tipo Identificación: código SRI — 04 = RUC, 05 = Cédula, 06 = Pasaporte, 08 = Exterior, 09 = Placa. "
                + "07 = Consumidor Final no se carga por plantilla.",
            "Número Identificación: escriba el número como texto (columna formateada como Texto). Si Excel "
                + "quita el 0 inicial de una cédula o RUC, la fila se bloquea.",
            "Tipo Entidad Legal: código del catálogo — 1 = Persona Natural, 2 = Sociedad Privada, "
                + "3 = Institución Pública. Para RUC/Cédula se deduce; si lo informa debe coincidir.",
            "Condición de Pago: código existente y activo de la empresa (p. ej. CONTADO). No hay valor por defecto. "
                + "Se guarda solo para la empresa actual.",
            "Tercero ya existente: no se duplica. Si no es cliente se le asigna el rol Cliente; si ya es cliente "
                + "la fila es idempotente. Su ficha maestra (nombre, contacto) no se modifica.",
            "Email / Teléfono (opcionales): crean el contacto de facturación solo para clientes nuevos.",
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
