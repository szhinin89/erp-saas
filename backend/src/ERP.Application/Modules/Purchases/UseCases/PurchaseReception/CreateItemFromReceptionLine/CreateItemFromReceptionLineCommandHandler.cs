using ERP.Application.Common;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases.PurchaseReception.CreateItemFromReceptionLine;

public sealed class CreateItemFromReceptionLineCommandHandler
    : IRequestHandler<CreateItemFromReceptionLineCommand, Result<CreateItemFromReceptionLineResultDto>>
{
    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly IMediator _mediator;
    private readonly IItemMatchConfirmationService _confirmationService;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public CreateItemFromReceptionLineCommandHandler(
        IPurchaseReceptionDocumentRepository documentRepo, IMediator mediator,
        IItemMatchConfirmationService confirmationService, ICurrentTenant tenant, ICurrentUser user)
    {
        _documentRepo = documentRepo;
        _mediator = mediator;
        _confirmationService = confirmationService;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<CreateItemFromReceptionLineResultDto>> Handle(
        CreateItemFromReceptionLineCommand request, CancellationToken cancellationToken)
    {
        var document = await _documentRepo.GetByLineIdAsync(_tenant.TenantId, request.PurchaseReceptionLineId, cancellationToken);
        if (document is null)
            return Result<CreateItemFromReceptionLineResultDto>.NotFound("La línea de recepción no existe.");

        var line = document.Lines.First(l => l.Id == request.PurchaseReceptionLineId);

        if (line.ItemId is not null)
        {
            return Result<CreateItemFromReceptionLineResultDto>.Conflict(
                "La línea ya tiene un ítem vinculado.", "ITEM_ALREADY_MATCHED");
        }

        // El auxiliar suele ser el código de barras real del producto; si el XML no lo trae, se
        // usa el código principal — decisión de negocio confirmada para esta fase.
        var barcodeCode = line.SupplierAuxCode ?? line.SupplierCode;
        if (string.IsNullOrWhiteSpace(barcodeCode))
        {
            return Result<CreateItemFromReceptionLineResultDto>.ValidationFailure(
                "La línea no trae código de proveedor ni código auxiliar para usar como código de barras.");
        }

        var createItemCommand = new CreateItemCommand(
            SKU: request.Sku,
            ShortName: request.ShortName,
            Description: request.Description,
            ItemTypeId: request.ItemTypeId,
            DefaultUomCode: request.DefaultUomCode,
            CategoryNodeId: request.CategoryNodeId,
            BrandId: request.BrandId,
            Barcodes: [new CreateItemBarcodeDto(barcodeCode, request.BarcodeType, IsPrimary: true)]);

        var createResult = await _mediator.Send(createItemCommand, cancellationToken);
        if (!createResult.IsSuccess)
            return Result<CreateItemFromReceptionLineResultDto>.Failure(createResult.Error!, createResult.Code);

        var item = createResult.Value!;

        // Crea ItemSupplierCode (si no existía) y marca la línea ManuallyMatched — misma lógica
        // que la vinculación manual/masiva, sin reimplementar deduplicación.
        await _confirmationService.ConfirmAsync(document, line, item.Id, _user.UserId, DateTime.UtcNow, cancellationToken);
        await _documentRepo.SaveChangesAsync(cancellationToken);

        return Result<CreateItemFromReceptionLineResultDto>.Success(
            new CreateItemFromReceptionLineResultDto(item.Id, item.ShortName, line.SupplierCode, "Created"));
    }
}
