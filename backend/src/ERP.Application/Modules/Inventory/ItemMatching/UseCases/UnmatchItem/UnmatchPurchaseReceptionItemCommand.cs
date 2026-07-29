using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.UnmatchItem;

/// <summary>Desvincula el Item conciliado incorrectamente de una línea de recepción — Item Matching, Fase 1.</summary>
public sealed record UnmatchPurchaseReceptionItemCommand(Guid PurchaseReceptionLineId)
    : IRequest<Result<PurchaseReceptionLineMatchDto>>,
        IBranchScopedRequest;
