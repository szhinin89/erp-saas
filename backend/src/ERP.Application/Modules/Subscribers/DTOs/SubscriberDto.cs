using ERP.Application.Common;
using ERP.Domain.Subscribers.Entities;

namespace ERP.Application.Subscribers.DTOs;

public record SubscriberDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTime CreatedAt,
    int DisplayOrder,
    int Priority,
    string? PlanCode,
    IReadOnlyList<string> EnabledModules,
    bool HasModuleRestrictions,
    string PreferredLanguage)
{
    public static SubscriberDto FromSubscriber(Subscriber tenant, IReadOnlyCollection<string>? enabledModules = null) =>
        new(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.IsActive,
            tenant.CreatedAt,
            tenant.DisplayOrder,
            tenant.Priority,
            tenant.PlanCode,
            ToModuleList(enabledModules ?? Array.Empty<string>()),
            SubscriberSubscriptionCatalog.HasModuleRestrictionsFromModules(
                enabledModules ?? Array.Empty<string>()),
            tenant.PreferredLanguage);

    private static IReadOnlyList<string> ToModuleList(IReadOnlyCollection<string> modules) =>
        modules is IReadOnlyList<string> list ? list : modules.ToList();
}
