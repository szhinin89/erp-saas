using System.Reflection;
using MediatR;
using ERP.Application.Common;
using ERP.Domain.Subscriptions.Exceptions;
using ERP.Domain.Subscriptions.Interfaces;

namespace ERP.Application.Behaviors;

/// <summary>
/// Valida features y límites de suscripción antes del handler, e incrementa uso medido tras éxito (<see cref="Result{T}.IsSuccess"/>).
/// No sustituye permisos de seguridad.
/// </summary>
public sealed class SubscriptionGateBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ISubscriptionService _subscriptions;
    private readonly ICurrentTenant _tenant;

    public SubscriptionGateBehavior(ISubscriptionService subscriptions, ICurrentTenant tenant)
    {
        _subscriptions = subscriptions;
        _tenant = tenant;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId == Guid.Empty)
            return await next();

        var type = request.GetType();
        var require = type.GetCustomAttribute<RequireFeatureAttribute>(inherit: true);
        if (require is not null && !await _subscriptions.HasFeatureAsync(tenantId, require.FeatureCode, ct))
            throw new FeatureNotEntitledException(require.FeatureCode);

        var consume = type.GetCustomAttribute<ConsumeSubscriptionUnitsAttribute>(inherit: true);
        if (consume is not null && !await _subscriptions.CheckLimitAsync(tenantId, consume.FeatureCode, consume.Units, ct))
            throw new SubscriptionLimitExceededException(consume.FeatureCode, 0, consume.Units);

        var response = await next();

        if (consume is not null && IsSuccessfulResult(response))
            await _subscriptions.IncrementUsageAsync(tenantId, consume.FeatureCode, consume.Units, ct);

        return response;
    }

    private static bool IsSuccessfulResult(TResponse response)
    {
        if (response is null) return false;
        var prop = response.GetType().GetProperty("IsSuccess", BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(response) is true;
    }
}
