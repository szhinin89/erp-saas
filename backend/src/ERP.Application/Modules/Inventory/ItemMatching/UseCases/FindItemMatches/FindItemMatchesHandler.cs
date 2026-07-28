using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Application.Modules.Inventory.ItemMatching.Mapping;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.FindItemMatches;

public sealed class FindItemMatchesHandler : IRequestHandler<FindItemMatchesQuery, Result<IReadOnlyList<PurchaseReceptionLineMatchDto>>>
{
    private const int MaxSuggestionsPerLine = 5;

    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly IItemMatchFinder _matchFinder;
    private readonly ICurrentTenant _tenant;

    public FindItemMatchesHandler(IPurchaseReceptionDocumentRepository documentRepo, IItemMatchFinder matchFinder, ICurrentTenant tenant)
    {
        _documentRepo = documentRepo;
        _matchFinder = matchFinder;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<PurchaseReceptionLineMatchDto>>> Handle(
        FindItemMatchesQuery request, CancellationToken cancellationToken)
    {
        var document = await _documentRepo.GetByIdAsync(_tenant.TenantId, request.PurchaseReceptionDocumentId, cancellationToken);
        if (document is null)
            return Result<IReadOnlyList<PurchaseReceptionLineMatchDto>>.NotFound("El documento de recepción no existe.");

        var dtos = new List<PurchaseReceptionLineMatchDto>();

        foreach (var line in document.Lines)
        {
            if (line.MatchStatus is ItemMatchStatus.AutoMatched or ItemMatchStatus.ManuallyMatched)
            {
                dtos.Add(ItemMatchingMapper.ToDto(line, document.SupplierId));
                continue;
            }

            var suggestions = await _matchFinder.FindCandidatesAsync(
                _tenant.TenantId, document.SupplierId, line.SupplierCode, line.SupplierAuxCode,
                line.Description, MaxSuggestionsPerLine, cancellationToken);

            dtos.Add(ItemMatchingMapper.ToDto(line, document.SupplierId, suggestions));
        }

        return Result<IReadOnlyList<PurchaseReceptionLineMatchDto>>.Success(dtos);
    }
}
