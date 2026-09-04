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
/// Construye el XML de Factura (ficha técnica SRI, esquema "factura" v1.1.0) a partir,
/// exclusivamente, de <see cref="ElectronicDocumentData"/>. Nunca recibe ni referencia
/// SalesInvoice, EF Core, repositorios ni ningún módulo de negocio.
///
/// Ningún dato funcional del SRI está codificado aquí: el código de tipo de comprobante
/// (<c>codDoc</c>) viene ya resuelto y validado contra el catálogo <c>sri_doc_types</c> en
/// <see cref="ElectronicDocumentData.Emission"/> (lo resolvió el proveedor, que sí tiene
/// acceso legítimo a BD), y el código de categoría tributaria ("2"=IVA, "3"=ICE) se resuelve
/// vía <see cref="ISriTaxCategoryCodeResolver"/> — un punto de extensión inyectado, no un
/// switch/diccionario hardcodeado en esta clase.
/// </summary>
public sealed class InvoiceXmlBuilder : IElectronicDocumentXmlBuilder
{
    private const string XmlVersionValue = "1.1.0";
    private const string XmlEncodingValue = "UTF-8";

    /// <summary>Ficha técnica SRI: máximo de nodos &lt;campoAdicional&gt; permitidos dentro de &lt;infoAdicional&gt;.</summary>
    private const int MaxAdditionalFields = 15;

    private readonly ISriTaxCategoryCodeResolver _taxCategoryCodeResolver;

    public InvoiceXmlBuilder(ISriTaxCategoryCodeResolver taxCategoryCodeResolver)
    {
        _taxCategoryCodeResolver = taxCategoryCodeResolver;
    }

    public ElectronicDocumentType DocumentType => ElectronicDocumentType.Invoice;

    public Result<ElectronicDocumentXml> Build(ElectronicDocumentData data)
    {
        var errors = Validate(data);
        if (errors.Count > 0)
            return Result<ElectronicDocumentXml>.ValidationFailure(string.Join(" ", errors));

        try
        {
            var accessKey = AccessKey.Create(BuildAccessKeyDigits(data));
            var xdoc = BuildXDocument(data, accessKey.Value);

            // XML-01 (auditoría SRI, Fase 2): StringWriter.Encoding es Encoding.Unicode
            // ("utf-16") por defecto — XDocument.Save toma el encoding de la declaración final
            // de esa propiedad, NO del valor pasado a XDeclaration (XmlEncodingValue = "UTF-8"
            // más abajo era efectivamente ignorado). El resultado era un XML cuya declaración
            // decía <?xml version="1.0" encoding="utf-16"?> mientras que
            // ElectronicDocumentXmlStorageService lo persistía como bytes UTF-8
            // (Encoding.UTF8.GetBytes) — una declaración inconsistente con los bytes reales del
            // archivo. Utf8StringWriter fuerza que XDocument.Save emita "utf-8", consistente con
            // cómo se persiste y con XmlEncodingValue.
            using var writer = new Utf8StringWriter();
            xdoc.Save(writer, SaveOptions.DisableFormatting);
            var xml = writer.ToString();

            // Verificación explícita de buena formación — XElement ya lo garantiza por
            // construcción, pero se revalida por contrato (punto 6 de la fase) y como red de
            // seguridad ante caracteres de control inválidos que la API de XElement no rechaza.
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
    private List<string> Validate(ElectronicDocumentData data)
    {
        var errors = new List<string>();

        if (data.Totals is null)
            errors.Add("Una factura debe tener totales.");
        if (data.Details.Count == 0)
            errors.Add("Una factura debe tener al menos un detalle.");
        if (data.Payments.Count == 0)
            errors.Add("Una factura debe tener al menos una forma de pago.");
        if (data.TaxSummary.Count == 0)
            errors.Add("Una factura debe tener al menos un impuesto.");

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
            if (string.IsNullOrWhiteSpace(line.Code))
                errors.Add($"La línea '{line.Description}' no tiene código de producto.");
            if (string.IsNullOrWhiteSpace(line.Description))
                errors.Add("Existe una línea de detalle sin descripción.");
            if (line.Taxes.Count == 0)
                errors.Add($"La línea '{line.Description}' no tiene impuestos.");
        }

        // Todo TaxCode presente (a nivel de línea o de resumen) debe poder resolverse a un
        // código SRI real antes de construir el XML — nunca se hardcodea ni se asume.
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

    /// <summary>
    /// "Código numérico" de 8 dígitos exigido por el SRI para diferenciar comprobantes con
    /// la misma serie/secuencial en reintentos. No es un valor de negocio ni una constante:
    /// se deriva determinísticamente de los campos identificadores del propio documento
    /// (mismo documento → mismo código, sin volver a generar aleatoriedad en cada intento).
    /// </summary>
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
    private XDocument BuildXDocument(ElectronicDocumentData data, string accessKey)
    {
        var totals = data.Totals!;

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
            // Ubicación oficial (ficha técnica, Anexo 22): "Entre la etiqueta <agenteRetencion>
            // y </infoTributaria>" — no dentro de <infoFactura>. Como el proyecto todavía no
            // modela <agenteRetencion>, se ubica como último hijo de <infoTributaria>.
            data.Issuer.TaxRegime
                is null
                ? null
                : new XElement("contribuyenteRimpe", data.Issuer.TaxRegime)
        );

        var infoFactura = new XElement(
            "infoFactura",
            new XElement(
                "fechaEmision",
                data.Emission.IssueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            ),
            new XElement("dirEstablecimiento", data.Emission.EstablishmentAddress),
            new XElement("obligadoContabilidad", data.Issuer.IsAccountingRequired ? "SI" : "NO"),
            new XElement("tipoIdentificacionComprador", data.Counterparty.IdentificationType),
            new XElement("razonSocialComprador", data.Counterparty.LegalName),
            new XElement("identificacionComprador", data.Counterparty.IdentificationNumber),
            data.Counterparty.Address is null
                ? null
                : new XElement("direccionComprador", data.Counterparty.Address),
            new XElement("totalSinImpuestos", FormatMoney(totals.Subtotal)),
            new XElement("totalDescuento", FormatMoney(totals.TotalDiscount)),
            new XElement("totalConImpuestos", data.TaxSummary.Select(BuildTotalImpuesto)),
            new XElement("propina", FormatMoney(0m)),
            new XElement("importeTotal", FormatMoney(totals.GrandTotal)),
            new XElement("moneda", totals.CurrencyCode),
            new XElement("pagos", data.Payments.Select(BuildPago))
        );

        var detalles = new XElement("detalles", data.Details.Select(BuildDetalle));

        var root = new XElement(
            "factura",
            new XAttribute("id", "comprobante"),
            new XAttribute("version", XmlVersionValue),
            infoTributaria,
            infoFactura,
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

    private static XElement BuildPago(ElectronicDocumentPayment payment)
    {
        var pago = new XElement(
            "pago",
            new XElement("formaPago", payment.PaymentMethodCode),
            new XElement("total", FormatMoney(payment.Amount))
        );

        if (payment.Term is { } term)
            pago.Add(new XElement("plazo", term));
        if (!string.IsNullOrWhiteSpace(payment.TimeUnit))
            pago.Add(new XElement("unidadTiempo", payment.TimeUnit));

        return pago;
    }

    private XElement BuildDetalle(ElectronicDocumentDetailLine line) =>
        new(
            "detalle",
            new XElement("codigoPrincipal", line.Code),
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

    /// <summary>
    /// Resuelve vía <see cref="ISriTaxCategoryCodeResolver"/> — nunca un switch/diccionario
    /// hardcodeado aquí. <see cref="Validate"/> ya garantizó que todo TaxCode presente es
    /// resoluble, así que un null en este punto sería un error de programación, no de datos.
    /// </summary>
    private string ResolveSriTaxCode(string taxCode) =>
        _taxCategoryCodeResolver.Resolve(taxCode)
        ?? throw new InvalidOperationException(
            $"Invariante violada: '{taxCode}' debía estar validado como resoluble antes de construir el XML."
        );

    private static string FormatMoney(decimal value) =>
        value.ToString("F2", CultureInfo.InvariantCulture);

    private static string FormatQuantity(decimal value) =>
        value.ToString("F6", CultureInfo.InvariantCulture);

    /// <summary>
    /// XML-01 (auditoría SRI, Fase 2): <see cref="StringWriter"/> declara
    /// <see cref="Encoding.Unicode"/> ("utf-16") por defecto — <see cref="XDocument.Save(TextWriter, SaveOptions)"/>
    /// usa esa propiedad para la declaración <c>encoding</c> del XML final, no el valor pasado a
    /// <see cref="XDeclaration"/>. Esta subclase mínima solo cambia qué encoding se declara;
    /// sigue siendo un <see cref="TextWriter"/> en memoria (<see cref="StringWriter.ToString"/>
    /// devuelve el mismo string en ambos casos) — no cambia cómo se persisten los bytes, que ya
    /// eran UTF-8 en <c>ElectronicDocumentXmlStorageService</c>.
    /// </summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
