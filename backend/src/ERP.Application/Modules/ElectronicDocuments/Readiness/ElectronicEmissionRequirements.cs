using ERP.Application.Common;
using ERP.Application.Modules.Company.PhysicalEmission;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.ElectronicDocuments.Readiness;

/// <summary>
/// READINESS-PREVENTIVE-1B — qué se va a emitir y desde dónde. <see cref="EmissionPointId"/> es el
/// punto de emisión efectivo del comprobante (su establecimiento aporta la dirección
/// <c>dirEstablecimiento</c>); <see cref="DocTypeCode"/> es el código SRI del comprobante
/// (01/04/07), validado contra el catálogo <c>sri_doc_types</c>.
/// </summary>
public sealed record ElectronicEmissionRequirementsRequest(
    Guid TenantId,
    Guid CompanyId,
    Guid EmissionPointId,
    string DocTypeCode
);

/// <summary>
/// Required: sin él el comprobante electrónico no puede generarse/firmarse — bloquea iniciar la
/// emisión. Warning: no bloquea, se informa (p. ej. certificado próximo a vencer).
/// </summary>
public enum ElectronicEmissionRequirementSeverity
{
    Required,
    Warning,
}

/// <summary>
/// Un requisito incumplido. Igual que <see cref="ReadinessItem"/>, no transporta texto de UI:
/// <see cref="Code"/> es el identificador estable (ver <see cref="ElectronicEmissionRequirementCodes"/>)
/// y <see cref="ActionTarget"/> la pantalla owner donde se corrige — el mismo enum que Readiness,
/// para que la Fase 2 consuma exactamente estos códigos y destinos.
/// </summary>
public sealed record ElectronicEmissionRequirementIssue(
    string Code,
    ElectronicEmissionRequirementSeverity Severity,
    ReadinessActionTarget? ActionTarget
);

public sealed record ElectronicEmissionRequirementsResult(
    IReadOnlyList<ElectronicEmissionRequirementIssue> Issues
)
{
    /// <summary>Resultado de un flujo sin emisión electrónica (p. ej. factura física).</summary>
    public static ElectronicEmissionRequirementsResult NotApplicable { get; } = new([]);

    /// <summary>
    /// PHYSICAL-FISCAL-F1..F5 — modalidad cuyos requisitos se evaluaron. Physical: los produce
    /// <see cref="IPhysicalEmissionRequirementsValidator"/> y el rechazo usa su código y mensajes.
    /// </summary>
    public EmissionType Modality { get; init; } = EmissionType.Electronic;

    public bool CanIssue =>
        Issues.All(i => i.Severity != ElectronicEmissionRequirementSeverity.Required);

    /// <summary>
    /// <c>null</c> si se puede emitir; si no, el ValidationFailure único de todos los flujos
    /// (01/04/07) con el código API <see cref="ApiResponseCodes.ElectronicDocuments.EmissionRequirementsMissing"/>
    /// y, en el detalle, cada requisito como <c>[código] mensaje</c>.
    /// </summary>
    public Result<T>? ToFailure<T>()
    {
        if (CanIssue)
            return null;

        var required = Issues.Where(i =>
            i.Severity == ElectronicEmissionRequirementSeverity.Required
        );
        if (Modality == EmissionType.Physical)
            return Result<T>.ValidationFailure(
                "No se puede emitir el comprobante físico: la empresa no cumple los requisitos "
                    + "fiscales de emisión física. "
                    + string.Join(
                        " ",
                        required.Select(i =>
                            $"[{i.Code}] {PhysicalEmissionRequirementCodes.MessageFor(i.Code)}"
                        )
                    ),
                ApiResponseCodes.PhysicalEmission.RequirementsMissing
            );

        var details = required.Select(i =>
            $"[{i.Code}] {ElectronicEmissionRequirementCodes.MessageFor(i.Code)}"
        );
        return Result<T>.ValidationFailure(
            "No se puede emitir el comprobante electrónico: la configuración fiscal de la empresa "
                + "está incompleta. "
                + string.Join(" ", details),
            ApiResponseCodes.ElectronicDocuments.EmissionRequirementsMissing
        );
    }
}

/// <summary>
/// Gate fiscal preventivo único para 01/04/07: comprueba, con datos locales y antes de capturar
/// secuencial o generar efectos comerciales, los requisitos del emisor que el XML exige. Nunca
/// contacta al SRI (la disponibilidad del webservice no es un requisito local). Las validaciones
/// de los builders XML permanecen como defensa final.
/// </summary>
public interface IElectronicEmissionRequirementsValidator
{
    Task<ElectronicEmissionRequirementsResult> ValidateAsync(
        ElectronicEmissionRequirementsRequest request,
        CancellationToken ct = default
    );
}

/// <summary>Códigos estables de requisitos de emisión y su único mensaje en español.</summary>
public static class ElectronicEmissionRequirementCodes
{
    public const string Company = "electronicEmission.company";
    public const string IssuerTaxId = "electronicEmission.issuerTaxId";
    public const string IssuerLegalName = "electronicEmission.issuerLegalName";
    public const string MainEstablishment = "electronicEmission.mainEstablishment";
    public const string MatrixAddress = "electronicEmission.matrixAddress";
    public const string EmissionPoint = "electronicEmission.emissionPoint";
    public const string EstablishmentAddress = "electronicEmission.establishmentAddress";
    public const string SriSettings = "electronicEmission.sriSettings";
    public const string SriEnvironment = "electronicEmission.sriEnvironment";
    public const string SriWsdlUrl = "electronicEmission.sriWsdlUrl";
    public const string Certificate = "electronicEmission.certificate";
    public const string CertificateUnusable = "electronicEmission.certificateUnusable";
    public const string CertificateExpired = "electronicEmission.certificateExpired";
    public const string CertificateExpiring = "electronicEmission.certificateExpiring";
    public const string DocType = "electronicEmission.docType";

    private static readonly IReadOnlyDictionary<string, string> Messages = new Dictionary<
        string,
        string
    >(StringComparer.Ordinal)
    {
        [Company] = "La empresa emisora no existe.",
        [IssuerTaxId] = "El RUC de la empresa debe tener 13 dígitos.",
        [IssuerLegalName] = "La razón social de la empresa es obligatoria.",
        [MainEstablishment] = "La empresa no tiene un establecimiento matriz configurado.",
        [MatrixAddress] = "La dirección del establecimiento matriz es obligatoria.",
        [EmissionPoint] = "El punto de emisión no existe o no pertenece a la empresa.",
        [EstablishmentAddress] =
            "La dirección del establecimiento del punto de emisión es obligatoria.",
        [SriSettings] = "La empresa no tiene configuración de facturación electrónica SRI.",
        [SriEnvironment] = "El ambiente SRI configurado no es válido.",
        [SriWsdlUrl] = "La URL del webservice SRI no es válida.",
        [Certificate] = "No hay un certificado de firma electrónica cargado.",
        [CertificateUnusable] =
            "El certificado de firma electrónica no se puede usar (archivo inaccesible o contraseña incorrecta).",
        [CertificateExpired] = "El certificado de firma electrónica está vencido.",
        [CertificateExpiring] = "El certificado de firma electrónica vence pronto.",
        [DocType] =
            "El tipo de comprobante no está activo para emisión electrónica en el catálogo SRI.",
    };

    public static string MessageFor(string code) =>
        Messages.TryGetValue(code, out var message) ? message : code;
}
