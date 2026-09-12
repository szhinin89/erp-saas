using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// FLOW-READY-02C.2 — <c>GetPurchaseCreditNoteByIdHandler</c>/<c>GetPurchaseCreditNoteListHandler</c>:
/// ambos resuelven exclusivamente con el <c>TenantId</c> de <c>ICurrentTenant</c> (nunca uno recibido
/// del cliente) — mismo criterio fail-closed multi-tenant que el resto del módulo Purchases.
/// </summary>
public sealed class PurchaseCreditNoteQueryUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private static ICurrentTenant FixedTenant(Guid tenantId)
    {
        var m = new Mock<ICurrentTenant>();
        m.SetupGet(x => x.TenantId).Returns(tenantId);
        return m.Object;
    }

    private static ICurrentCompany FixedCompany()
    {
        var m = new Mock<ICurrentCompany>();
        m.SetupGet(x => x.CompanyId).Returns(CompanyId);
        return m.Object;
    }

    private static PurchaseCreditNote SampleCreditNote() =>
        PurchaseCreditNote.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            Guid.NewGuid(),
            null,
            PurchaseCreditNoteApplicationType.Discount,
            "001-001-000000005",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Descuento",
            new[] { new PurchaseCreditNote.DraftLineInput("Descuento", 100m, "2", 15m, 15m) },
            Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash"
        );

    [Fact]
    public async Task GetById_consulta_el_repositorio_con_el_TenantId_del_contexto_autenticado()
    {
        var creditNote = SampleCreditNote();
        var repo = new Mock<IPurchaseCreditNoteRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, creditNote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditNote);
        var invoiceRepo = new Mock<IPurchaseInvoiceRepository>();
        var receptionRepo = new Mock<IPurchaseReceptionDocumentRepository>();

        var handler = new GetPurchaseCreditNoteByIdHandler(
            repo.Object,
            invoiceRepo.Object,
            Mock.Of<IAccountsPayableRepository>(),
            receptionRepo.Object,
            Mock.Of<IPurchaseReturnRepository>(),
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            FixedTenant(TenantId),
            FixedCompany()
        );

        var result = await handler.Handle(
            new GetPurchaseCreditNoteByIdQuery(creditNote.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        repo.Verify(
            r => r.GetByIdAsync(TenantId, creditNote.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task GetById_nunca_consulta_con_un_TenantId_distinto_al_del_contexto()
    {
        var creditNote = SampleCreditNote();
        var otroTenantId = Guid.NewGuid();
        var repo = new Mock<IPurchaseCreditNoteRepository>();
        // Solo configurado para el tenant "correcto" — cualquier otra clave retorna null por
        // defecto de Moq, simulando el aislamiento fail-closed real del query filter de EF.
        repo.Setup(r => r.GetByIdAsync(TenantId, creditNote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditNote);
        var invoiceRepo = new Mock<IPurchaseInvoiceRepository>();
        var receptionRepo = new Mock<IPurchaseReceptionDocumentRepository>();

        var handler = new GetPurchaseCreditNoteByIdHandler(
            repo.Object,
            invoiceRepo.Object,
            Mock.Of<IAccountsPayableRepository>(),
            receptionRepo.Object,
            Mock.Of<IPurchaseReturnRepository>(),
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            FixedTenant(otroTenantId),
            FixedCompany()
        );

        var result = await handler.Handle(
            new GetPurchaseCreditNoteByIdQuery(creditNote.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task GetById_enriquece_con_la_PurchaseReturn_vinculada_para_NC_tipo_Devolucion()
    {
        var invoice = ERP.Domain.Modules.Purchases.Entities.PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1710034065001",
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Contado",
            1,
            30
        );
        var item = ERP.Domain.Modules.Items.Entities.Item.Create(
            TenantId,
            "SKU-001",
            "Producto de prueba",
            "Descripción",
            Guid.NewGuid(),
            "UNIT",
            ERP.Domain.Modules.Items.ValueObjects.ItemTaxConfig.Create(saleVatCode: "0", purchaseVatCode: "0"),
            ERP.Domain.Modules.Items.ValueObjects.ItemSaleConfig.Create(isForSale: true),
            ERP.Domain.Modules.Items.ValueObjects.ItemStockConfig.Create(tracksStock: true),
            Guid.NewGuid()
        );
        var itemId = item.Id;
        var warehouseId = Guid.NewGuid();
        var invoiceLine = ERP.Domain.Modules.Purchases.Entities.PurchaseInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Producto 1",
            quantity: 5,
            unitPrice: 20m,
            vatCode: "0",
            uomCode: "UNIT",
            itemId: itemId,
            warehouseId: warehouseId
        );
        invoiceLine.ApplyTaxes("0", 0m, "IVA", null, 0m, null);
        invoice.ReplaceLines(new[] { invoiceLine }, Guid.NewGuid());
        invoice.Confirm(Guid.NewGuid());

        var purchaseReturn = PurchaseReturn.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            invoice.Id,
            SupplierId,
            "Producto en mal estado",
            new[] { new PurchaseReturn.DraftLineInput(invoiceLine.Id, itemId, 1m, warehouseId) },
            Guid.NewGuid(),
            Guid.NewGuid(),
            "return-create-hash"
        );

        var creditNote = PurchaseCreditNote.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            invoice.Id,
            receptionDocumentId: null,
            PurchaseCreditNoteApplicationType.Return,
            "005-001-000000010",
            accessKey: null,
            authorizationNumber: null,
            authorizationDate: null,
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            reason: "Devolución de producto",
            lines: new[]
            {
                new PurchaseCreditNote.DraftLineInput("Producto 1", 20m, "0", 0m, 0m, invoiceLine.Id, 1m),
            },
            taxSummaryLines: Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "cn-create-hash"
        );
        creditNote.LinkPurchaseReturn(purchaseReturn.Id, Guid.NewGuid());

        var repo = new Mock<IPurchaseCreditNoteRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, creditNote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditNote);
        var invoiceRepo = new Mock<IPurchaseInvoiceRepository>();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        var payableRepo = new Mock<IAccountsPayableRepository>();
        payableRepo
            .Setup(r =>
                r.GetByOriginAsync(
                    TenantId,
                    CompanyId,
                    ERP.Domain.Modules.Payables.Enums.AccountsPayableOriginType.PurchaseInvoice,
                    invoice.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ERP.Domain.Modules.Payables.Entities.AccountsPayable?)null);
        var receptionRepo = new Mock<IPurchaseReceptionDocumentRepository>();
        var returnRepo = new Mock<IPurchaseReturnRepository>();
        returnRepo
            .Setup(r => r.GetByIdAsync(TenantId, purchaseReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseReturn);
        var itemRepo = new Mock<IItemRepository>();
        itemRepo
            .Setup(r =>
                r.GetByIdsLightAsync(
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(itemId)),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { item });
        var warehouseRepo = new Mock<IWarehouseRepository>();
        var warehouse = ERP.Domain.Modules.Inventory.Entities.Warehouse.Create(
            TenantId, BranchId, "Bodega Principal", "BOD-01",
            null, null, null, null, null, null, null, null, null,
            Guid.NewGuid(), CompanyId, isMain: true
        );
        warehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, warehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouse);

        var handler = new GetPurchaseCreditNoteByIdHandler(
            repo.Object,
            invoiceRepo.Object,
            payableRepo.Object,
            receptionRepo.Object,
            returnRepo.Object,
            itemRepo.Object,
            warehouseRepo.Object,
            FixedTenant(TenantId),
            FixedCompany()
        );

        var result = await handler.Handle(
            new GetPurchaseCreditNoteByIdQuery(creditNote.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;
        dto.LinkedPurchaseReturnStatus.Should().Be("Draft");
        dto.LinkedPurchaseReturnNumber.Should().BeNull();
        var line = dto.Lines.Should().ContainSingle().Which;
        line.ItemSku.Should().Be("SKU-001");
        line.ItemName.Should().Be("Producto de prueba");
        line.WarehouseName.Should().Be("Bodega Principal");
    }

    [Fact]
    public async Task GetList_pagina_y_consulta_el_repositorio_con_el_TenantId_del_contexto()
    {
        var repo = new Mock<IPurchaseCreditNoteRepository>();
        repo.Setup(r =>
                r.GetPagedAsync(
                    TenantId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    1,
                    20,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((new List<PurchaseCreditNote> { SampleCreditNote() }, 1));

        var handler = new GetPurchaseCreditNoteListHandler(repo.Object, FixedTenant(TenantId));

        var result = await handler.Handle(new GetPurchaseCreditNoteListQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Total.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        repo.Verify(
            r =>
                r.GetPagedAsync(
                    TenantId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    1,
                    20,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
