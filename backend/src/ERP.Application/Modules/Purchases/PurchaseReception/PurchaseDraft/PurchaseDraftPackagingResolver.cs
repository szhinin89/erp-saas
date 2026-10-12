using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

namespace ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft;

/// <summary>
/// Presentación aprendida (Proveedor + Código → Item/PackagingLevel) de cada línea de recepción ya
/// conciliada. Compartido por "Crear compra" desde Recepción y por la lectura de un borrador
/// guardado (PURCHASE-DRAFT-RECOVERY D1), para que ambos propongan exactamente la misma presentación.
/// Solo se informa una línea cuando la equivalencia aprendida apunta al mismo ítem conciliado.
/// </summary>
public static class PurchaseDraftPackagingResolver
{
    public static async Task<
        IReadOnlyDictionary<Guid, PurchaseDraftLinePackagingSnapshot>
    > ResolveAsync(
        IItemRepository itemRepo,
        PurchaseReceptionDocument document,
        CancellationToken cancellationToken
    )
    {
        var snapshots = new Dictionary<Guid, PurchaseDraftLinePackagingSnapshot>();

        if (document.SupplierId is not { } supplierId)
            return snapshots;

        foreach (var line in document.Lines)
        {
            if (line.ItemId is not { } itemId || string.IsNullOrWhiteSpace(line.SupplierCode))
                continue;

            var match = await itemRepo.GetSupplierCodeMatchAsync(
                supplierId,
                line.SupplierCode,
                document.TenantId,
                cancellationToken
            );
            if (match is null || match.ItemId != itemId)
                continue;

            if (
                match.PackagingLevelId is { } packagingLevelId
                && !string.IsNullOrWhiteSpace(match.PackagingUomCode)
                && match.PackagingBaseQuantity is > 0m
            )
            {
                snapshots[line.Id] = new PurchaseDraftLinePackagingSnapshot(
                    packagingLevelId,
                    match.PackagingUomCode,
                    match.BaseUomCode,
                    match.PackagingBaseQuantity.Value
                );
                continue;
            }

            snapshots[line.Id] = PurchaseDraftLinePackagingSnapshot.Base(match.BaseUomCode);
        }

        return snapshots;
    }
}
