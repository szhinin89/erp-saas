using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
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
        };

    public static bool IsOwnedByBaseItem(string requirementCode) =>
        RequirementsOwnedByBaseItem.ContainsKey(requirementCode);

    /// <summary>Código de Readiness con el que se referencia un requisito del validador.</summary>
    public static string ForRequirement(string requirementCode) =>
        RequirementsOwnedByBaseItem.GetValueOrDefault(requirementCode, requirementCode);

    /// <summary>
    /// Estado fiscal de una caja según su punto activo: Physical ⇒ NotVerified (brechas F-1..F-5,
    /// sin política física propietaria; nunca se presenta como autorizado).
    /// </summary>
    public static ReadinessFiscalStatus FiscalStatusFor(EmissionType? activePointType, bool canIssue) =>
        activePointType switch
        {
            null => ReadinessFiscalStatus.NotApplicable,
            EmissionType.Electronic => canIssue
                ? ReadinessFiscalStatus.Verified
                : ReadinessFiscalStatus.Pending,
            _ => ReadinessFiscalStatus.NotVerified,
        };
}
