using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.ValueObjects;

namespace ERP.Application.MasterData.Services;

/// <summary>
/// ZH-API-THIN-BP-ROLES-01 — único punto que traduce la config de rol recibida como datos
/// primitivos (<see cref="SupplierRoleConfigDto"/>, <see cref="CarrierRoleConfigDto"/>,
/// <see cref="CustomerRoleConfigDto"/>) a sus value objects de Domain. Antes lo hacía
/// BusinessPartnerRolesController. Domain sigue siendo la última barrera de invariantes: el
/// ArgumentException del VO se convierte en <see cref="RoleConfigBuild{T}.Error"/>, con su mensaje
/// intacto, y los handlers lo devuelven con <see cref="InvalidConfigCode"/>.
/// Los validators usan la config ya construida: validan los valores normalizados por Domain
/// (trim, vacío → null), igual que cuando el controller construía el VO.
/// </summary>
public static class RoleConfigFactory
{
    /// <summary>
    /// Contrato público vigente: una config que viola un invariante de Domain responde 400
    /// BadRequest (no 422) con el mensaje del ArgumentException, como hacía el controller.
    /// </summary>
    public const string InvalidConfigCode = ApiResponseCodes.Common.BadRequest;

    public static RoleConfigBuild<SupplierRoleConfig> Build(SupplierRoleConfigDto input) =>
        Try(() =>
            SupplierRoleConfig.Create(
                input.DefaultTaxSupportCode,
                defaultPaymentMethodCode: input.DefaultPaymentMethodCode,
                refundProviderTypeCode: input.RefundProviderTypeCode,
                isRetentionExempt: input.IsRetentionExempt,
                isRequiredToKeepAccounting: input.IsRequiredToKeepAccounting
            )
        );

    public static RoleConfigBuild<CarrierRoleConfig> Build(CarrierRoleConfigDto input) =>
        Try(() =>
            CarrierRoleConfig.Create(input.TransportAuthorizationNumber, input.VehicleCapacityTons)
        );

    public static RoleConfigBuild<CustomerRoleConfig> Build(CustomerRoleConfigDto input) =>
        Try(() =>
            CustomerRoleConfig.Create(
                input.CustomerCategory,
                input.CustomerSegment,
                input.SalesZone,
                input.CreditRating,
                input.LoyaltyTier,
                input.PreferredInvoiceFormat,
                input.CustomerClassification
            )
        );

    private static RoleConfigBuild<T> Try<T>(Func<T> create)
        where T : class
    {
        try
        {
            return new RoleConfigBuild<T>(create(), null);
        }
        catch (ArgumentException ex)
        {
            return new RoleConfigBuild<T>(null, ex.Message);
        }
    }
}

/// <summary>Resultado de construir una config de rol: el VO, o el mensaje del invariante violado.</summary>
public readonly record struct RoleConfigBuild<T>(T? Config, string? Error)
    where T : class
{
    public bool IsValid => Error is null;
}
