using ERP.Application.Common;
using ERP.Application.Common.Subscriptions;
using ERP.Domain.Subscribers.Exceptions;
using MediatR;

namespace ERP.Application.Platform.Subscribers.UseCases;

// ── Activate ──────────────────────────────────────────────────────────────────

public sealed class ActivateSubscriberHandler : IRequestHandler<ActivateSubscriberCommand, Result<bool>>
{
    private readonly ISubscriptionLifecycleOrchestrator _lifecycle;
    private readonly ICurrentUser                       _currentUser;

    public ActivateSubscriberHandler(
        ISubscriptionLifecycleOrchestrator lifecycle,
        ICurrentUser currentUser)
    {
        _lifecycle   = lifecycle;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(ActivateSubscriberCommand request, CancellationToken ct)
    {
        try
        {
            await _lifecycle.ActivateAsync(request.SubscriberId, _currentUser.UserId, request.Notes, ct);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no encontrado"))
        {
            return Result<bool>.NotFound("Suscriptor no encontrado.");
        }
        catch (InvalidLifecycleTransitionException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
        // DbUpdateConcurrencyException propagates up — ExceptionMiddleware maps it to 409
    }
}

// ── Suspend ───────────────────────────────────────────────────────────────────

public sealed class SuspendSubscriberHandler : IRequestHandler<SuspendSubscriberCommand, Result<bool>>
{
    private readonly ISubscriptionLifecycleOrchestrator _lifecycle;
    private readonly ICurrentUser                       _currentUser;

    public SuspendSubscriberHandler(
        ISubscriptionLifecycleOrchestrator lifecycle,
        ICurrentUser currentUser)
    {
        _lifecycle   = lifecycle;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(SuspendSubscriberCommand request, CancellationToken ct)
    {
        try
        {
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Sin motivo indicado." : request.Reason;
            await _lifecycle.SuspendAsync(request.SubscriberId, reason, _currentUser.UserId, ct);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no encontrado"))
        {
            return Result<bool>.NotFound("Suscriptor no encontrado.");
        }
        catch (InvalidLifecycleTransitionException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
        // DbUpdateConcurrencyException propagates up — ExceptionMiddleware maps it to 409
    }
}

// ── SetTrial ──────────────────────────────────────────────────────────────────

public sealed class SetSubscriberTrialHandler : IRequestHandler<SetSubscriberTrialCommand, Result<bool>>
{
    private readonly ISubscriptionLifecycleOrchestrator _lifecycle;
    private readonly ICurrentUser                       _currentUser;

    public SetSubscriberTrialHandler(
        ISubscriptionLifecycleOrchestrator lifecycle,
        ICurrentUser currentUser)
    {
        _lifecycle   = lifecycle;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(SetSubscriberTrialCommand request, CancellationToken ct)
    {
        if (request.TrialEndsAtUtc <= DateTime.UtcNow)
            return Result<bool>.Failure("TrialEndsAtUtc debe ser una fecha futura.");

        try
        {
            await _lifecycle.StartTrialAsync(request.SubscriberId, request.TrialEndsAtUtc, _currentUser.UserId, ct);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no encontrado"))
        {
            return Result<bool>.NotFound("Suscriptor no encontrado.");
        }
        catch (InvalidLifecycleTransitionException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
        // DbUpdateConcurrencyException propagates up — ExceptionMiddleware maps it to 409
    }
}

// ── GracePeriod ───────────────────────────────────────────────────────────────

public sealed class SetSubscriberGracePeriodHandler : IRequestHandler<SetSubscriberGracePeriodCommand, Result<bool>>
{
    private readonly ISubscriptionLifecycleOrchestrator _lifecycle;
    private readonly ICurrentUser                       _currentUser;

    public SetSubscriberGracePeriodHandler(
        ISubscriptionLifecycleOrchestrator lifecycle,
        ICurrentUser currentUser)
    {
        _lifecycle   = lifecycle;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(SetSubscriberGracePeriodCommand request, CancellationToken ct)
    {
        if (request.GracePeriodEndsAtUtc <= DateTime.UtcNow)
            return Result<bool>.Failure("GracePeriodEndsAtUtc debe ser una fecha futura.");

        try
        {
            await _lifecycle.EnterGracePeriodAsync(
                request.SubscriberId, request.GracePeriodEndsAtUtc, _currentUser.UserId, ct);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no encontrado"))
        {
            return Result<bool>.NotFound("Suscriptor no encontrado.");
        }
        catch (InvalidLifecycleTransitionException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
        // DbUpdateConcurrencyException propagates up — ExceptionMiddleware maps it to 409
    }
}

// ── ChangePlan ────────────────────────────────────────────────────────────────

public sealed class ChangePlatformSubscriberPlanHandler : IRequestHandler<ChangePlatformSubscriberPlanCommand, Result<bool>>
{
    private readonly ERP.Domain.Subscribers.Interfaces.ISubscriberRepository _subscribers;
    private readonly ERP.Application.Platform.Audit.IPlatformAuditLogger     _audit;
    private readonly ICurrentUser                                             _currentUser;
    private readonly ERP.Application.Subscriptions.Caching.ISubscriberEntitlementsCacheInvalidator _cache;

    public ChangePlatformSubscriberPlanHandler(
        ERP.Domain.Subscribers.Interfaces.ISubscriberRepository subscribers,
        ERP.Application.Platform.Audit.IPlatformAuditLogger audit,
        ICurrentUser currentUser,
        ERP.Application.Subscriptions.Caching.ISubscriberEntitlementsCacheInvalidator cache)
    {
        _subscribers = subscribers;
        _audit       = audit;
        _currentUser = currentUser;
        _cache       = cache;
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

        subscriber.SetPlanCode(newPlanCode, _currentUser.UserId);

        // DbUpdateConcurrencyException propagates up — ExceptionMiddleware maps it to 409
        await _subscribers.SaveChangesAsync(ct);

        await _cache.InvalidateAsync(request.SubscriberId, ct);

        await _audit.LogAsync(
            ERP.Domain.Platform.Audit.Entities.PlatformAuditLog.Actions.PlanChanged,
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
