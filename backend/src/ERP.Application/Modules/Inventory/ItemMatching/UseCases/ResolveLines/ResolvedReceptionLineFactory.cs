using ERP.Application.Modules.Inventory.ItemMatching.Mapping;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.ResolveLines;

/// <summary>
/// Estado final de una línea XML vinculada (producto + presentación + conversión) — único
/// constructor de <see cref="ResolvedReceptionLineDto"/>, compartido por la resolución masiva y la
/// corrección de relación. Sin presentación explícita usa la aprendida para Proveedor + Código.
/// </summary>
internal static class ResolvedReceptionLineFactory
{
    /// <returns><c>null</c> si el producto ya no existe.</returns>
    public static async Task<ResolvedReceptionLineDto?> BuildAsync(
        IItemRepository items,
        PurchaseReceptionDocument document,
        PurchaseReceptionLine line,
        Guid itemId,
        Guid? packagingLevelId,
        bool learned,
        CancellationToken ct
    )
    {
        var item = await items.GetByIdAsync(itemId, document.TenantId, ct);
        if (item is null)
            return null;
        var effectiveLevelId =
            packagingLevelId
            ?? (
                document.SupplierId is { } supplierId
                && !string.IsNullOrWhiteSpace(line.SupplierCode)
                    ? (
                        await items.GetSupplierCodeMatchAsync(
                            supplierId,
                            line.SupplierCode,
                            document.TenantId,
                            ct
                        )
                    )?.PackagingLevelId
                    : null
            );
        var level = item.PackagingLevels.FirstOrDefault(p => p.Id == effectiveLevelId);
        return new ResolvedReceptionLineDto(
            line.Id,
            item.Id,
            item.Code.SKU,
            item.Code.ShortName,
            ItemMatchingMapper.ToStatusCode(line.MatchStatus),
            level?.Id,
            level?.UomCode ?? item.DefaultUomCode,
            item.DefaultUomCode,
            level?.BaseQuantity ?? 1m,
            learned
        );
    }
}
