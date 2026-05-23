using ERP.Application.Common;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Sales.UseCases.ListPendingSriRetry;

public sealed record ListPendingSriRetryQuery : IRequest<Result<IReadOnlyList<SalesBillRetryCandidate>>>, IPlatformScopedRequest;

public sealed class ListPendingSriRetryQueryHandler
    : IRequestHandler<ListPendingSriRetryQuery, Result<IReadOnlyList<SalesBillRetryCandidate>>>
{
    private readonly ISalesRepository _sales;

    public ListPendingSriRetryQueryHandler(ISalesRepository sales)
    {
        _sales = sales;
    }

    public async Task<Result<IReadOnlyList<SalesBillRetryCandidate>>> Handle(
        ListPendingSriRetryQuery request,
        CancellationToken ct)
    {
        var rows = await _sales.ListPendingElectronicRetryAsync(ct);
        return Result<IReadOnlyList<SalesBillRetryCandidate>>.Success(rows);
    }
}
