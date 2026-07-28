using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Application.Modules.Inventory.ItemMatching.Mapping;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.GetLineMatch;

public sealed class GetPurchaseReceptionLineMatchHandler
    : IRequestHandler<GetPurchaseReceptionLineMatchQuery, Result<PurchaseReceptionLineMatchDto>>
{
    private const int MaxSuggestions = 5;

    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly IItemMatchFinder _matchFinder;
    private readonly ICurrentTenant _tenant;

    public GetPurchaseReceptionLineMatchHandler(
        IPurchaseReceptionDocumentRepository documentRepo, IItemMatchFinder matchFinder, ICurrentTenant tenant)
    {
        _documentRepo = documentRepo;
        _matchFinder = matchFinder;
        _tenant = tenant;
    }

    public async Task<Result<PurchaseReceptionLineMatchDto>> Handle(
        GetPurchaseReceptionLineMatchQuery request, CancellationToken cancellationToken)
    {
        var document = await _documentRepo.GetByLineIdAsync(_tenant.TenantId, request.PurchaseReceptionLineId, cancellationToken);
        if (document is null)
            return Result<PurchaseReceptionLineMatchDto>.NotFound("La línea de recepción no existe.");

        var line = document.Lines.First(l => l.Id == request.PurchaseReceptionLineId);

        if (line.MatchStatus is ItemMatchStatus.AutoMatched or ItemMatchStatus.ManuallyMatched)
            return Result<PurchaseReceptionLineMatchDto>.Success(ItemMatchingMapper.ToDto(line, document.SupplierId));

        var suggestions = await _matchFinder.FindCandidatesAsync(
            _tenant.TenantId, document.SupplierId, line.SupplierCode, line.SupplierAuxCode,
            line.Description, MaxSuggestions, cancellationToken);

        return Result<PurchaseReceptionLineMatchDto>.Success(ItemMatchingMapper.ToDto(line, document.SupplierId, suggestions));
    }
}
