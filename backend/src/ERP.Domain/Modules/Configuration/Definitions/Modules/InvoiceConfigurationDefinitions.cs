using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>Definitions para OrgSettingKeys.Invoice — ver docs/architecture/configuration-engine-target-architecture.md Fase 3.</summary>
public static class InvoiceConfigurationDefinitions
{
    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return new ConfigurationDefinition
        {
            Key = OrgSettingKeys.Invoice.DefaultDocTypeCode,
            Module = "Invoice",
            DataType = ConfigurationDataType.String,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = true,
            DeveloperNotes = "Fallback: SriSettings.FallbackDocTypeCode. Consumido por GetSalesInvoiceDefaultsQueryHandler.",
        };

        yield return new ConfigurationDefinition
        {
            Key = OrgSettingKeys.Invoice.DefaultPaymentMethodCode,
            Module = "Invoice",
            DataType = ConfigurationDataType.String,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = true,
            DeveloperNotes = "Fallback: SriSettings.FallbackSriPaymentMethodCode.",
        };

        yield return new ConfigurationDefinition
        {
            Key = OrgSettingKeys.Invoice.DefaultPaymentTermId,
            Module = "Invoice",
            DataType = ConfigurationDataType.Guid,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            FallbackStrategy = ConfigurationFallbackStrategy.RequireManualSelection,
            RequiresAudit = true,
            Validator = value => Guid.TryParse(value, out _),
        };

        // CONFIG-FOUNDATION-P0-01: propietario Sucursal — resuelto server-side (Branch OrgSetting
        // → Warehouse.IsMain → null). Único scope permitido es Branch (no Company): moverlo a
        // Company violaría el Principio 14 de la arquitectura ("si una regla pertenece a Branch...
        // no debe vivir en Company").
        yield return new ConfigurationDefinition
        {
            Key = OrgSettingKeys.Invoice.DefaultWarehouseId,
            Module = "Invoice",
            DataType = ConfigurationDataType.Guid,
            AllowedScopes = [OrgScope.Branch],
            DefaultScope = OrgScope.Branch,
            FallbackStrategy = ConfigurationFallbackStrategy.RequireManualSelection,
            RequiresAudit = true,
            RequiresSnapshot = false,
            Validator = value => Guid.TryParse(value, out _),
            DeveloperNotes = "Validado además en lectura contra Warehouse.BranchId/IsActive por GetSalesInvoiceDefaultsQueryHandler (fail-closed si corrupto/cruzado).",
        };
    }
}
