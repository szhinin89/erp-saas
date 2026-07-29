using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using CustomerRoleConfig = ERP.Domain.MasterData.ValueObjects.CustomerRoleConfig;

namespace ERP.Application.MasterData.DTOs;

// ── Resumen de identidad — usado en listas y búsquedas ───────────────────────

/// <summary>
/// DTO para listas y resultados de búsqueda.
/// No incluye roles (evita N+1). El cliente usa el filtro roles[] para acotar resultados.
/// </summary>
public sealed record BusinessPartnerSummaryDto(
    Guid Id,
    string IdentificationType,
    string IdentificationNumber,
    string LegalName,
    string? TradeName,
    string PersonType,
    string? CountryCode,
    bool IsActive,
    DateTime CreatedAt)
{
    public static BusinessPartnerSummaryDto From(BusinessPartner bp) => new(
        bp.Id,
        bp.Identification.Type,
        bp.Identification.Number,
        bp.Name.LegalName,
        bp.Name.TradeName,
        bp.PersonType.ToString(),
        bp.CountryCode,
        bp.IsActive,
        bp.CreatedAt);
}

// ── line completo — incluye roles con sus configs ─────────────────────────

public sealed record BusinessPartnerDetailDto(
    Guid Id,
    string IdentificationType,
    string IdentificationNumber,
    string LegalName,
    string? TradeName,
    string PersonType,
    string? CountryCode,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<BusinessPartnerRoleDto> Roles)
{
    public static BusinessPartnerDetailDto From(
        BusinessPartner bp,
        IReadOnlyList<BusinessPartnerRoleDto> roles) => new(
        bp.Id,
        bp.Identification.Type,
        bp.Identification.Number,
        bp.Name.LegalName,
        bp.Name.TradeName,
        bp.PersonType.ToString(),
        bp.CountryCode,
        bp.IsActive,
        bp.CreatedAt,
        roles);
}

// ── Rol del BusinessPartner ───────────────────────────────────────────────────

public sealed record BusinessPartnerRoleDto(
    Guid Id,
    string RoleType,
    string RoleLabel,
    bool IsActive,
    string? Notes,
    DateTime AssignedAt,
    DateTime? RevokedAt,
    SupplierRoleConfigDto? SupplierConfig,
    CarrierRoleConfigDto? CarrierConfig,
    CustomerRoleConfigDto? CustomerConfig,
    SupplierClassificationConfigDto? ClassificationConfig)
{
    public static BusinessPartnerRoleDto From(BusinessPartnerRole role) => new(
        role.Id,
        role.RoleType.ToString(),
        BpRoleDtoHelpers.RoleTypeLabel(role.RoleType),
        role.IsActive,
        role.Notes,
        role.AssignedAt,
        role.RevokedAt,
        role.SupplierConfig is not null ? SupplierRoleConfigDto.From(role.SupplierConfig) : null,
        role.CarrierConfig is not null ? CarrierRoleConfigDto.From(role.CarrierConfig) : null,
        role.CustomerConfig is not null ? CustomerRoleConfigDto.From(role.CustomerConfig) : null,
        role.ClassificationConfig is not null ? SupplierClassificationConfigDto.From(role.ClassificationConfig) : null);

}

public sealed record SupplierRoleConfigDto(
    string? DefaultTaxSupportCode,
    string? DefaultRetentionVatCode,
    string? DefaultRetentionIncomeCode,
    string? DefaultPaymentMethodCode,
    string? RefundProviderTypeCode,
    bool IsRetentionExempt,
    Guid PaymentTermId)
{
    public static SupplierRoleConfigDto From(SupplierRoleConfig c) => new(
        c.DefaultTaxSupportCode,
        c.DefaultRetentionVatCode,
        c.DefaultRetentionIncomeCode,
        c.DefaultPaymentMethodCode,
        c.RefundProviderTypeCode,
        c.IsRetentionExempt,
        c.PaymentTermId);
}

public sealed record SupplierClassificationConfigDto(
    string? SupplierCategory,
    string? SupplierType,
    string? SupplierRisk,
    string? SupplierRating,
    string? PrimaryGoodType,
    string? SupplierSegment,
    string? PaymentMethodPreference)
{
    public static SupplierClassificationConfigDto From(SupplierClassificationConfig c) => new(
        c.SupplierCategory,
        c.SupplierType,
        c.SupplierRisk,
        c.SupplierRating,
        c.PrimaryGoodType,
        c.SupplierSegment,
        c.PaymentMethodPreference);
}

public sealed record CarrierRoleConfigDto(
    string? TransportAuthorizationNumber,
    decimal? VehicleCapacityTons)
{
    public static CarrierRoleConfigDto From(CarrierRoleConfig c) => new(
        c.TransportAuthorizationNumber,
        c.VehicleCapacityTons);
}

public sealed record CustomerRoleConfigDto(
    string? CustomerCategory,
    string? CustomerSegment,
    string? SalesZone,
    string? CreditRating,
    string? LoyaltyTier,
    string? PreferredInvoiceFormat,
    string? CustomerClassification)
{
    public static CustomerRoleConfigDto From(CustomerRoleConfig c) => new(
        c.CustomerCategory,
        c.CustomerSegment,
        c.SalesZone,
        c.CreditRating,
        c.LoyaltyTier,
        c.PreferredInvoiceFormat,
        c.CustomerClassification);
}

// ── Helper fuera del record (evita conflicto propiedad RoleType string vs enum RoleType) ──

internal static class BpRoleDtoHelpers
{
    internal static string RoleTypeLabel(RoleType rt) => rt switch
    {
        RoleType.Customer => "Cliente",
        RoleType.Supplier => "Proveedor",
        RoleType.Employee => "Empleado",
        RoleType.Carrier => "Transportista",
        RoleType.Broker => "Intermediario",
        RoleType.Agent => "Agente",
        RoleType.Distributor => "Distribuidor",
        RoleType.Contractor => "Contratista",
        _ => rt.ToString(),
    };
}
