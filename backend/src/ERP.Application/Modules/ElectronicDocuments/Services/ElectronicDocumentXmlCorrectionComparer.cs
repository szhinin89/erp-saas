using System.Xml;
using System.Xml.Linq;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

public enum ElectronicDocumentXmlCorrectionOutcome
{
    /// <summary>Hay diferencias fuera de la firma y ningún dato protegido cambió.</summary>
    Corrected = 1,

    /// <summary>Fuera de la firma, el XML corregido es semánticamente idéntico al anterior.</summary>
    NoCorrection = 2,

    /// <summary>Cambió un dato de la operación comercial, de la contraparte o de la clave.</summary>
    ProtectedDataChanged = 3,

    /// <summary>Retención 07: cambió la identificación del sujeto retenido.</summary>
    SubjectIdentificationChanged = 4,

    /// <summary>Alguno de los XML no es interpretable.</summary>
    Unreadable = 5,
}

public sealed record ElectronicDocumentXmlCorrection(
    ElectronicDocumentXmlCorrectionOutcome Outcome,
    IReadOnlyList<string> Changes
)
{
    public string Summary => string.Join("; ", Changes);
}

/// <summary>
/// R12 Fase B — compara semánticamente el XML firmado del intento rechazado con el XML corregido,
/// excluyendo únicamente la firma (<c>ds:Signature</c> y todo su contenido XAdES). La comparación es
/// estructural (elementos, atributos y texto por ruta; sin espacios insignificantes ni declaraciones de
/// namespace), así que un re-firmado sin cambios nunca cuenta como corrección. Los datos protegidos
/// (identidad de la clave, fecha de emisión, contraparte y valores comerciales) deben coincidir valor
/// por valor y en el mismo orden: si cambian, la retransmisión no es la misma operación.
/// </summary>
public static class ElectronicDocumentXmlCorrectionComparer
{
    private const string XmlDsigNamespace = "http://www.w3.org/2000/09/xmldsig#";
    private const int MaxReportedChanges = 25;

    private static readonly string[] KeyFields =
    [
        "ambiente", "tipoEmision", "ruc", "claveAcceso", "codDoc", "estab", "ptoEmi", "secuencial",
        "fechaEmision",
    ];

    private static readonly Dictionary<ElectronicDocumentType, string[]> ProtectedFields =
        new Dictionary<ElectronicDocumentType, string[]>
        {
            [ElectronicDocumentType.Invoice] =
            [
                "tipoIdentificacionComprador", "identificacionComprador", "razonSocialComprador",
                "totalSinImpuestos", "totalDescuento", "importeTotal", "propina", "moneda",
                "codigoPrincipal", "cantidad", "precioTotalSinImpuesto",
            ],
            [ElectronicDocumentType.CreditNote] =
            [
                "tipoIdentificacionComprador", "identificacionComprador", "razonSocialComprador",
                "codDocModificado", "numDocModificado", "fechaEmisionDocSustento",
                "totalSinImpuestos", "valorModificacion", "moneda", "motivo",
                "codigoInterno", "cantidad", "precioTotalSinImpuesto",
            ],
            [ElectronicDocumentType.Retention] =
            [
                "razonSocialSujetoRetenido", "periodoFiscal", "codigo", "codigoRetencion",
                "baseImponible", "porcentajeRetener", "valorRetenido", "codDocSustento",
                "numDocSustento", "fechaEmisionDocSustento",
            ],
        };

    private static readonly string[] RetentionSubjectIdentification =
    [
        "tipoIdentificacionSujetoRetenido", "identificacionSujetoRetenido",
    ];

    public static ElectronicDocumentXmlCorrection Compare(
        ElectronicDocumentType documentType,
        string previousSignedXml,
        string correctedSignedXml
    )
    {
        if (!ProtectedFields.TryGetValue(documentType, out var protectedFields))
            return new(ElectronicDocumentXmlCorrectionOutcome.Unreadable, [$"Tipo de comprobante no soportado: {documentType}."]);

        XElement previous;
        XElement corrected;
        try
        {
            previous = WithoutSignature(previousSignedXml);
            corrected = WithoutSignature(correctedSignedXml);
        }
        catch (XmlException ex)
        {
            return new(ElectronicDocumentXmlCorrectionOutcome.Unreadable, [ex.Message]);
        }

        if (documentType == ElectronicDocumentType.Retention)
        {
            var subjectChanges = ProtectedChanges(previous, corrected, RetentionSubjectIdentification);
            if (subjectChanges.Count > 0)
                return new(ElectronicDocumentXmlCorrectionOutcome.SubjectIdentificationChanged, subjectChanges);
        }

        var protectedChanges = ProtectedChanges(previous, corrected, [.. KeyFields, .. protectedFields]);
        if (protectedChanges.Count > 0)
            return new(ElectronicDocumentXmlCorrectionOutcome.ProtectedDataChanged, protectedChanges);

        var before = Flatten(previous);
        var after = Flatten(corrected);
        var changes = before.Keys.Union(after.Keys)
            .Where(path => !before.TryGetValue(path, out var a) || !after.TryGetValue(path, out var b) || a != b)
            .Order(StringComparer.Ordinal)
            .Select(path => Describe(path, before.GetValueOrDefault(path), after.GetValueOrDefault(path)))
            .ToList();

        return changes.Count == 0
            ? new(ElectronicDocumentXmlCorrectionOutcome.NoCorrection, [])
            : new(ElectronicDocumentXmlCorrectionOutcome.Corrected, Limit(changes));
    }

    private static XElement WithoutSignature(string xml)
    {
        var root = XDocument.Parse(xml, LoadOptions.None).Root
            ?? throw new XmlException("El XML no tiene elemento raíz.");
        root.Descendants().Where(e => e.Name.NamespaceName == XmlDsigNamespace).ToList()
            .ForEach(e => e.Remove());
        return root;
    }

    private static List<string> ProtectedChanges(XElement previous, XElement corrected, IEnumerable<string> fields)
    {
        var changes = new List<string>();
        foreach (var field in fields)
        {
            var a = Values(previous, field);
            var b = Values(corrected, field);
            if (!a.SequenceEqual(b, StringComparer.Ordinal))
                changes.Add($"{field}: '{string.Join("|", a)}' → '{string.Join("|", b)}'");
        }
        return changes;
    }

    private static List<string> Values(XElement root, string localName) =>
        root.DescendantsAndSelf()
            .Where(e => e.Name.LocalName == localName && !e.HasElements)
            .Select(e => e.Value.Trim())
            .ToList();

    /// <summary>Ruta por nombre local con índice entre hermanos homónimos → valor (texto u atributo).</summary>
    private static Dictionary<string, string> Flatten(XElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        Visit(root, "/" + root.Name.LocalName, result);
        return result;
    }

    private static void Visit(XElement element, string path, Dictionary<string, string> result)
    {
        foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
            result[$"{path}/@{attribute.Name.LocalName}"] = attribute.Value;

        if (!element.HasElements)
        {
            result[path] = element.Value.Trim();
            return;
        }

        foreach (var group in element.Elements().GroupBy(e => e.Name.LocalName))
        {
            var index = 0;
            foreach (var child in group)
                Visit(child, $"{path}/{group.Key}[{++index}]", result);
        }
    }

    private static string Describe(string path, string? before, string? after) =>
        (before, after) switch
        {
            (null, _) => $"{path} agregado",
            (_, null) => $"{path} eliminado",
            _ => $"{path}: '{before}' → '{after}'",
        };

    private static List<string> Limit(List<string> changes) =>
        changes.Count <= MaxReportedChanges
            ? changes
            : [.. changes.Take(MaxReportedChanges), $"… y {changes.Count - MaxReportedChanges} diferencias más"];
}
