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
/// RETENTIONS-SRI-XML-MAPPER-03B — construye el XML de Comprobante de Retención (ficha técnica
/// SRI, esquema "comprobanteRetencion" v1.0.0) a partir, exclusivamente, de
/// <see cref="RetentionElectronicDocumentData"/>. Nunca recibe ni referencia RetentionDocument,
/// EF Core, repositorios ni ningún módulo de negocio — mismo criterio que
/// <see cref="InvoiceXmlBuilder"/>/<see cref="CreditNoteXmlBuilder"/>.
///
/// Versión de esquema elegida deliberadamente: <c>1.0.0</c>, no <c>2.0.0</c>. El XSD 2.0.0 exige
/// tres elementos que <see cref="RetentionElectronicDocumentData"/> no modela y que el dominio
/// (<c>RetentionDocument</c>) no tiene forma de conocer sin inventar el dato — <c>parteRel</c>
/// ("SI"/"NO", ¿sujeto retenido es parte relacionada?, minOccurs=1), <c>pagoLocExt</c> ("01"/"02",
/// pago local o al exterior, minOccurs=1) e <c>impuestosDocSustento</c> (desglose de IVA/ICE del
/// propio documento sustento, minOccurs=1) — hardcodear cualquiera de los tres violaría la regla
/// del proyecto de no quemar datos configurables/de negocio. El XSD 1.0.0 no tiene ninguno de
/// esos tres campos y es estructuralmente compatible con el modelo tal cual existe hoy; a cambio
/// no tiene <c>codSustento</c>, así que ese dato (cuando <see cref="RetentionElectronicDocumentSourceDocument.TaxSupportCode"/>
/// existe) no se emite en el XML en esta fase — es una limitación de la versión de esquema
/// elegida, no un dato inventado ni un gap nuevo del modelo.
///
/// No inyecta ningún resolver de catálogo de impuestos: a diferencia de Factura/Nota de Crédito
/// (donde <c>ISriTaxCategoryCodeResolver</c> traduce un <c>TaxCode</c> interno a código SRI),
/// <see cref="RetentionElectronicDocumentTaxLine.SriTaxTypeCode"/> ya viene resuelto por
/// <c>RetentionElectronicDocumentDataProvider</c> desde <c>SriRetentionTaxTypeCodes</c> — este
/// builder solo lo transcribe, nunca lo recalcula ni lo vuelve a resolver.
/// </summary>
public sealed class RetentionXmlBuilder : IRetentionXmlBuilder
{
    private const string XmlVersionValue = "1.0.0";
    private const string XmlEncodingValue = "UTF-8";

    /// <summary>Ficha técnica SRI: máximo de nodos &lt;campoAdicional&gt; permitidos dentro de &lt;infoAdicional&gt;.</summary>
    private const int MaxAdditionalFields = 15;

    /// <summary>numDocSustento (XSD 1.0.0) exige exactamente 15 dígitos numéricos — el formato
    /// habitualmente almacenado es "EST-PTO-SECUENCIAL" (3+3+9 = 15 dígitos con guiones). Si al
    /// quitar los guiones no quedan exactamente 15 dígitos, el elemento se omite (es opcional en
    /// este esquema) en vez de transmitir un valor que no cumple el patrón del SRI.</summary>
    private const int NumDocSustentoDigits = 15;

    public ElectronicDocumentType DocumentType => ElectronicDocumentType.Retention;

    public Result<ElectronicDocumentXml> Build(RetentionElectronicDocumentData data)
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

            // Verificación explícita de buena formación, mismo criterio que Invoice/CreditNote.
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

    // ── Validación estructural (nodos obligatorios) ─────────────────────────
    private static List<string> Validate(RetentionElectronicDocumentData data)
    {
        var errors = new List<string>();

        if (data.Lines.Count == 0)
            errors.Add("Un comprobante de retención debe tener al menos una línea de impuesto retenido.");

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

        if (string.IsNullOrWhiteSpace(data.SubjectWithheld.IdentificationType))
            errors.Add("El tipo de identificación del sujeto retenido es obligatorio.");
        if (string.IsNullOrWhiteSpace(data.SubjectWithheld.IdentificationNumber))
            errors.Add("La identificación del sujeto retenido es obligatoria.");
        if (string.IsNullOrWhiteSpace(data.SubjectWithheld.LegalName))
            errors.Add("La razón social del sujeto retenido es obligatoria.");

        if (string.IsNullOrWhiteSpace(data.RetentionInfo.FiscalPeriod))
            errors.Add("El período fiscal es obligatorio.");

        if (string.IsNullOrWhiteSpace(data.NumeroCompleto))
            errors.Add("El número completo de la retención es obligatorio.");

        if (data.AdditionalInfo.Count > MaxAdditionalFields)
            errors.Add(
                $"El comprobante tiene {data.AdditionalInfo.Count} campos adicionales; el SRI permite un máximo de {MaxAdditionalFields}."
            );

        foreach (var line in data.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.SriTaxTypeCode))
                errors.Add("Existe una línea de retención sin código SRI de tipo de impuesto.");
            if (string.IsNullOrWhiteSpace(line.RetentionCode))
                errors.Add("Existe una línea de retención sin código de retención.");
        }

        return errors;
    }

    // ── Clave de acceso (49 dígitos, dígito verificador módulo 11) ──────────
    // Mismo algoritmo exacto que InvoiceXmlBuilder/CreditNoteXmlBuilder — genérico del SRI, no
    // específico de Factura. No se extrae a un helper compartido por el mismo motivo documentado
    // en CreditNoteXmlBuilder: InvoiceXmlBuilder es infraestructura FROZEN (ADR-023) y no se
    // modifica para forzar una reutilización; para tres consumidores la duplicación puntual de
    // este algoritmo cerrado es preferible a introducir un nuevo punto de acoplamiento.
    private static string BuildAccessKeyDigits(RetentionElectronicDocumentData data)
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

    // ── Construcción del árbol XML ───────────────────────────────────────────
    private static XDocument BuildXDocument(RetentionElectronicDocumentData data, string accessKey)
    {
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

        var infoCompRetencion = new XElement(
            "infoCompRetencion",
            new XElement(
                "fechaEmision",
                data.Emission.IssueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            ),
            new XElement("dirEstablecimiento", data.Emission.EstablishmentAddress),
            string.IsNullOrWhiteSpace(data.RetentionInfo.SpecialTaxpayerNumber)
                ? null
                : new XElement("contribuyenteEspecial", data.RetentionInfo.SpecialTaxpayerNumber),
            new XElement("obligadoContabilidad", data.Issuer.IsAccountingRequired ? "SI" : "NO"),
            new XElement("tipoIdentificacionSujetoRetenido", data.SubjectWithheld.IdentificationType),
            new XElement("razonSocialSujetoRetenido", data.SubjectWithheld.LegalName),
            new XElement("identificacionSujetoRetenido", data.SubjectWithheld.IdentificationNumber),
            new XElement("periodoFiscal", data.RetentionInfo.FiscalPeriod)
        );

        var impuestos = new XElement(
            "impuestos",
            data.Lines.Select(line => BuildImpuesto(line, data.SourceDocument))
        );

        var root = new XElement(
            "comprobanteRetencion",
            new XAttribute("id", "comprobante"),
            new XAttribute("version", XmlVersionValue),
            infoTributaria,
            infoCompRetencion,
            impuestos,
            data.AdditionalInfo.Count == 0 ? null : BuildInfoAdicional(data.AdditionalInfo)
        );

        return new XDocument(new XDeclaration("1.0", XmlEncodingValue, null), root);
    }

    private static XElement BuildImpuesto(
        RetentionElectronicDocumentTaxLine line,
        RetentionElectronicDocumentSourceDocument sourceDocument
    )
    {
        var numDocSustento = NormalizeNumDocSustento(sourceDocument.Number);

        return new XElement(
            "impuesto",
            new XElement("codigo", line.SriTaxTypeCode),
            new XElement("codigoRetencion", line.RetentionCode),
            new XElement("baseImponible", FormatMoney(line.BaseAmount)),
            new XElement("porcentajeRetener", FormatPercentage(line.RetentionRate)),
            new XElement("valorRetenido", FormatMoney(line.RetainedAmount)),
            string.IsNullOrWhiteSpace(sourceDocument.DocTypeCode)
                ? null
                : new XElement("codDocSustento", sourceDocument.DocTypeCode),
            numDocSustento is null ? null : new XElement("numDocSustento", numDocSustento),
            sourceDocument.IssueDate is { } issueDate
                ? new XElement(
                    "fechaEmisionDocSustento",
                    issueDate.ToDateTime(TimeOnly.MinValue).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                )
                : null
        );
    }

    /// <summary>numDocSustento (XSD 1.0.0) exige <c>[0-9]{15}</c>. El número almacenado suele ser
    /// "EST-PTO-SECUENCIAL" (3+3+9 dígitos con guiones): al quitar los guiones da exactamente 15
    /// dígitos. Si el resultado no cumple el patrón, se omite el elemento (es opcional en este
    /// esquema) — nunca se trunca ni se rellena para forzar el largo.</summary>
    private static string? NormalizeNumDocSustento(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return null;

        var digitsOnly = new string(number.Where(char.IsDigit).ToArray());
        return digitsOnly.Length == NumDocSustentoDigits ? digitsOnly : null;
    }

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

    private static string FormatMoney(decimal value) =>
        value.ToString("F2", CultureInfo.InvariantCulture);

    private static string FormatPercentage(decimal value) =>
        value.ToString("F2", CultureInfo.InvariantCulture);

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
