using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases.PurchaseReception.CreateItemFromReceptionLine;

public sealed record CreateItemFromReceptionLineResultDto(
    Guid ItemId, string ItemName, string? SupplierCode, string Status);

/// <summary>
/// Item Matching — crea un Item nuevo directamente desde una línea de recepción sin conciliar,
/// reutilizando el módulo Items (<c>CreateItemCommand</c>, vía <see cref="IMediator"/>) y el
/// <c>ItemMatchConfirmationService</c> ya construido para la vinculación manual/masiva — no
/// reimplementa reglas de creación de Item ni de la relación proveedor↔ítem.
/// </summary>
public sealed record CreateItemFromReceptionLineCommand(
    Guid PurchaseReceptionLineId,
    string Sku,
    string ShortName,
    string Description,
    Guid ItemTypeId,
    Guid CategoryNodeId,
    Guid BrandId,
    string DefaultUomCode,
    string BarcodeType)
    : IRequest<Result<CreateItemFromReceptionLineResultDto>>, IBranchScopedRequest;
