using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// PURCHASE-RETURN-DETAIL-DISPLAY-NAMES-01 — GetPurchaseReturnByIdHandler es la única fuente de la
/// pantalla de detalle de devolución; enriquece SKU/nombre de producto, nombre de bodega y
/// número/clave de la NC vinculada — nunca inventa datos (queda en null si no se pudo resolver) y
/// nunca toca lógica de autorización/cancelación/reversa (Map.ToDto, compartido con esos handlers,
/// permanece intacto).
/// </summary>
public sealed class GetPurchaseReturnByIdHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    private sealed class Mocks
    {
        public Mock<IPurchaseReturnRepository> ReturnRepo { get; } = new();
        public Mock<IItemRepository> ItemRepo { get; } = new();
        public Mock<IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<IPurchaseReceptionDocumentRepository> ReceptionRepo { get; } = new();
        public Mock<IPurchaseInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IPurchaseCreditNoteRepository> CreditNoteRepo { get; } = new();

        public Mocks()
        {
            InvoiceRepo
                .Setup(r =>
                    r.GetJournalSourceSummariesByIdsAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<IReadOnlyCollection<Guid>>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    new Dictionary<Guid, (string InvoiceNumber, string SupplierName, string Status, DateOnly IssueDate)>()
                );
            CreditNoteRepo
                .Setup(r =>
                    r.GetByLinkedPurchaseReturnIdAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((PurchaseCreditNote?)null);
        }

        public GetPurchaseReturnByIdHandler BuildHandler() =>
            new(
                ReturnRepo.Object,
                ItemRepo.Object,
                WarehouseRepo.Object,
                ReceptionRepo.Object,
                InvoiceRepo.Object,
                CreditNoteRepo.Object,
                FixedTenant()
            );
    }

    private static ICurrentTenant FixedTenant()
    {
        var m = new Mock<ICurrentTenant>();
        m.SetupGet(x => x.TenantId).Returns(TenantId);
        return m.Object;
    }

    private static PurchaseReturn BuildAuthorizedReturn(Guid? itemId = null)
    {
        var invoiceId = Guid.NewGuid();
        var ret = PurchaseReturn.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            invoiceId,
            SupplierId,
            "Producto en mal estado",
            new[]
            {
                new PurchaseReturn.DraftLineInput(Guid.NewGuid(), itemId ?? ItemId, 2m, WarehouseId),
            },
            UserId,
            Guid.NewGuid(),
            "create-hash"
        );
        return ret;
    }

    private static Item BuildItem(string sku, string shortName) =>
        Item.Create(
            TenantId,
            sku,
            shortName,
            "Descripción de prueba",
            Guid.NewGuid(),
            "UNIT",
            ItemTaxConfig.Create(saleVatCode: "10", purchaseVatCode: "10"),
            ItemSaleConfig.Create(isForSale: true),
            ItemStockConfig.Create(tracksStock: true),
            UserId
        );

    private static Warehouse BuildWarehouse(string name) =>
        Warehouse.Create(
            TenantId,
            BranchId,
            name,
            "BOD-01",
            null, null, null, null, null, null, null, null, null,
            UserId,
            CompanyId,
            isMain: true
        );

    [Fact]
    public async Task Handle_resuelve_SKU_nombre_de_producto_y_nombre_de_bodega_de_las_lineas()
    {
        var item = BuildItem("SKU-001", "Producto de prueba");
        var purchaseReturn = BuildAuthorizedReturn(item.Id);
        var m = new Mocks();
        m.ReturnRepo
            .Setup(r => r.GetByIdAsync(TenantId, purchaseReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseReturn);
        m.ItemRepo
            .Setup(r =>
                r.GetByIdsLightAsync(
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(item.Id)),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { item });
        var warehouse = BuildWarehouse("Bodega Principal");
        m.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, WarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouse);

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new GetPurchaseReturnByIdQuery(purchaseReturn.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        var line = result.Value!.Lines.Should().ContainSingle().Which;
        line.ItemSku.Should().Be("SKU-001");
        line.ItemName.Should().Be("Producto de prueba");
        line.WarehouseName.Should().Be("Bodega Principal");
        // Nunca reemplaza los Ids crudos — solo agrega los campos de presentación.
        line.ItemId.Should().Be(item.Id);
        line.WarehouseId.Should().Be(WarehouseId);
    }

    [Fact]
    public async Task Handle_deja_en_null_SKU_nombre_y_bodega_si_no_se_pudieron_resolver()
    {
        var purchaseReturn = BuildAuthorizedReturn();
        var m = new Mocks();
        m.ReturnRepo
            .Setup(r => r.GetByIdAsync(TenantId, purchaseReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseReturn);
        m.ItemRepo
            .Setup(r =>
                r.GetByIdsLightAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<Item>());
        m.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, WarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Warehouse?)null);

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new GetPurchaseReturnByIdQuery(purchaseReturn.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        var line = result.Value!.Lines.Should().ContainSingle().Which;
        line.ItemSku.Should().BeNull();
        line.ItemName.Should().BeNull();
        line.WarehouseName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_resuelve_numero_y_clave_de_la_NC_vinculada()
    {
        var purchaseReturn = BuildAuthorizedReturn();
        purchaseReturn.Authorize(
            "00000001",
            new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>
            {
                [purchaseReturn.Lines[0].OriginalInvoiceDetailId] = new PurchaseReturn.OriginalLineSnapshot(
                    10m, 100m, 0m, 15m, 0m, "10", 15m, null, 0m, 10m,
                    Array.Empty<PurchaseReturn.OriginalLineTaxSnapshot>()
                ),
            },
            balanceDueBeforeApplication: 100m,
            currencyCode: "USD",
            hasIssuedRetention: false,
            UserId,
            Guid.NewGuid(),
            "authorize-hash"
        );

        var receptionDoc = PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.CreditNote,
            "1710034065001",
            "Proveedor Test",
            SupplierId,
            "AK-12345",
            "001-001-000000099",
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateTime.UtcNow,
            100m,
            15m,
            115m,
            UserId
        );
        purchaseReturn.LinkSupplierCreditNote(receptionDoc.Id, UserId, Guid.NewGuid(), "link-hash");

        var m = new Mocks();
        m.ReturnRepo
            .Setup(r => r.GetByIdAsync(TenantId, purchaseReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseReturn);
        m.ItemRepo
            .Setup(r =>
                r.GetByIdsLightAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<Item>());
        m.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, WarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Warehouse?)null);
        m.ReceptionRepo
            .Setup(r => r.GetByIdAsync(TenantId, receptionDoc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receptionDoc);

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new GetPurchaseReturnByIdQuery(purchaseReturn.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.SupplierCreditNoteInvoiceNumber.Should().Be("001-001-000000099");
        result.Value.SupplierCreditNoteAccessKey.Should().Be("AK-12345");
        result.Value.SupplierCreditNoteIssueDate.Should().Be(receptionDoc.IssueDate);
        result.Value.SupplierCreditNoteAuthorizationDate.Should().Be(receptionDoc.AuthorizationDate);
        result.Value.SupplierCreditNoteTotalAmount.Should().Be(115m);
    }

    [Fact]
    public async Task Handle_resuelve_el_numero_de_la_factura_afectada()
    {
        var purchaseReturn = BuildAuthorizedReturn();
        var m = new Mocks();
        m.ReturnRepo
            .Setup(r => r.GetByIdAsync(TenantId, purchaseReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseReturn);
        m.ItemRepo
            .Setup(r =>
                r.GetByIdsLightAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<Item>());
        m.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, WarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Warehouse?)null);
        m.InvoiceRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(purchaseReturn.PurchaseInvoiceId)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (string InvoiceNumber, string SupplierName, string Status, DateOnly IssueDate)>
                {
                    [purchaseReturn.PurchaseInvoiceId] = ("001-001-000000042", "Proveedor Test", "Confirmed", DateOnly.FromDateTime(DateTime.UtcNow)),
                }
            );

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new GetPurchaseReturnByIdQuery(purchaseReturn.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.PurchaseInvoiceNumber.Should().Be("001-001-000000042");
    }

    [Fact]
    public async Task Handle_resuelve_la_PurchaseCreditNote_interna_vinculada_si_existe()
    {
        var purchaseReturn = BuildAuthorizedReturn();
        var creditNote = PurchaseCreditNote.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            purchaseReturn.PurchaseInvoiceId,
            receptionDocumentId: null,
            ERP.Domain.Modules.Purchases.Enums.PurchaseCreditNoteApplicationType.Return,
            "005-001-000000010",
            accessKey: null,
            authorizationNumber: null,
            authorizationDate: null,
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            reason: "Devolución de producto",
            lines: new[]
            {
                new PurchaseCreditNote.DraftLineInput("Línea libre", 100m, "10", 15m, 15m),
            },
            taxSummaryLines: Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
            UserId,
            Guid.NewGuid(),
            "cn-create-hash"
        );
        creditNote.LinkPurchaseReturn(purchaseReturn.Id, UserId);

        var m = new Mocks();
        m.ReturnRepo
            .Setup(r => r.GetByIdAsync(TenantId, purchaseReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseReturn);
        m.ItemRepo
            .Setup(r =>
                r.GetByIdsLightAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<Item>());
        m.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, WarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Warehouse?)null);
        m.CreditNoteRepo
            .Setup(r =>
                r.GetByLinkedPurchaseReturnIdAsync(TenantId, purchaseReturn.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(creditNote);

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new GetPurchaseReturnByIdQuery(purchaseReturn.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.LinkedPurchaseCreditNoteId.Should().Be(creditNote.Id);
        result.Value.LinkedPurchaseCreditNoteStatus.Should().Be("Draft");
    }

    [Fact]
    public async Task Handle_no_llama_al_repositorio_de_recepcion_cuando_no_hay_NC_vinculada()
    {
        var purchaseReturn = BuildAuthorizedReturn();
        var m = new Mocks();
        m.ReturnRepo
            .Setup(r => r.GetByIdAsync(TenantId, purchaseReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseReturn);
        m.ItemRepo
            .Setup(r =>
                r.GetByIdsLightAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<Item>());
        m.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, WarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Warehouse?)null);

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new GetPurchaseReturnByIdQuery(purchaseReturn.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.SupplierCreditNoteInvoiceNumber.Should().BeNull();
        result.Value.SupplierCreditNoteAccessKey.Should().BeNull();
        m.ReceptionRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
