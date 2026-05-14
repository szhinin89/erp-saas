using MediatR;
using ERP.Application.Common;
using ERP.Application.Tenants.DTOs;

namespace ERP.Application.Tenants.UseCases.UpdateTenantSubscription;

public sealed record UpdateTenantSubscriptionCommand(
    Guid TenantId,
    string? PlanCode,
    IReadOnlyList<string>? EnabledModules
) : IRequest<Result<TenantDto>>;
