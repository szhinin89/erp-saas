using ERP.Application.Billing.DTOs;
using ERP.Application.Common;
using ERP.Domain.Billing.Interfaces;
using MediatR;

namespace ERP.Application.Billing.UseCases.ListBillingEvents;

public sealed class ListBillingEventsHandler
    : IRequestHandler<ListBillingEventsQuery, Result<IReadOnlyList<BillingEventDto>>>
{
    private readonly ICurrentSubscriber _subscriber;
    private readonly ISubscriberBillingRepository _billing;

    public ListBillingEventsHandler(ICurrentSubscriber subscriber, ISubscriberBillingRepository billing)
    {
        _subscriber = subscriber;
        _billing = billing;
    }

    public async Task<Result<IReadOnlyList<BillingEventDto>>> Handle(ListBillingEventsQuery request, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        if (subscriberId == Guid.Empty)
            return Result<IReadOnlyList<BillingEventDto>>.Failure("Contexto de suscriptor no establecido.");

        var events = await _billing.GetBillingEventsAsync(subscriberId, request.Take, ct);
        var dtos = events.Select(e => new BillingEventDto(
            e.Id,
            e.EventType,
            e.Source.ToString(),
            e.OccurredAtUtc,
            e.CorrelationId,
            e.PayloadJson)).ToList();

        return Result<IReadOnlyList<BillingEventDto>>.Success(dtos);
    }
}
