using ERP.Application.Billing.Governance;
using ERP.Application.Common;
using ERP.Domain.Billing.Exceptions;
using MediatR;

namespace ERP.Application.Behaviors;

/// <summary>
/// Bloquea handlers MediatR si la cuenta de billing SaaS no permite acceso (suspensión, etc.).
/// </summary>
public sealed class BillingGateBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IBillingGovernanceService _billing;
    private readonly ICurrentSubscriber _subscriber;

    public BillingGateBehavior(IBillingGovernanceService billing, ICurrentSubscriber subscriber)
    {
        _billing = billing;
        _subscriber = subscriber;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        if (subscriberId == Guid.Empty)
            return await next();

        if (!await _billing.CanAccessPlatformAsync(subscriberId, ct))
        {
            var state = await _billing.GetStateAsync(subscriberId, ct);
            throw state.BillingStatus switch
            {
                Domain.Billing.Enums.BillingAccountStatus.Suspended => BillingAccessDeniedException.Suspended(),
                Domain.Billing.Enums.BillingAccountStatus.PastDue => BillingAccessDeniedException.PastDue(),
                _ => BillingAccessDeniedException.Suspended(),
            };
        }

        return await next();
    }
}
