using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;

namespace ERP.Application.Tests.TestSupport;

/// <summary>Atajo de prueba: nueva distribución externa vía la operación canónica.</summary>
internal static class PurchaseCostDistributionTestExtensions
{
    public static Guid Distribute(this PurchaseInvoice invoice, PurchaseCostType costType, decimal amount,
        IReadOnlyCollection<Guid> targetLineIds, Guid userId)
    {
        var id = Guid.NewGuid();
        invoice.ReplaceCostDistribution(new PurchaseCostDistributionChange(id, 0, costType, amount, targetLineIds, null),
            ProductNatures(invoice), userId);
        return id;
    }

    /// <summary>Destinos externos: cada Item de la compra se trata como Product (regla del dominio).</summary>
    public static IReadOnlyDictionary<Guid, ItemNature> ProductNatures(PurchaseInvoice invoice) =>
        invoice.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct()
            .ToDictionary(id => id, _ => ItemNature.Product);
}
