using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.GetStockKardex;

public record KardexLineDto(
    Guid      MovementId,
    Guid      ItemId,
    Guid?     VariantId,
    Guid      WarehouseId,
    Guid?     LotId,
    Guid?     SerialId,
    string    MovementType,
    decimal   Quantity,
    decimal   PreviousQuantity,
    decimal   ResultQuantity,
    decimal?  UnitCost,
    decimal?  TotalCost,
    string?   Reference,
    string?   SourceDocumentType,
    DateTime  MovementDate
);

public record KardexResponse(
    IReadOnlyList<KardexLineDto> Lines,
    int   TotalCount,
    int   PageNumber,
    int   PageSize
);

public sealed record GetStockKardexQuery(
    Guid      ItemId,
    Guid?     VariantId   = null,
    Guid?     WarehouseId = null,
    DateTime? FromUtc     = null,
    DateTime? ToUtc       = null,
    int       PageNumber  = 1,
    int       PageSize    = 50
) : IRequest<Result<KardexResponse>>, ICompanyScopedRequest;

public sealed class GetStockKardexQueryHandler
    : IRequestHandler<GetStockKardexQuery, Result<KardexResponse>>
{
    private readonly IItemRepository _repository;

    public GetStockKardexQueryHandler(IItemRepository repository) => _repository = repository;

    public async Task<Result<KardexResponse>> Handle(GetStockKardexQuery query, CancellationToken ct)
    {
        var (rows, total) = await _repository.GetKardexAsync(
            query.ItemId, query.VariantId, query.WarehouseId,
            query.FromUtc, query.ToUtc,
            query.PageNumber, query.PageSize, ct);

        var lines = rows.Select(r => new KardexLineDto(
            r.MovementId, r.ItemId, r.VariantId, r.WarehouseId,
            r.LotId, r.SerialId,
            r.MovementType, r.Quantity,
            r.PreviousQuantity, r.ResultQuantity,
            r.UnitCost, r.TotalCost,
            r.Reference, r.SourceDocumentType, r.MovementDate)).ToList();

        return Result<KardexResponse>.Success(
            new KardexResponse(lines, total, query.PageNumber, query.PageSize));
    }
}
