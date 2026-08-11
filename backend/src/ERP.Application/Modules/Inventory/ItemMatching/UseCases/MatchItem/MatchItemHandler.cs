using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Application.Modules.Inventory.ItemMatching.Mapping;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.MatchItem;

public sealed class MatchItemHandler
    : IRequestHandler<MatchItemCommand, Result<PurchaseReceptionLineMatchDto>>
{
    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly IItemRepository _itemRepo;
    private readonly IItemMatchConfirmationService _confirmationService;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public MatchItemHandler(
        IPurchaseReceptionDocumentRepository documentRepo,
        IItemRepository itemRepo,
        IItemMatchConfirmationService confirmationService,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _documentRepo = documentRepo;
        _itemRepo = itemRepo;
        _confirmationService = confirmationService;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<PurchaseReceptionLineMatchDto>> Handle(
        MatchItemCommand request,
        CancellationToken cancellationToken
    )
    {
        var document = await _documentRepo.GetByLineIdAsync(
            _tenant.TenantId,
            request.PurchaseReceptionLineId,
            cancellationToken
        );
        if (document is null)
            return Result<PurchaseReceptionLineMatchDto>.NotFound(
                "La línea de recepción no existe."
            );

        var line = document.Lines.First(l => l.Id == request.PurchaseReceptionLineId);

        var item = await _itemRepo.GetByIdLightAsync(
            request.ItemId,
            _tenant.TenantId,
            cancellationToken
        );
        if (item is null)
            return Result<PurchaseReceptionLineMatchDto>.NotFound(
                "El ítem seleccionado no existe."
            );

        if (
            request.PackagingLevelId.HasValue
            && !await _itemRepo.PackagingLevelBelongsToItemAsync(
                request.ItemId,
                request.PackagingLevelId.Value,
                _tenant.TenantId,
                cancellationToken
            )
        )
        {
            return Result<PurchaseReceptionLineMatchDto>.ValidationFailure(
                "La presentación seleccionada no pertenece al ítem."
            );
        }

        if (
            request.PackagingLevelId.HasValue
            && document.SupplierId.HasValue
            && !string.IsNullOrWhiteSpace(line.SupplierCode)
        )
        {
            var existingMatch = await _itemRepo.GetSupplierCodeMatchAsync(
                document.SupplierId.Value,
                line.SupplierCode!,
                _tenant.TenantId,
                cancellationToken
            );
            if (existingMatch is not null && existingMatch.ItemId != request.ItemId)
            {
                return Result<PurchaseReceptionLineMatchDto>.ValidationFailure(
                    "El código de proveedor ya está asociado a otro ítem."
                );
            }
        }

        await _confirmationService.ConfirmAsync(
            document,
            line,
            request.ItemId,
            _user.UserId,
            DateTime.UtcNow,
            request.PackagingLevelId,
            cancellationToken
        );
        await _documentRepo.SaveChangesAsync(cancellationToken);

        return Result<PurchaseReceptionLineMatchDto>.Success(
            ItemMatchingMapper.ToDto(line, document.SupplierId)
        );
    }
}
