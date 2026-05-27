using ERP.Application.Common;
using ERP.Application.Platform.Audit;
using ERP.Application.Subscriptions.Caching;
using ERP.Domain.Platform.Audit.Entities;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Platform.Subscribers.UseCases;

public sealed class ActivateSubscriberHandler : IRequestHandler<ActivateSubscriberCommand, Result<bool>>
{
    private readonly ISubscriberRepository _subscribers;
    private readonly IPlatformAuditLogger _audit;
    private readonly ICurrentUser _currentUser;
    private readonly ISubscriberEntitlementsCacheInvalidator _cacheInvalidator;

    public ActivateSubscriberHandler(
        ISubscriberRepository subscribers,
        IPlatformAuditLogger audit,
        ICurrentUser currentUser,
        ISubscriberEntitlementsCacheInvalidator cacheInvalidator)
    {
        _subscribers = subscribers;
        _audit = audit;
        _currentUser = currentUser;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<Result<bool>> Handle(ActivateSubscriberCommand request, CancellationToken ct)
    {
        var subscriber = await _subscribers.GetByIdAsync(request.SubscriberId, ct);
        if (subscriber is null)
            return Result<bool>.NotFound("Suscriptor no encontrado.");

        subscriber.Activate(_currentUser.UserId);
        await _subscribers.SaveChangesAsync(ct);
        await _cacheInvalidator.InvalidateAsync(request.SubscriberId, ct);

        await _audit.LogAsync(
            PlatformAuditLog.Actions.SubscriberActivated,
            _currentUser.UserId,
            _currentUser.Email,
            request.SubscriberId,
            resourceType: "Subscriber",
            resourceId: request.SubscriberId,
            notes: request.Notes,
            ct: ct);

        return Result<bool>.Success(true);
    }
}

public sealed class SuspendSubscriberHandler : IRequestHandler<SuspendSubscriberCommand, Result<bool>>
{
    private readonly ISubscriberRepository _subscribers;
    private readonly IPlatformAuditLogger _audit;
    private readonly ICurrentUser _currentUser;
    private readonly ISubscriberEntitlementsCacheInvalidator _cacheInvalidator;

    public SuspendSubscriberHandler(
        ISubscriberRepository subscribers,
        IPlatformAuditLogger audit,
        ICurrentUser currentUser,
        ISubscriberEntitlementsCacheInvalidator cacheInvalidator)
    {
        _subscribers = subscribers;
        _audit = audit;
        _currentUser = currentUser;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<Result<bool>> Handle(SuspendSubscriberCommand request, CancellationToken ct)
    {
        var subscriber = await _subscribers.GetByIdAsync(request.SubscriberId, ct);
        if (subscriber is null)
            return Result<bool>.NotFound("Suscriptor no encontrado.");

        var previousStatus = subscriber.LifecycleStatus.ToString();
        subscriber.Suspend(request.Reason, _currentUser.UserId);
        await _subscribers.SaveChangesAsync(ct);
        await _cacheInvalidator.InvalidateAsync(request.SubscriberId, ct);

        await _audit.LogAsync(
            PlatformAuditLog.Actions.SubscriberSuspended,
            _currentUser.UserId,
            _currentUser.Email,
            request.SubscriberId,
            resourceType: "Subscriber",
            resourceId: request.SubscriberId,
            oldValueJson: $"{{\"status\":\"{previousStatus}\"}}",
            newValueJson: $"{{\"status\":\"Suspended\",\"reason\":\"{request.Reason ?? ""}\"}}",
            notes: request.Reason,
            ct: ct);

        return Result<bool>.Success(true);
    }
}

public sealed class SetSubscriberTrialHandler : IRequestHandler<SetSubscriberTrialCommand, Result<bool>>
{
    private readonly ISubscriberRepository _subscribers;
    private readonly IPlatformAuditLogger _audit;
    private readonly ICurrentUser _currentUser;
    private readonly ISubscriberEntitlementsCacheInvalidator _cacheInvalidator;

    public SetSubscriberTrialHandler(
        ISubscriberRepository subscribers,
        IPlatformAuditLogger audit,
        ICurrentUser currentUser,
        ISubscriberEntitlementsCacheInvalidator cacheInvalidator)
    {
        _subscribers = subscribers;
        _audit = audit;
        _currentUser = currentUser;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<Result<bool>> Handle(SetSubscriberTrialCommand request, CancellationToken ct)
    {
        if (request.TrialEndsAtUtc <= DateTime.UtcNow)
            return Result<bool>.Failure("TrialEndsAtUtc debe ser una fecha futura.");

        var subscriber = await _subscribers.GetByIdAsync(request.SubscriberId, ct);
        if (subscriber is null)
            return Result<bool>.NotFound("Suscriptor no encontrado.");

        subscriber.SetTrial(_currentUser.UserId);
        await _subscribers.SaveChangesAsync(ct);
        await _cacheInvalidator.InvalidateAsync(request.SubscriberId, ct);

        await _audit.LogAsync(
            PlatformAuditLog.Actions.TrialStarted,
            _currentUser.UserId,
            _currentUser.Email,
            request.SubscriberId,
            resourceType: "Subscriber",
            resourceId: request.SubscriberId,
            newValueJson: $"{{\"trialEndsAtUtc\":\"{request.TrialEndsAtUtc:O}\"}}",
            ct: ct);

        return Result<bool>.Success(true);
    }
}

public sealed class SetSubscriberGracePeriodHandler : IRequestHandler<SetSubscriberGracePeriodCommand, Result<bool>>
{
    private readonly ISubscriberRepository _subscribers;
    private readonly IPlatformAuditLogger _audit;
    private readonly ICurrentUser _currentUser;
    private readonly ISubscriberEntitlementsCacheInvalidator _cacheInvalidator;

    public SetSubscriberGracePeriodHandler(
        ISubscriberRepository subscribers,
        IPlatformAuditLogger audit,
        ICurrentUser currentUser,
        ISubscriberEntitlementsCacheInvalidator cacheInvalidator)
    {
        _subscribers = subscribers;
        _audit = audit;
        _currentUser = currentUser;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<Result<bool>> Handle(SetSubscriberGracePeriodCommand request, CancellationToken ct)
    {
        if (request.GracePeriodEndsAtUtc <= DateTime.UtcNow)
            return Result<bool>.Failure("GracePeriodEndsAtUtc debe ser una fecha futura.");

        var subscriber = await _subscribers.GetByIdAsync(request.SubscriberId, ct);
        if (subscriber is null)
            return Result<bool>.NotFound("Suscriptor no encontrado.");

        subscriber.EnterGracePeriod(_currentUser.UserId);
        await _subscribers.SaveChangesAsync(ct);
        await _cacheInvalidator.InvalidateAsync(request.SubscriberId, ct);

        await _audit.LogAsync(
            PlatformAuditLog.Actions.GracePeriodStarted,
            _currentUser.UserId,
            _currentUser.Email,
            request.SubscriberId,
            resourceType: "Subscriber",
            resourceId: request.SubscriberId,
            newValueJson: $"{{\"gracePeriodEndsAtUtc\":\"{request.GracePeriodEndsAtUtc:O}\"}}",
            notes: request.Reason,
            ct: ct);

        return Result<bool>.Success(true);
    }
}

public sealed class ChangePlatformSubscriberPlanHandler : IRequestHandler<ChangePlatformSubscriberPlanCommand, Result<bool>>
{
    private readonly ISubscriberRepository _subscribers;
    private readonly IPlatformAuditLogger _audit;
    private readonly ICurrentUser _currentUser;
    private readonly ISubscriberEntitlementsCacheInvalidator _cacheInvalidator;

    public ChangePlatformSubscriberPlanHandler(
        ISubscriberRepository subscribers,
        IPlatformAuditLogger audit,
        ICurrentUser currentUser,
        ISubscriberEntitlementsCacheInvalidator cacheInvalidator)
    {
        _subscribers = subscribers;
        _audit = audit;
        _currentUser = currentUser;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<Result<bool>> Handle(ChangePlatformSubscriberPlanCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPlanCode))
            return Result<bool>.Failure("NewPlanCode requerido.");

        var subscriber = await _subscribers.GetByIdAsync(request.SubscriberId, ct);
        if (subscriber is null)
            return Result<bool>.NotFound("Suscriptor no encontrado.");

        var oldPlanCode = subscriber.PlanCode;
        var newPlanCode = request.NewPlanCode.Trim().ToLowerInvariant();

        // SetPlanCode triggers SyncSubscriberSubscriptionsFromPlanCodeAsync in DbContext.SaveChangesAsync
        subscriber.SetPlanCode(newPlanCode, _currentUser.UserId);
        await _subscribers.SaveChangesAsync(ct);
        await _cacheInvalidator.InvalidateAsync(request.SubscriberId, ct);

        await _audit.LogAsync(
            PlatformAuditLog.Actions.PlanChanged,
            _currentUser.UserId,
            _currentUser.Email,
            request.SubscriberId,
            resourceType: "Subscriber",
            resourceId: request.SubscriberId,
            oldValueJson: $"{{\"planCode\":\"{oldPlanCode ?? "null"}\"}}",
            newValueJson: $"{{\"planCode\":\"{newPlanCode}\"}}",
            notes: request.Notes,
            ct: ct);

        return Result<bool>.Success(true);
    }
}
