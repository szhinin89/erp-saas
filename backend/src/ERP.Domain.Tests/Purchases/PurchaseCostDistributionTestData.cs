using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;

namespace ERP.Domain.Tests.Purchases;

/// <summary>Atajos de prueba sobre la operación canónica <see cref="PurchaseInvoice.ReplaceCostDistribution"/>.</summary>
internal static class PurchaseCostDistributionTestData
{
    public static PurchaseInvoice Draft(Guid tenantId, Guid userId) =>
        PurchaseInvoice.CreateDraft(tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Proveedor Test",
            "1791352688001", "01", $"001-001-{Random.Shared.Next(100000000, 999999999)}",
            new DateOnly(2026, 10, 1), userId, Guid.NewGuid(), "Contado", 1, 0);

    /// <summary>Asocia una línea suelta a un borrador para poder distribuirle costos.</summary>
    public static PurchaseInvoice InDraft(this PurchaseInvoiceDetail line, Guid userId)
    {
        var invoice = Draft(line.TenantId, userId);
        invoice.ReplaceLines([line], userId);
        return invoice;
    }

    /// <summary>Nueva distribución externa (identidad nueva, revisión 0).</summary>
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
