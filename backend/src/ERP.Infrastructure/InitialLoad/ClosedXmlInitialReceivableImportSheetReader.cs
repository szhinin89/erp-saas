using System.Globalization;
using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.Exceptions;

namespace ERP.Infrastructure.InitialLoad;

/// <summary>
/// Lectura/escritura del .xlsx de la plantilla de CxC Inicial vía ClosedXML (IL-5A) — mismo patrón
/// que <see cref="ClosedXmlInitialStockImportSheetReader"/>: encabezados exactos, texto invariante,
/// hoja "Instrucciones" ignorada al leer.
/// </summary>
public sealed class ClosedXmlInitialReceivableImportSheetReader : IInitialReceivableImportSheetReader
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
                throw new DomainRuleViolationException("El archivo no contiene ninguna hoja de datos.");

            var headerRow = sheet.Row(1);
            var columnIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastUsedColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            for (var col = 1; col <= lastUsedColumn; col++)
            {
                var header = headerRow.Cell(col).GetString().Trim();
                if (!string.IsNullOrEmpty(header) && !columnIndexes.TryAdd(header, col))
                    throw new DomainRuleViolationException("Encabezado duplicado: " + header);
            }

            var missing = InitialReceivableImportColumns.All.Where(c => !columnIndexes.ContainsKey(c)).ToList();
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
                foreach (var column in InitialReceivableImportColumns.All)
                {
                    var value = InvariantText(row.Cell(columnIndexes[column]));
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
        var sheet = workbook.Worksheets.Add("CxC Inicial");
        var columns = InitialReceivableImportColumns.All;

        for (var i = 0; i < columns.Count; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = columns[i];
            cell.Style.Font.Bold = true;
        }

        var example = new Dictionary<string, string>
        {
            [InitialReceivableImportColumns.IdentificationType] = "04",
            [InitialReceivableImportColumns.IdentificationNumber] = "1790012345001",
            [InitialReceivableImportColumns.DocumentNumber] = "001-001-000001234",
            [InitialReceivableImportColumns.IssueDate] = "2026-08-15",
            [InitialReceivableImportColumns.DueDate] = "2026-10-15",
            [InitialReceivableImportColumns.Balance] = "150.75",
            [InitialReceivableImportColumns.Currency] = "USD",
            [InitialReceivableImportColumns.CutoffDate] = "2026-09-30",
        };
        for (var i = 0; i < columns.Count; i++)
        {
            // Texto en todas las columnas: Excel no debe quitar ceros iniciales ni convertir montos o
            // fechas según la configuración regional del equipo.
            sheet.Column(i + 1).Style.NumberFormat.Format = "@";
            sheet.Cell(2, i + 1).Value = example[columns[i]];
        }

        sheet.Columns().AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instrucciones");
        instructions.Cell(1, 1).Value = "Cómo llenar esta plantilla";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        string[] lines =
        [
            "Una fila = un documento pendiente de cobro de un cliente a la fecha de corte. Se carga solo el "
                + "SALDO PENDIENTE: no se registran ventas, facturas ni cobros históricos.",
            "Todas las columnas son obligatorias: Tipo Identificación, Número Identificación, Número Documento, "
                + "Fecha Emisión, Fecha Vencimiento, Saldo Pendiente, Moneda y Fecha de corte.",
            "Cliente: Tipo Identificación SRI (04 RUC, 05 Cédula, 06 Pasaporte, 08 Exterior) y número. El cliente "
                + "debe existir, estar activo y tener rol Cliente — esta plantilla nunca crea clientes. "
                + "Consumidor Final no admite saldos.",
            "Número Documento: número del documento pendiente (p. ej. 001-001-000001234). No puede repetirse para el "
                + "mismo cliente en el archivo ni coincidir con una cuenta por cobrar existente del cliente; se "
                + "compara sin guiones, espacios ni mayúsculas.",
            "Fechas: AAAA-MM-DD. La emisión no puede ser posterior a la fecha de corte y el vencimiento no puede ser "
                + "anterior a la emisión. Un vencimiento anterior al corte se carga como deuda vencida.",
            "Saldo Pendiente: mayor a cero, con punto decimal y máximo 2 decimales (p. ej. 150.75), sin separador "
                + "de miles. No se redondea automáticamente.",
            "Moneda: obligatoria, solo USD. Vacía u otra moneda es error.",
            "Fecha de corte: la misma para todo el archivo, no futura, e igual a la fecha de apertura de saldos "
                + "definida para la empresa. Sin fecha de apertura definida no se pueden cargar saldos.",
            "Sucursal: los saldos se registran en la sucursal activa. Para otra sucursal, cambie de sucursal y "
                + "cargue otro archivo.",
            "Si cualquier fila tiene error, el lote no se confirma. No modifique los encabezados de la fila 1.",
        ];
        for (var i = 0; i < lines.Length; i++)
            instructions.Cell(i + 3, 1).Value = lines[i];
        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    /// <summary>
    /// El texto de una celda nunca depende de la cultura del servidor — un número de Excel se
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
