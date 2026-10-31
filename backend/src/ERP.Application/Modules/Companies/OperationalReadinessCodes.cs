using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using PhysicalCodes = ERP.Application.Modules.Company.PhysicalEmission.PhysicalEmissionRequirementCodes;
using RequirementCodes = ERP.Application.Modules.ElectronicDocuments.Readiness.ElectronicEmissionRequirementCodes;

namespace ERP.Application.Modules.Companies;

/// <summary>
/// READINESS-2B-3 — códigos de ítem de Readiness compartidos por el diagnóstico de empresa
/// (<c>CompanyOperationalReadinessResolver</c>) y la habilitación preventiva de cajas
/// (<c>CashRegisterOpeningReadinessChecker</c>): ambos publican el mismo código para el mismo
/// requisito, y el frontend lo resuelve con la misma clave i18n
/// (<c>operationalReadiness.items.{code}</c>). Ver docs/architecture/company-readiness.md.
/// </summary>
public static class OperationalReadinessCodes
{
    public const string IdentityTaxId = "identity.taxId";
    public const string IdentityLegalName = "identity.legalName";
    public const string StructureMainEstablishment = "structure.mainEstablishment";
    public const string CashRegisterWithEmissionPoint = "cashRegister.withEmissionPoint";
    public const string ElectronicActivation = "electronicInvoicing.activation";
    public const string ElectronicVerificationFailed = "electronicInvoicing.verificationFailed";
    public const string PhysicalActivation = "physicalEmission.activation";
    public const string PhysicalVerificationFailed = "physicalEmission.verificationFailed";

    /// <summary>Código de fallo técnico de verificación según la modalidad del punto.</summary>
    public static string VerificationFailedFor(EmissionType emissionType) =>
        emissionType == EmissionType.Physical
            ? PhysicalVerificationFailed
            : ElectronicVerificationFailed;

    public static ReadinessActionTarget VerificationFailedTargetFor(EmissionType emissionType) =>
        emissionType == EmissionType.Physical
            ? ReadinessActionTarget.PhysicalDocumentAuthorizations
            : ReadinessActionTarget.ElectronicInvoicingSettings;

    /// <summary>
    /// Requisitos del validador electrónico sobre un dato que ya tiene un ítem base (mismo dato,
    /// misma pantalla): el ítem base adopta el veredicto del validador y las cajas lo referencian
    /// con el código base — un dato, un ítem.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> RequirementsOwnedByBaseItem =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RequirementCodes.Company] = IdentityTaxId,
            [RequirementCodes.IssuerTaxId] = IdentityTaxId,
            [RequirementCodes.IssuerLegalName] = IdentityLegalName,
            [RequirementCodes.MainEstablishment] = StructureMainEstablishment,
            [PhysicalCodes.IssuerTaxId] = IdentityTaxId,
            [PhysicalCodes.IssuerLegalName] = IdentityLegalName,
        };

    public static bool IsOwnedByBaseItem(string requirementCode) =>
        RequirementsOwnedByBaseItem.ContainsKey(requirementCode);

    /// <summary>Código de Readiness con el que se referencia un requisito del validador.</summary>
    public static string ForRequirement(string requirementCode) =>
        RequirementsOwnedByBaseItem.GetValueOrDefault(requirementCode, requirementCode);

    /// <summary>
    /// Estado fiscal de una caja según su punto activo y el resultado de su política de emisión.
    /// Physical con requisitos pendientes ⇒ Pending; con los requisitos verificables cumplidos ⇒
    /// NotVerified: la autenticidad de la autorización ante el SRI y la circunstancia excepcional
    /// que la justifica no son verificables localmente, así que nunca se presenta como autorizado.
    /// </summary>
    public static ReadinessFiscalStatus FiscalStatusFor(
        EmissionType? activePointType,
        bool meetsRequirements
    ) =>
        activePointType switch
        {
            null => ReadinessFiscalStatus.NotApplicable,
            _ when !meetsRequirements => ReadinessFiscalStatus.Pending,
            EmissionType.Electronic => ReadinessFiscalStatus.Verified,
            _ => ReadinessFiscalStatus.NotVerified,
        };
}
