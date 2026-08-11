using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

namespace ERP.Application.Modules.Inventory.ItemMatching.Services;

/// <summary>
/// Efecto compartido de confirmar una vinculación línea↔Item (individual o en lote) — evita
/// duplicar entre <c>MatchItemHandler</c> y <c>BulkMatchItemsHandler</c> la lógica de: marcar la
/// línea como conciliada y crear <c>ItemSupplierCode</c> si el código de esta línea todavía no
/// estaba registrado para el proveedor del documento. No crea Items, no toca impuestos.
/// </summary>
public interface IItemMatchConfirmationService
{
    Task ConfirmAsync(
        PurchaseReceptionDocument document,
        PurchaseReceptionLine line,
        Guid itemId,
        Guid matchedBy,
        DateTime matchedAtUtc,
        Guid? packagingLevelId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Efecto inverso de <see cref="ConfirmAsync"/>: antes de desvincular la línea, revierte el
    /// <c>ItemSupplierCode</c> que pudo haberse creado automáticamente para el ítem incorrecto —
    /// para que el motor de sugerencias (<see cref="IItemMatchFinder"/>) no lo vuelva a proponer.
    /// </summary>
    Task UnconfirmAsync(
        PurchaseReceptionDocument document,
        PurchaseReceptionLine line,
        Guid unmatchedBy,
        CancellationToken cancellationToken = default
    );
}

public sealed class ItemMatchConfirmationService : IItemMatchConfirmationService
{
    private readonly IItemRepository _itemRepo;

    public ItemMatchConfirmationService(IItemRepository itemRepo) => _itemRepo = itemRepo;

    public async Task ConfirmAsync(
        PurchaseReceptionDocument document,
        PurchaseReceptionLine line,
        Guid itemId,
        Guid matchedBy,
        DateTime matchedAtUtc,
        Guid? packagingLevelId = null,
        CancellationToken cancellationToken = default
    )
    {
        if (document.SupplierId is { } supplierId && !string.IsNullOrWhiteSpace(line.SupplierCode))
        {
            var alreadyExists = await _itemRepo.SupplierCodeExistsAsync(
                supplierId,
                line.SupplierCode,
                document.TenantId,
                cancellationToken
            );
            if (alreadyExists && packagingLevelId.HasValue)
            {
                await _itemRepo.UpdateSupplierCodePackagingLevelAsync(
                    itemId,
                    supplierId,
                    line.SupplierCode,
                    packagingLevelId,
                    document.TenantId,
                    matchedBy,
                    cancellationToken
                );
                await _itemRepo.SaveChangesAsync(cancellationToken);
            }
            else if (!alreadyExists)
            {
                var item = await _itemRepo.GetByIdAsync(
                    itemId,
                    document.TenantId,
                    cancellationToken
                );
                if (item is not null)
                {
                    item.AddSupplierCode(
                        line.SupplierCode,
                        isPrimary: false,
                        supplierId,
                        matchedBy,
                        packagingLevelId
                    );
                    await _itemRepo.SaveChangesAsync(cancellationToken);
                }
            }
        }

        line.ManualMatch(itemId, matchedBy, matchedAtUtc);
    }

    public async Task UnconfirmAsync(
        PurchaseReceptionDocument document,
        PurchaseReceptionLine line,
        Guid unmatchedBy,
        CancellationToken cancellationToken = default
    )
    {
        if (
            document.SupplierId is { } supplierId
            && !string.IsNullOrWhiteSpace(line.SupplierCode)
            && line.ItemId is { } previousItemId
        )
        {
            var item = await _itemRepo.GetByIdAsync(
                previousItemId,
                document.TenantId,
                cancellationToken
            );
            if (item is not null)
            {
                item.DisableSupplierCode(supplierId, line.SupplierCode, unmatchedBy);
                await _itemRepo.SaveChangesAsync(cancellationToken);
            }
        }

        line.UnmatchItem();
    }
}
