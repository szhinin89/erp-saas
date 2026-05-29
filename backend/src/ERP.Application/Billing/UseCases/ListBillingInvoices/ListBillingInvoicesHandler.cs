using ERP.Application.Billing.DTOs;
using ERP.Application.Common;
using ERP.Domain.Billing.Interfaces;
using MediatR;

namespace ERP.Application.Billing.UseCases.ListBillingInvoices;

public sealed class ListBillingInvoicesHandler
    : IRequestHandler<ListBillingInvoicesQuery, Result<IReadOnlyList<SaasBillingInvoiceDto>>>
{
    private readonly ICurrentSubscriber _subscriber;
    private readonly ISubscriberBillingRepository _billing;

    public ListBillingInvoicesHandler(ICurrentSubscriber subscriber, ISubscriberBillingRepository billing)
    {
        _subscriber = subscriber;
        _billing = billing;
    }

    public async Task<Result<IReadOnlyList<SaasBillingInvoiceDto>>> Handle(
        ListBillingInvoicesQuery request,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        if (subscriberId == Guid.Empty)
            return Result<IReadOnlyList<SaasBillingInvoiceDto>>.Failure("Contexto de suscriptor no establecido.");

        var invoices = await _billing.GetInvoicesBySubscriberAsync(subscriberId, request.Take, ct);
        var dtos = invoices.Select(inv => new SaasBillingInvoiceDto(
            inv.Id,
            inv.InvoiceNumber,
            inv.Status.ToString(),
            inv.CurrencyCode,
            inv.Subtotal,
            inv.TaxAmount,
            inv.Total,
            inv.PeriodStartUtc,
            inv.PeriodEndUtc,
            inv.IssuedAtUtc,
            inv.DueAtUtc,
            inv.PaidAtUtc,
            inv.ErpInvoiceId,
            inv.Lines.Select(l => new SaasBillingInvoiceLineDto(
                l.Description,
                l.LineType.ToString(),
                l.Quantity,
                l.UnitAmount,
                l.LineTotal)).ToList())).ToList();

        return Result<IReadOnlyList<SaasBillingInvoiceDto>>.Success(dtos);
    }
}
