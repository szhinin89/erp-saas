using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — definitions para OrgSettingKeys.Payables.
/// AllowSupplierPaymentWithoutPayable está conectada (RegisterSupplierPaymentCommandHandler, y
/// GET /api/v1/supplier-payments/policy para el formulario).
/// </summary>
public static class PayablesConfigurationDefinitions
{
    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return new ConfigurationDefinition
        {
            Key = OrgSettingKeys.Payables.AllowSupplierPaymentWithoutPayable,
            Module = "Payables",
            DataType = ConfigurationDataType.Bool,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = "false",
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = true,
            Validator = value => bool.TryParse(value, out _),
        };
    }
}
