using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// Fase 2 (Item Matching en Compras) — PurchaseInvoice.Confirm() rechaza líneas que vienen de
/// Recepción Electrónica (PurchaseReceptionLineId != null) y aún no tienen ItemId resuelto, para
/// no generar Inventario/Kardex/CxP/Contabilidad con un vínculo de producto sin resolver. Líneas
/// manuales (PurchaseReceptionLineId == null) sin ItemId siguen permitidas — comportamiento previo.
/// </summary>
public sealed class PurchaseInvoiceConfirmMatchingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid WhId = Guid.NewGuid();
    private static readonly Guid PtId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

    private static PurchaseInvoice CreateDraftInvoice(bool withWarehouse = true) =>
        PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Contado",
            1,
            30,
            globalWarehouseId: withWarehouse ? WhId : null
        );

    [Fact]
    public void Confirm_rejects_a_reception_linked_line_without_item()
    {
        var inv = CreateDraftInvoice();
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto sin vincular",
            quantity: 1,
            unitPrice: 10m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: null,
            warehouseId: WhId,
            purchaseReceptionLineId: Guid.NewGuid()
        );
        inv.ReplaceLines([line], UserId);

        var act = () => inv.Confirm(UserId);

        act.Should()
            .Throw<DomainRuleViolationException>()
            .WithMessage("*productos pendientes de vinculación*Producto sin vincular*");
    }

    [Theory]
    [InlineData(ItemNature.Service, false)]
    [InlineData(ItemNature.Product, true)]
    [InlineData(null, true)]
    public void Warehouse_guard_uses_explicit_nature_and_preserves_physical_guard(
        ItemNature? nature, bool requiresWarehouse)
    {
        var inv = CreateDraftInvoice(withWarehouse: false);
        inv.ReplaceLines([PurchaseInvoiceDetail.Create(inv.Id, TenantId, "Línea XML", 1m,
            10m, "4", "UNIT", itemId: ItemId, purchaseReceptionLineId: Guid.NewGuid())], UserId);
        var natures = new Dictionary<Guid, ItemNature>();
        if (nature.HasValue)
            natures.Add(ItemId, nature.Value);

        var act = () => inv.Confirm(UserId, natures);
        if (requiresWarehouse)
        {
            act.Should().Throw<DomainRuleViolationException>().WithMessage("*bodega destino*");
            inv.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft);
            inv.Lines.Single().IsFrozen.Should().BeFalse();
        }
        else
        {
            act.Should().NotThrow();
            inv.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed);
        }
    }

    [Fact]
    public void Confirm_allows_a_reception_linked_line_once_item_is_resolved()
    {
        var inv = CreateDraftInvoice();
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto vinculado",
            quantity: 1,
            unitPrice: 10m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: ItemId,
            warehouseId: WhId,
            purchaseReceptionLineId: Guid.NewGuid()
        );
        inv.ReplaceLines([line], UserId);

        var act = () => inv.Confirm(UserId);

        act.Should().NotThrow();
        inv.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed);
    }

    [Fact]
    public void Confirm_allows_a_manual_line_without_item_exactly_like_before()
    {
        var inv = CreateDraftInvoice();
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Servicio manual sin ítem",
            quantity: 1,
            unitPrice: 10m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: null,
            purchaseReceptionLineId: null
        );
        inv.ReplaceLines([line], UserId);

        var act = () => inv.Confirm(UserId);

        act.Should().NotThrow();
    }
}
