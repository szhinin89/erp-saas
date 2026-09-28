using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.ResolveLines;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.Models;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Inventory.ItemMatching;

/// <summary>COMPRAS-METODO-ZH-01B — reglas del lote que se validan antes de tocar la base de datos.</summary>
public sealed class ResolvePurchaseReceptionLinesHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IPurchaseReceptionDocumentRepository> Documents { get; } = new();
        public Mock<IItemRepository> Items { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IRuntimePermissionAuthorizer> Authorizer { get; } = new();
        public PurchaseReceptionDocument Document { get; }
        public PurchaseReceptionLine LineA { get; }
        public PurchaseReceptionLine LineB { get; }
        /// <summary>Same supplier code as <see cref="LineA"/> (the product repeated in the invoice).</summary>
        public PurchaseReceptionLine LineC { get; }

        public Fixture(PurchaseReceptionDocumentStatus? status = null, Guid? branchId = null, Guid? companyId = null)
        {
            Document = PurchaseReceptionDocument.Create(TenantId, companyId ?? CompanyId, branchId ?? BranchId,
                PurchaseReceptionSourceDocType.Invoice, "1791352688001", "Proveedor", SupplierId,
                new string('1', 49), "001-001-000000001", new DateOnly(2026, 9, 27), null, 20m, 0m, 20m, UserId);
            LineA = Line("COD-A");
            LineB = Line("COD-B");
            LineC = Line("COD-A");
            Document.AttachSriAuthorization("1", DateTime.UtcNow, "<factura/>", DateTime.UtcNow, [LineA, LineB, LineC],
                UserId, "01", null, new PurchaseReceptionProcessingOutcome(PurchaseReceptionProcessingStatus.Processed, 3, 3, null));
            if (status == PurchaseReceptionDocumentStatus.Cancelled)
                Document.Cancel(UserId);
            foreach (var line in Document.Lines)
                Documents.Setup(d => d.GetByLineIdAsync(TenantId, line.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Document);
            Authorizer.Setup(a => a.IsAuthorizedAsync(It.IsAny<string>(), UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        private PurchaseReceptionLine Line(string code) =>
            PurchaseReceptionLine.Create(Document.Id, TenantId, $"XML {code}", 1m, 10m, "0", "2", 0m, 0m, 0m, 0m,
                10m, 10m, supplierCode: code);

        public ResolvePurchaseReceptionLinesHandler Build() =>
            new(Documents.Object, Items.Object, Mock.Of<IItemMatchConfirmationService>(),
                Mock.Of<IPurchaseReceptionAutoMatcher>(), [new CreateItemCommandValidator()], Mock.Of<IMediator>(),
                UnitOfWork.Object, Authorizer.Object, Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId), Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId && u.Role == "Compras"),
                Mock.Of<ERP.Application.Common.Persistence.IDatabaseExceptionTranslator>());
    }

    private static ResolveReceptionNewItemInput NewItem(string key = "n1") =>
        new(key, $"SKU-{key}", "Producto", "Producto nuevo", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "19",
            $"BAR-{key}", "Internal", "0", "0", null, 2m);

    private static Item StockItem()
    {
        var item = Item.Create(TenantId, "EX-1", "Existente", "Existente", Guid.NewGuid(), "19",
            ItemTaxConfig.Create("0", "0"), ItemSaleConfig.Create(true), ItemStockConfig.Create(true), UserId);
        return item;
    }

    [Fact]
    public async Task Document_outside_active_company_is_rejected()
    {
        var f = new Fixture(companyId: Guid.NewGuid());
        var result = await f.Build().Handle(new([NewItem()], [new(f.LineA.Id, NewItemKey: "n1")]), CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        f.UnitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Foreign_tenant_line_is_not_found_through_scoped_repository()
    {
        var f = new Fixture();
        var foreignLine = Guid.NewGuid();
        var result = await f.Build().Handle(new([NewItem()], [new(foreignLine, NewItemKey: "n1")]), CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        f.Documents.Verify(d => d.GetByLineIdAsync(TenantId, foreignLine, It.IsAny<CancellationToken>()), Times.Once);
        f.UnitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Duplicate_line_is_rejected_before_transaction()
    {
        var f = new Fixture();
        var result = await f.Build().Handle(new([NewItem()], [new(f.LineA.Id, NewItemKey: "n1"), new(f.LineA.Id, NewItemKey: "n1")]), CancellationToken.None);
        result.Value!.Applied.Should().BeFalse();
        result.Value.Errors.Should().Contain(e => e.PurchaseReceptionLineId == f.LineA.Id);
        f.UnitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Creating_products_requires_items_create_permission()
    {
        var f = new Fixture();
        f.Authorizer.Setup(a => a.IsAuthorizedAsync(InventoryPermissions.ItemsCreate, UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await f.Build().Handle(new ResolvePurchaseReceptionLinesCommand([NewItem()],
            [new(f.LineA.Id, NewItemKey: "n1")]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.UnitOfWork.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Document_outside_active_branch_or_not_usable_is_rejected(bool otherBranch)
    {
        var f = otherBranch ? new Fixture(branchId: Guid.NewGuid()) : new Fixture(PurchaseReceptionDocumentStatus.Cancelled);

        var result = await f.Build().Handle(new ResolvePurchaseReceptionLinesCommand([NewItem()],
            [new(f.LineA.Id, NewItemKey: "n1")]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.UnitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Every_invalid_row_is_reported_before_anything_executes()
    {
        var f = new Fixture();
        var existing = StockItem();
        f.Items.Setup(i => i.GetByIdLightAsync(existing.Id, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        f.Items.Setup(i => i.GetSupplierCodeMatchAsync(SupplierId, "COD-B", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItemSupplierCodeMatch(Guid.NewGuid(), null, null, null, "19"));
        var orphan = NewItem("orphan");

        var result = await f.Build().Handle(new ResolvePurchaseReceptionLinesCommand(
            [NewItem("n1") with { BaseSalePrice = 0m }, orphan],
            [
                new(f.LineA.Id, ItemId: existing.Id),          // stock item without presentation
                new(f.LineB.Id, NewItemKey: "n1"),             // code already linked to another item
                new(Guid.NewGuid(), ItemId: existing.Id),      // line from another document
            ]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var outcome = result.Value!;
        outcome.Applied.Should().BeFalse();
        outcome.Errors.Should().Contain(e => e.PurchaseReceptionLineId == f.LineA.Id && e.Message.Contains("presentación"));
        outcome.Errors.Should().Contain(e => e.PurchaseReceptionLineId == f.LineB.Id && e.Message.Contains("Vincular"));
        outcome.Errors.Should().Contain(e => e.Message.Contains("no pertenece a este documento"));
        outcome.Errors.Should().Contain(e => e.NewItemKey == "n1" && e.Message.Contains("precio de venta"));
        outcome.Errors.Should().Contain(e => e.NewItemKey == "orphan" && e.Message.Contains("ninguna línea"));
        f.UnitOfWork.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Same_supplier_code_cannot_map_to_different_presentations_in_one_batch()
    {
        var f = new Fixture();

        var result = await f.Build().Handle(new ResolvePurchaseReceptionLinesCommand([NewItem()],
            [
                new(f.LineA.Id, NewItemKey: "n1"),
                new(f.LineC.Id, NewItemKey: "n1", PresentationFactor: 12m, PresentationName: "Caja x12", PresentationUomCode: "02"),
            ]), CancellationToken.None);

        result.Value!.Applied.Should().BeFalse();
        result.Value.Errors.Should().ContainSingle(e => e.Message.Contains("COD-A"));
    }
}
