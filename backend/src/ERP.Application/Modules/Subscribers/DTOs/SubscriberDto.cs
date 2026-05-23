using ERP.Application.Common;
using ERP.Domain.Subscribers.Entities;

namespace ERP.Application.Subscribers.DTOs;

public record SubscriberDto(
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
    public static SubscriberDto FromTenant(Subscriber tenant, IReadOnlyCollection<string>? enabledModules = null) =>
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
            ToModuleList(enabledModules ?? Array.Empty<string>()),
            SubscriberSubscriptionCatalog.HasModuleRestrictionsFromModules(
                enabledModules ?? Array.Empty<string>()),
            tenant.Currency,
            tenant.Language,
            tenant.Timezone,
            tenant.InvoicePrefix,
            tenant.DefaultCreditDays);

    private static IReadOnlyList<string> ToModuleList(IReadOnlyCollection<string> modules) =>
        modules is IReadOnlyList<string> list ? list : modules.ToList();
}
