namespace ERP.Application.Tenants.UseCases.UpdateTenantSubscription;

public sealed record UpdateTenantSubscriptionCommand(
    Guid TenantId,
    string? PlanCode,
    IReadOnlyList<string>? EnabledModules
);
