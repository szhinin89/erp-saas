using ERP.Application.Common;
using ERP.Domain.Tenants.Entities;

namespace ERP.Application.Tenants.DTOs;

public record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTime CreatedAt,
    string? Ruc,
    string? ShortName,
    string? TradeName,
    string? Dinardap,
    string? LogoUrl,
    int DisplayOrder,
    int Priority,
    bool ElectronicBillingTrialEnabled,
    string? PlanCode,
    IReadOnlyList<string> EnabledModules,
    bool HasModuleRestrictions,
    // Parámetros operativos configurables por la empresa
    string Currency,
    string Language,
    string Timezone,
    string? InvoicePrefix,
    int DefaultCreditDays)
{
    public static TenantDto FromTenant(Tenant tenant) =>
        new(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.IsActive,
            tenant.CreatedAt,
            tenant.Ruc,
            tenant.ShortName,
            tenant.TradeName,
            tenant.Dinardap,
            tenant.LogoUrl,
            tenant.DisplayOrder,
            tenant.Priority,
            tenant.ElectronicBillingTrialEnabled,
            tenant.PlanCode,
            TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant),
            !string.IsNullOrWhiteSpace(tenant.EnabledModulesJson),
            tenant.Currency,
            tenant.Language,
            tenant.Timezone,
            tenant.InvoicePrefix,
            tenant.DefaultCreditDays);
}
