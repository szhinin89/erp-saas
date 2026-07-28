using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Application.Modules.Inventory.ItemMatching.Mapping;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.UnmatchItem;

public sealed class UnmatchPurchaseReceptionItemHandler
    : IRequestHandler<UnmatchPurchaseReceptionItemCommand, Result<PurchaseReceptionLineMatchDto>>
{
    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly IItemMatchConfirmationService _confirmationService;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public UnmatchPurchaseReceptionItemHandler(
        IPurchaseReceptionDocumentRepository documentRepo, IItemMatchConfirmationService confirmationService,
        ICurrentTenant tenant, ICurrentUser user)
    {
        _documentRepo = documentRepo;
        _confirmationService = confirmationService;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<PurchaseReceptionLineMatchDto>> Handle(
        UnmatchPurchaseReceptionItemCommand request, CancellationToken cancellationToken)
    {
        var document = await _documentRepo.GetByLineIdAsync(_tenant.TenantId, request.PurchaseReceptionLineId, cancellationToken);
        if (document is null)
            return Result<PurchaseReceptionLineMatchDto>.NotFound("La línea de recepción no existe.");

        if (document.Status == PurchaseReceptionDocumentStatus.Cancelled)
            return Result<PurchaseReceptionLineMatchDto>.Conflict("El documento de recepción está anulado.");

        var line = document.Lines.First(l => l.Id == request.PurchaseReceptionLineId);

        if (line.ItemId is null)
            return Result<PurchaseReceptionLineMatchDto>.ValidationFailure("La línea no tiene un ítem asociado.");

        await _confirmationService.UnconfirmAsync(document, line, _user.UserId, cancellationToken);
        await _documentRepo.SaveChangesAsync(cancellationToken);

        return Result<PurchaseReceptionLineMatchDto>.Success(ItemMatchingMapper.ToDto(line, document.SupplierId));
    }
}
