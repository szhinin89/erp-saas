using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using System.Globalization;
using System.Text;

namespace ERP.Infrastructure.Modules.Purchases.PurchaseReception;

/// <summary>
/// Interpreta el TXT de recepción de comprobantes del SRI (delimitado por tabulador, con encabezado).
/// Procesa filas <c>TIPO_COMPROBANTE = Factura</c> y <c>Nota de Crédito</c> — otros tipos se cuentan
/// como omitidos (no soportados todavía), sin abortar el archivo. No conoce base de datos ni EF Core.
/// </summary>
public sealed class PurchaseInvoiceTxtParser : IPurchaseReceptionParser
{
    private const char Delimiter = '\t';

    private static readonly string[] RequiredColumns =
    [
        "RUC_EMISOR",
        "RAZON_SOCIAL_EMISOR",
        "TIPO_COMPROBANTE",
        "SERIE_COMPROBANTE",
        "CLAVE_ACCESO",
        "FECHA_AUTORIZACION",
        "FECHA_EMISION",
        "VALOR_SIN_IMPUESTOS",
        "IVA",
        "IMPORTE_TOTAL",
    ];

    public async Task<PurchaseReceptionParseResult> ParseAsync(
        Stream fileContent,
        CancellationToken cancellationToken = default
    )
    {
        var records = new List<PurchaseReceptionRecord>();
        var errors = new List<PurchaseReceptionParseError>();
        var skippedUnsupported = 0;

        using var buffer = new MemoryStream();
        await fileContent.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        using var reader = new StreamReader(
            new MemoryStream(bytes),
            DetectEncoding(bytes),
            detectEncodingFromByteOrderMarks: true
        );

        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
            return new PurchaseReceptionParseResult(records, errors, skippedUnsupported);

        var columnIndex = BuildColumnIndex(headerLine);
        var missing = RequiredColumns.Where(c => !columnIndex.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            errors.Add(
                new PurchaseReceptionParseError(
                    1,
                    headerLine,
                    $"Encabezado inválido — faltan columnas: {string.Join(", ", missing)}."
                )
            );
            return new PurchaseReceptionParseResult(records, errors, skippedUnsupported);
        }

        var lineNumber = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parsed = TryParseLine(
                line,
                lineNumber,
                columnIndex,
                out var record,
                out var reason,
                out var isSupportedType
            );
            if (!parsed)
            {
                errors.Add(new PurchaseReceptionParseError(lineNumber, line, reason!));
                continue;
            }

            if (!isSupportedType)
            {
                skippedUnsupported++;
                continue;
            }

            records.Add(record!);
        }

        return new PurchaseReceptionParseResult(records, errors, skippedUnsupported);
    }

    /// <summary>
    /// El TXT del SRI casi siempre es UTF-8 sin BOM, pero algunos exports (frecuente en notas de
    /// crédito) vienen en Windows-1252/ISO-8859-1 de un solo byte por acento. Forzar UTF-8 en ese
    /// caso corrompe "Crédito" en <c>TIPO_COMPROBANTE</c>, la fila deja de mapear a
    /// <see cref="PurchaseReceptionSourceDocType.CreditNote"/> y se cuenta como omitida sin error
    /// visible — el archivo "no carga" en silencio. Se detecta con una decodificación UTF-8
    /// estricta (falla ante cualquier byte inválido); si falla, se usa <see cref="Encoding.Latin1"/>
    /// — mismo mapeo que Windows-1252 para el rango de acentos que usa este archivo (á é í ó ú ñ),
    /// sin requerir el paquete de code pages.
    /// </summary>
    private static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8;

        try
        {
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
            return Encoding.UTF8;
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1;
        }
    }

    private static Dictionary<string, int> BuildColumnIndex(string headerLine)
    {
        var columns = headerLine.Split(Delimiter);
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Length; i++)
            index[columns[i].Trim()] = i;
        return index;
    }

    private static bool TryParseLine(
        string line,
        int lineNumber,
        Dictionary<string, int> columnIndex,
        out PurchaseReceptionRecord? record,
        out string? reason,
        out bool isSupportedType
    )
    {
        record = null;
        reason = null;
        isSupportedType = false;

        var fields = line.Split(Delimiter);
        var maxRequiredIndex = RequiredColumns.Max(c => columnIndex[c]);
        if (fields.Length <= maxRequiredIndex)
        {
            reason =
                $"La línea tiene {fields.Length} columna(s), se esperaban al menos {maxRequiredIndex + 1}.";
            return false;
        }

        string Field(string name) => fields[columnIndex[name]].Trim();
        string? OptionalField(string name) =>
            columnIndex.TryGetValue(name, out var idx) && idx < fields.Length
                ? fields[idx].Trim()
                : null;

        var docTypeRaw = Field("TIPO_COMPROBANTE");
        var sourceDocType = PurchaseReceptionSourceDocTypeMapper.FromRawText(docTypeRaw);
        isSupportedType =
            sourceDocType == PurchaseReceptionSourceDocType.Invoice
            || sourceDocType == PurchaseReceptionSourceDocType.CreditNote;
        if (!isSupportedType)
            return true; // línea válida, pero de un tipo no soportado todavía — se cuenta como omitida, no como error

        var ruc = Field("RUC_EMISOR");
        var supplierName = Field("RAZON_SOCIAL_EMISOR");
        var invoiceNumber = Field("SERIE_COMPROBANTE");
        var accessKey = Field("CLAVE_ACCESO");

        if (
            string.IsNullOrWhiteSpace(ruc)
            || string.IsNullOrWhiteSpace(accessKey)
            || string.IsNullOrWhiteSpace(invoiceNumber)
        )
        {
            reason = "RUC_EMISOR, CLAVE_ACCESO y SERIE_COMPROBANTE son obligatorios.";
            return false;
        }

        if (
            !DateOnly.TryParseExact(
                Field("FECHA_EMISION"),
                "dd/MM/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var issueDate
            )
        )
        {
            reason = $"FECHA_EMISION inválida: '{Field("FECHA_EMISION")}'.";
            return false;
        }

        if (
            !DateTime.TryParseExact(
                Field("FECHA_AUTORIZACION"),
                "dd/MM/yyyy HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var authorizationDate
            )
        )
        {
            reason = $"FECHA_AUTORIZACION inválida: '{Field("FECHA_AUTORIZACION")}'.";
            return false;
        }
        // El TXT no trae offset — se marca Kind=Utc sin desplazar el valor (igual que el resto del
        // pipeline la trata como timestamp opaco). Necesario porque Npgsql exige Kind=Utc para
        // "timestamp with time zone"; sin esto, persistir el documento en Fase 2 lanza ArgumentException.
        authorizationDate = DateTime.SpecifyKind(authorizationDate, DateTimeKind.Utc);

        if (
            !TryParseAmount(Field("VALOR_SIN_IMPUESTOS"), out var subtotal)
            || !TryParseAmount(Field("IVA"), out var vat)
        )
        {
            reason = "VALOR_SIN_IMPUESTOS e IVA deben ser valores numéricos válidos.";
            return false;
        }

        var totalRaw = Field("IMPORTE_TOTAL");
        decimal total;
        if (string.IsNullOrWhiteSpace(totalRaw))
        {
            // IMPORTE_TOTAL viene vacío en algunas notas de crédito del SRI — se calcula, nunca se
            // inventa: mismo criterio que la UI aplicará para mostrar el total de crédito.
            if (sourceDocType != PurchaseReceptionSourceDocType.CreditNote)
            {
                reason = "IMPORTE_TOTAL es obligatorio.";
                return false;
            }
            total = subtotal + vat;
        }
        else if (!TryParseAmount(totalRaw, out total))
        {
            reason = "IMPORTE_TOTAL debe ser un valor numérico válido.";
            return false;
        }

        record = new PurchaseReceptionRecord(
            lineNumber,
            sourceDocType,
            ruc,
            supplierName,
            invoiceNumber,
            accessKey,
            issueDate,
            authorizationDate,
            OptionalField("IDENTIFICACION_RECEPTOR"),
            subtotal,
            vat,
            total,
            NullIfEmpty(OptionalField("NUMERO_DOCUMENTO_MODIFICADO"))
        );
        return true;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool TryParseAmount(string raw, out decimal value) =>
        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
