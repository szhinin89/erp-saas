using MediatR;
using ERP.Application.Common;
using ERP.Application.Pricing.DTOs;
using ERP.Domain.Modules.Pricing.Interfaces;

namespace ERP.Application.Pricing.UseCases.ResolveItemPrice;

public sealed record ResolveItemPriceQuery(
    Guid     ItemId,
    string   UomCode,
    decimal  Qty       = 1,
    Guid?    VariantId = null
) : IRequest<Result<ResolvedPriceDto>>, ICompanyScopedRequest;

public sealed class ResolveItemPriceQueryHandler
    : IRequestHandler<ResolveItemPriceQuery, Result<ResolvedPriceDto>>
{
    private readonly IPriceListRepository _repository;

    public ResolveItemPriceQueryHandler(IPriceListRepository repository) => _repository = repository;

    public async Task<Result<ResolvedPriceDto>> Handle(ResolveItemPriceQuery query, CancellationToken ct)
    {
        // Get all active entries for the item across price lists, ordered by minQty DESC
        var entries = await _repository.GetEntriesByItemAsync(query.ItemId, query.VariantId, ct);

        if (!entries.Any())
            return Result<ResolvedPriceDto>.NotFound(
                "No se encontró precio para este ítem en las listas activas.");

        // Select the entry with highest MinQty that is <= requested Qty
        var uom = query.UomCode.Trim().ToUpperInvariant();
        var applicableEntries = entries
            .Where(e => e.UomCode == uom && e.MinQty <= query.Qty)
            .OrderByDescending(e => e.MinQty)
            .ToList();

        if (!applicableEntries.Any())
            return Result<ResolvedPriceDto>.NotFound(
                $"No se encontró precio para UOM '{uom}' con cantidad {query.Qty}.");

        var best    = applicableEntries.First();
        var listDef = await _repository.GetByIdAsync(best.PriceListId, ct);

        return Result<ResolvedPriceDto>.Success(new ResolvedPriceDto(
            query.ItemId,
            query.VariantId,
            best.UomCode,
            best.Price,
            0m,
            best.Price,
            listDef?.PriceIncludesVat ?? false,
            best.PriceListId,
            listDef?.Name ?? "–",
            $"PriceList:{best.PriceListId}|MinQty:{best.MinQty}"));
    }
}
