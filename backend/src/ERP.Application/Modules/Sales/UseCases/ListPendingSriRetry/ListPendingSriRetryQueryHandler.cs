using ERP.Application.Common;
using ERP.Domain.Modules.Fiscal.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Sales.UseCases.ListPendingSriRetry;

public sealed record ListPendingSriRetryQuery : IRequest<Result<IReadOnlyList<SalesBillRetryCandidate>>>, IPlatformScopedRequest;

public sealed class ListPendingSriRetryQueryHandler
    : IRequestHandler<ListPendingSriRetryQuery, Result<IReadOnlyList<SalesBillRetryCandidate>>>
{
    private readonly IInvoiceRepository _invoices;

    public ListPendingSriRetryQueryHandler(IInvoiceRepository invoices)
    {
        _invoices = invoices;
    }

    public async Task<Result<IReadOnlyList<SalesBillRetryCandidate>>> Handle(
        ListPendingSriRetryQuery request,
        CancellationToken ct)
    {
        var rows = await _invoices.ListPendingElectronicRetryAsync(ct);
        var mapped = rows
            .Select(r => new SalesBillRetryCandidate(r.PublicId, r.SubscriberId))
            .ToList();
        return Result<IReadOnlyList<SalesBillRetryCandidate>>.Success(mapped);
    }
}
