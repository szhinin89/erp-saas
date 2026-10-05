using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

namespace ERP.Application.Modules.Inventory.ItemMatching.Services;

/// <summary>
/// COMPRAS-METODO-ZH-01B — re-evaluates the still unresolved lines of an already processed
/// reception against the supplier-code equivalences (<c>ItemSupplierCode</c>) learned after its XML
/// was downloaded (e.g. resolved on another invoice of the same batch). Same rule as the automatic
/// resolution at download time (<c>PurchaseReceptionDetailProcessor</c>): exact supplier + code only,
/// never by description. Does not save; the caller owns the unit of work.
/// </summary>
public interface IPurchaseReceptionAutoMatcher
{
    /// <returns>Number of lines resolved automatically.</returns>
    Task<int> RefreshAsync(PurchaseReceptionDocument document, CancellationToken cancellationToken);
}

public sealed class PurchaseReceptionAutoMatcher(IItemRepository items)
    : IPurchaseReceptionAutoMatcher
{
    public async Task<int> RefreshAsync(
        PurchaseReceptionDocument document,
        CancellationToken cancellationToken
    )
    {
        if (document.SupplierId is not { } supplierId)
            return 0;

        var resolved = 0;
        foreach (var line in document.Lines)
        {
            if (line.ItemId is not null || string.IsNullOrWhiteSpace(line.SupplierCode))
                continue;

            var itemId = await items.FindItemIdBySupplierCodeAsync(
                supplierId,
                line.SupplierCode,
                document.TenantId,
                cancellationToken
            );
            if (itemId is not { } learned)
                continue;

            line.AutoMatch(learned);
            resolved++;
        }
        return resolved;
    }
}
