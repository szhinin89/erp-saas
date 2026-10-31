using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.PhysicalEmission;

/// <summary>
/// PHYSICAL-FISCAL-F1..F5 — qué comprobante físico se va a emitir y desde qué punto.
/// <see cref="DocTypeCode"/> es el código SRI del comprobante (01 en el flujo de Ventas).
/// </summary>
public sealed record PhysicalEmissionRequirementsRequest(
    Guid TenantId,
    Guid CompanyId,
    Guid EmissionPointId,
    string DocTypeCode
);

/// <summary>
/// Política fiscal propietaria de la emisión física (módulo Company: régimen, punto de emisión,
/// autorización de impresión y secuencia). Comprueba con datos locales, antes de capturar
/// secuencial o producir efectos, lo que la normativa exige y el ERP puede verificar: RUC oficial y
/// razón social del emisor, punto de emisión activo, régimen que permite el tipo en físico, tipo
/// habilitado, autorización de impresión vigente y siguiente secuencial dentro del rango
/// autorizado. Nunca contacta al SRI: la autenticidad de la autorización y la circunstancia
/// excepcional que la justifica no se verifican (ver company-readiness.md).
/// Devuelve el mismo resultado que la emisión electrónica, con <c>Modality = Physical</c>.
/// </summary>
public interface IPhysicalEmissionRequirementsValidator
{
    Task<ElectronicEmissionRequirementsResult> ValidateAsync(
        PhysicalEmissionRequirementsRequest request,
        CancellationToken ct = default
    );

    /// <summary>
    /// Rangos de las autorizaciones activas y en vigor hoy (día de empresa) del punto y tipo. Los
    /// recibe <see cref="IDocumentSequenceRepository"/> al capturar: la comprobación definitiva del
    /// rango ocurre bajo el lock de la secuencia, nunca fuera de ella.
    /// </summary>
    Task<IReadOnlyList<AuthorizedSequentialRange>> GetAuthorizedSequentialRangesAsync(
        PhysicalEmissionRequirementsRequest request,
        CancellationToken ct = default
    );
}

/// <summary>Códigos estables de requisitos de emisión física y su único mensaje en español.</summary>
public static class PhysicalEmissionRequirementCodes
{
    public const string IssuerTaxId = "physicalEmission.issuerTaxId";
    public const string IssuerLegalName = "physicalEmission.issuerLegalName";
    public const string EmissionPoint = "physicalEmission.emissionPoint";
    public const string TaxRegime = "physicalEmission.taxRegime";
    public const string DocTypeNotPermitted = "physicalEmission.docTypeNotPermitted";
    public const string DocType = "physicalEmission.docType";
    public const string Authorization = "physicalEmission.authorization";
    public const string AuthorizationExpired = "physicalEmission.authorizationExpired";
    public const string SequentialOutOfRange = "physicalEmission.sequentialOutOfRange";

    private static readonly IReadOnlyDictionary<string, string> Messages = new Dictionary<
        string,
        string
    >(StringComparer.Ordinal)
    {
        [IssuerTaxId] = "La empresa debe tener su RUC oficial de 13 dígitos (no provisional).",
        [IssuerLegalName] = "La razón social de la empresa es obligatoria.",
        [EmissionPoint] =
            "El punto de emisión no existe en la empresa o está desactivado.",
        [TaxRegime] = "La empresa no tiene un régimen tributario configurado.",
        [DocTypeNotPermitted] =
            "El régimen tributario de la empresa no permite emitir este tipo de comprobante en forma física.",
        [DocType] =
            "El tipo de comprobante no está habilitado en el ERP para emisión física.",
        [Authorization] =
            "No hay una autorización de impresión del SRI vigente para este punto y tipo de comprobante.",
        [AuthorizationExpired] =
            "La autorización de impresión del SRI que cubre el siguiente secuencial está caducada.",
        [SequentialOutOfRange] =
            "El siguiente secuencial está fuera del rango autorizado por el SRI para este punto.",
    };

    public static bool IsPhysical(string code) =>
        code.StartsWith("physicalEmission.", StringComparison.Ordinal);

    public static string MessageFor(string code) =>
        Messages.TryGetValue(code, out var message) ? message : code;

    /// <summary>Rechazo cuando la captura bajo lock encuentra el siguiente número fuera de rango.</summary>
    public static ElectronicEmissionRequirementsResult SequentialOutOfRangeResult { get; } =
        new([
            new(
                SequentialOutOfRange,
                ElectronicEmissionRequirementSeverity.Required,
                ReadinessActionTarget.PhysicalDocumentAuthorizations
            ),
        ])
        {
            Modality = EmissionType.Physical,
        };
}
