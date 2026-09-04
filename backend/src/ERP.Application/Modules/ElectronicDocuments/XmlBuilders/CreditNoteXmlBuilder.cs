using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ERP.Application.Modules.ElectronicDocuments.XmlBuilders;

/// <summary>
/// Construye el XML de Nota de Crédito (ficha técnica SRI, esquema "notaCredito" v1.1.0) a
/// partir, exclusivamente, de <see cref="ElectronicDocumentData"/> — mismo contrato y mismo
/// criterio que <see cref="InvoiceXmlBuilder"/> (P0-01 Fase 8/10): nunca recibe ni referencia
/// SalesReturn, EF Core, repositorios ni ningún módulo de negocio. El código de categoría
/// tributaria se resuelve vía <see cref="ISriTaxCategoryCodeResolver"/> — mismo servicio ya
/// usado por <see cref="InvoiceXmlBuilder"/>, reutilizado tal cual, sin duplicar el mapeo.
/// </summary>
public sealed class CreditNoteXmlBuilder : IElectronicDocumentXmlBuilder
{
    private const string XmlVersionValue = "1.1.0";
    private const string XmlEncodingValue = "UTF-8";
    private const int MaxAdditionalFields = 15;

    private readonly ISriTaxCategoryCodeResolver _taxCategoryCodeResolver;

    public CreditNoteXmlBuilder(ISriTaxCategoryCodeResolver taxCategoryCodeResolver)
    {
        _taxCategoryCodeResolver = taxCategoryCodeResolver;
    }

    public ElectronicDocumentType DocumentType => ElectronicDocumentType.CreditNote;

    public Result<ElectronicDocumentXml> Build(ElectronicDocumentData data)
    {
        var errors = Validate(data);
        if (errors.Count > 0)
            return Result<ElectronicDocumentXml>.ValidationFailure(string.Join(" ", errors));

        try
        {
            var accessKey = AccessKey.Create(BuildAccessKeyDigits(data));
            var xdoc = BuildXDocument(data, accessKey.Value);

            using var writer = new Utf8StringWriter();
            xdoc.Save(writer, SaveOptions.DisableFormatting);
            var xml = writer.ToString();

            XDocument.Parse(xml);

            return Result<ElectronicDocumentXml>.Success(
                new ElectronicDocumentXml(
                    Xml: xml,
                    Encoding: XmlEncodingValue,
                    Version: XmlVersionValue,
                    DocumentType: DocumentType,
                    Environment: data.Emission.Environment,
                    AccessKey: accessKey.Value,
                    GeneratedAtUtc: DateTime.UtcNow
                )
            );
        }
        catch (ArgumentException ex)
        {
            return Result<ElectronicDocumentXml>.ValidationFailure(
                $"No se pudo construir un XML válido: {ex.Message}"
            );
        }
        catch (XmlException ex)
        {
            return Result<ElectronicDocumentXml>.ValidationFailure(
                $"El XML generado no está bien formado: {ex.Message}"
            );
        }
    }

    // ── Validación estructural (nodos obligatorios, ficha técnica NC) ───────
    private List<string> Validate(ElectronicDocumentData data)
    {
        var errors = new List<string>();

        if (data.Totals is null)
            errors.Add("Una nota de crédito debe tener totales.");
        if (data.Details.Count == 0)
            errors.Add("Una nota de crédito debe tener al menos un detalle.");
        if (data.TaxSummary.Count == 0)
            errors.Add("Una nota de crédito debe tener al menos un impuesto.");
        if (string.IsNullOrWhiteSpace(data.Reason))
            errors.Add("El motivo de la nota de crédito es obligatorio.");
        if (data.ModifiedDocument is null)
            errors.Add("La nota de crédito debe referenciar el comprobante que modifica.");

        if (string.IsNullOrWhiteSpace(data.Issuer.TaxId) || data.Issuer.TaxId.Length != 13)
            errors.Add("El RUC del emisor debe tener 13 dígitos.");
        if (string.IsNullOrWhiteSpace(data.Issuer.LegalName))
            errors.Add("La razón social del emisor es obligatoria.");
        if (string.IsNullOrWhiteSpace(data.Issuer.MatrixAddress))
            errors.Add("La dirección matriz del emisor es obligatoria.");

        if (string.IsNullOrWhiteSpace(data.Emission.Environment))
            errors.Add("El ambiente SRI es obligatorio.");
        if (string.IsNullOrWhiteSpace(data.Emission.EmissionType))
            errors.Add("El tipo de emisión SRI es obligatorio.");
        if (string.IsNullOrWhiteSpace(data.Emission.DocTypeCode))
            errors.Add("El código de tipo de comprobante SRI es obligatorio.");
        if (data.Emission.Establishment?.Length != 3)
            errors.Add("El código de establecimiento debe tener 3 dígitos.");
        if (data.Emission.EmissionPoint?.Length != 3)
            errors.Add("El código de punto de emisión debe tener 3 dígitos.");
        if (data.Emission.Sequential?.Length != 9)
            errors.Add("El secuencial debe tener 9 dígitos.");
        if (string.IsNullOrWhiteSpace(data.Emission.EstablishmentAddress))
            errors.Add("La dirección del establecimiento emisor es obligatoria.");

        if (string.IsNullOrWhiteSpace(data.Counterparty.IdentificationType))
            errors.Add("El tipo de identificación del comprador es obligatorio.");
        if (string.IsNullOrWhiteSpace(data.Counterparty.IdentificationNumber))
            errors.Add("La identificación del comprador es obligatoria.");
        if (string.IsNullOrWhiteSpace(data.Counterparty.LegalName))
            errors.Add("La razón social del comprador es obligatoria.");

        if (data.AdditionalInfo.Count > MaxAdditionalFields)
            errors.Add(
                $"El comprobante tiene {data.AdditionalInfo.Count} campos adicionales; el SRI permite un máximo de {MaxAdditionalFields}."
            );

        foreach (var line in data.Details)
        {
            if (string.IsNullOrWhiteSpace(line.Description))
                errors.Add("Existe una línea de detalle sin descripción.");
        }

        var distinctTaxCodes = data
            .Details.SelectMany(d => d.Taxes)
            .Select(t => t.TaxCode)
            .Concat(data.TaxSummary.Select(t => t.TaxCode))
            .Distinct();
        foreach (var taxCode in distinctTaxCodes)
        {
            if (_taxCategoryCodeResolver.Resolve(taxCode) is null)
                errors.Add(
                    $"No se pudo generar el documento electrónico: el producto tiene un código de impuesto ('{taxCode}') "
                        + "que el sistema no reconoce. Contacta a soporte técnico e indica este código."
                );
        }

        return errors;
    }

    // ── Clave de acceso (49 dígitos, dígito verificador módulo 11) ──────────
    // Mismo algoritmo exacto que InvoiceXmlBuilder — genérico del SRI, no específico de Factura.
    // No se extrae a un helper compartido: InvoiceXmlBuilder es infraestructura FROZEN (ADR-023)
    // y no se modifica para forzar una reutilización; para dos consumidores la duplicación
    // puntual de este algoritmo cerrado es preferible a introducir un nuevo punto de acoplamiento.
    private static string BuildAccessKeyDigits(ElectronicDocumentData data)
    {
        var fecha = data.Emission.IssueDate.ToString("ddMMyyyy", CultureInfo.InvariantCulture);
        var numericCode = ComputeNumericCode(
            $"{data.Issuer.TaxId}|{data.Emission.Establishment}|{data.Emission.EmissionPoint}|{data.Emission.Sequential}|{data.Emission.DocTypeCode}"
        );

        var digits48 = string.Concat(
            fecha,
            data.Emission.DocTypeCode,
            data.Issuer.TaxId,
            data.Emission.Environment,
            data.Emission.Establishment,
            data.Emission.EmissionPoint,
            data.Emission.Sequential,
            numericCode,
            data.Emission.EmissionType
        );

        return digits48 + ComputeCheckDigit(digits48);
    }

    private static string ComputeNumericCode(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var value = BitConverter.ToUInt32(hash, 0) % 100_000_000u;
        return value.ToString("D8", CultureInfo.InvariantCulture);
    }

    private static char ComputeCheckDigit(string digits)
    {
        ReadOnlySpan<int> weights = [2, 3, 4, 5, 6, 7];
        var sum = 0;
        var weightIndex = 0;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            sum += (digits[i] - '0') * weights[weightIndex];
            weightIndex = (weightIndex + 1) % weights.Length;
        }

        var check = 11 - (sum % 11);
        if (check == 11)
            check = 0;
        else if (check == 10)
            check = 1;
        return (char)('0' + check);
    }

    // ── Construcción del árbol XML (orden exacto exigido por NotaCredito_V1.1.0.xsd) ────────
    private XDocument BuildXDocument(ElectronicDocumentData data, string accessKey)
    {
        var totals = data.Totals!;
        var modified = data.ModifiedDocument!;

        var infoTributaria = new XElement(
            "infoTributaria",
            new XElement("ambiente", data.Emission.Environment),
            new XElement("tipoEmision", data.Emission.EmissionType),
            new XElement("razonSocial", data.Issuer.LegalName),
            data.Issuer.TradeName is null
                ? null
                : new XElement("nombreComercial", data.Issuer.TradeName),
            new XElement("ruc", data.Issuer.TaxId),
            new XElement("claveAcceso", accessKey),
            new XElement("codDoc", data.Emission.DocTypeCode),
            new XElement("estab", data.Emission.Establishment),
            new XElement("ptoEmi", data.Emission.EmissionPoint),
            new XElement("secuencial", data.Emission.Sequential),
            new XElement("dirMatriz", data.Issuer.MatrixAddress),
            data.Issuer.TaxRegime is null
                ? null
                : new XElement("contribuyenteRimpe", data.Issuer.TaxRegime)
        );

        var infoNotaCredito = new XElement(
            "infoNotaCredito",
            new XElement(
                "fechaEmision",
                data.Emission.IssueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            ),
            new XElement("dirEstablecimiento", data.Emission.EstablishmentAddress),
            new XElement("tipoIdentificacionComprador", data.Counterparty.IdentificationType),
            new XElement("razonSocialComprador", data.Counterparty.LegalName),
            new XElement("identificacionComprador", data.Counterparty.IdentificationNumber),
            new XElement("obligadoContabilidad", data.Issuer.IsAccountingRequired ? "SI" : "NO"),
            new XElement("codDocModificado", modified.DocTypeCode),
            new XElement("numDocModificado", modified.Number),
            new XElement(
                "fechaEmisionDocSustento",
                modified.IssueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            ),
            new XElement("totalSinImpuestos", FormatMoney(totals.Subtotal)),
            new XElement("valorModificacion", FormatMoney(totals.GrandTotal)),
            new XElement("moneda", totals.CurrencyCode),
            new XElement("totalConImpuestos", data.TaxSummary.Select(BuildTotalImpuesto)),
            new XElement("motivo", data.Reason)
        );

        var detalles = new XElement("detalles", data.Details.Select(BuildDetalle));

        var root = new XElement(
            "notaCredito",
            new XAttribute("id", "comprobante"),
            new XAttribute("version", XmlVersionValue),
            infoTributaria,
            infoNotaCredito,
            detalles,
            data.AdditionalInfo.Count == 0 ? null : BuildInfoAdicional(data.AdditionalInfo)
        );

        return new XDocument(new XDeclaration("1.0", XmlEncodingValue, null), root);
    }

    private XElement BuildTotalImpuesto(ElectronicDocumentTaxSummary tax) =>
        new(
            "totalImpuesto",
            new XElement("codigo", ResolveSriTaxCode(tax.TaxCode)),
            new XElement("codigoPorcentaje", tax.TaxPercentageCode),
            new XElement("baseImponible", FormatMoney(tax.TaxableBase)),
            new XElement("valor", FormatMoney(tax.TaxAmount))
        );

    private XElement BuildDetalle(ElectronicDocumentDetailLine line) =>
        new(
            "detalle",
            string.IsNullOrWhiteSpace(line.Code) ? null : new XElement("codigoInterno", line.Code),
            new XElement("descripcion", line.Description),
            new XElement("cantidad", FormatQuantity(line.Quantity)),
            new XElement("precioUnitario", FormatQuantity(line.UnitPrice)),
            new XElement("descuento", FormatMoney(line.Discount)),
            new XElement("precioTotalSinImpuesto", FormatMoney(line.Subtotal)),
            new XElement("impuestos", line.Taxes.Select(BuildImpuestoDetalle))
        );

    private XElement BuildImpuestoDetalle(ElectronicDocumentDetailTax tax) =>
        new(
            "impuesto",
            new XElement("codigo", ResolveSriTaxCode(tax.TaxCode)),
            new XElement("codigoPorcentaje", tax.TaxPercentageCode),
            new XElement("tarifa", FormatMoney(tax.TaxRate)),
            new XElement("baseImponible", FormatMoney(tax.TaxableBase)),
            new XElement("valor", FormatMoney(tax.TaxAmount))
        );

    private static XElement BuildInfoAdicional(
        IReadOnlyList<ElectronicDocumentAdditionalField> fields
    ) =>
        new(
            "infoAdicional",
            fields.Select(f => new XElement(
                "campoAdicional",
                new XAttribute("nombre", f.Name),
                f.Value
            ))
        );

    private string ResolveSriTaxCode(string taxCode) =>
        _taxCategoryCodeResolver.Resolve(taxCode)
        ?? throw new InvalidOperationException(
            $"Invariante violada: '{taxCode}' debía estar validado como resoluble antes de construir el XML."
        );

    private static string FormatMoney(decimal value) =>
        value.ToString("F2", CultureInfo.InvariantCulture);

    private static string FormatQuantity(decimal value) =>
        value.ToString("F6", CultureInfo.InvariantCulture);

    /// <summary>Ver XML-01 en InvoiceXmlBuilder — misma corrección de encoding, misma razón.</summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
