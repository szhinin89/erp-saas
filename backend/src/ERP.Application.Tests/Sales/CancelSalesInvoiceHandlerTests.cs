using ERP.Application.Common;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// SALES-PRESENTATIONS-02: CancelSalesInvoiceHandler es el cuarto punto de kardex (junto a los dos
/// de AuthorizeSalesUseCases y el de AuthorizeSalesReturnUseCases) que debe revertir stock en
/// QuantityInBaseUom/BaseUomCode — nunca Quantity/UomCode crudos, o la cancelación de una venta con
/// presentación (ej. cajas) desincroniza el stock físico (hallazgo de la auditoría 01B).
/// </summary>
public sealed class CancelSalesInvoiceHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();

    private static (
        CancelSalesInvoiceHandler Handler,
        Mock<IStockRepository> StockRepo,
        SalesInvoice Invoice
    ) BuildHandler(
        decimal quantity,
        decimal conversionFactor,
        Guid itemId,
        Guid warehouseId,
        Guid? activeBranchId = null
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);

        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "001-001-000000050",
            issueDate: new DateOnly(2026, 7, 25),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Caja x12",
            quantity: quantity,
            unitPrice: 120m,
            vatCode: "0",
            uomCode: "CAJA",
            itemId: itemId,
            warehouseId: warehouseId,
            conversionFactor: conversionFactor,
            baseUomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, UserId);

        var payment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            line.TaxInclusiveTotal
        );
        inv.ReplacePayments(new[] { payment }, UserId);
        inv.Authorize(UserId);

        var repo = new Mock<ISalesInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);

        var receivableRepo = new Mock<ISalesReceivableRepository>();
        receivableRepo
            .Setup(r => r.GetByInvoiceIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Modules.Sales.Entities.SalesReceivable?)null);

        var stockRepo = new Mock<IStockRepository>();
        stockRepo
            .Setup(s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var edocRepo = new Mock<IElectronicDocumentRepository>();
        edocRepo
            .Setup(e => e.GetBySourceAsync(TenantId, "Sales", inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ElectronicDocument?)null);

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(activeBranchId ?? BranchId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var handler = new CancelSalesInvoiceHandler(
            repo.Object,
            receivableRepo.Object,
            stockRepo.Object,
            edocRepo.Object,
            tenant.Object,
            company.Object,
            branch.Object,
            user.Object
        );

        return (handler, stockRepo, inv);
    }

    [Fact]
    public async Task Presentation_revierte_stock_en_QuantityInBaseUom_no_en_Quantity_cruda()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var (handler, stockRepo, inv) = BuildHandler(
            quantity: 2m,
            conversionFactor: 12m,
            itemId: itemId,
            warehouseId: warehouseId
        );

        var result = await handler.Handle(
            new CancelSalesInvoiceCommand(inv.Id, "Motivo de prueba"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    itemId,
                    warehouseId,
                    StockMovementType.SaleReturn,
                    24m, // QuantityInBaseUom (2 CAJA * 12), nunca 2 (Quantity cruda)
                    "UNIT", // BaseUomCode, nunca "CAJA" (UomCode de la presentación vendida)
                    It.IsAny<DateOnly>(),
                    It.IsAny<string>(),
                    inv.Id,
                    "SalesInvoice",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Sin_presentacion_preserva_comportamiento_actual()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var (handler, stockRepo, inv) = BuildHandler(
            quantity: 5m,
            conversionFactor: 1m,
            itemId: itemId,
            warehouseId: warehouseId
        );

        var result = await handler.Handle(
            new CancelSalesInvoiceCommand(inv.Id, "Motivo de prueba"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    itemId,
                    warehouseId,
                    StockMovementType.SaleReturn,
                    5m,
                    "UNIT",
                    It.IsAny<DateOnly>(),
                    It.IsAny<string>(),
                    inv.Id,
                    "SalesInvoice",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid?>()
                ),
            Times.Once
        );
    }

    /// <summary>
    /// Hallazgo ALTO auditoría de aislamiento (Sales/Purchases cross-branch): CancelSalesInvoiceCommand
    /// está marcado IBranchScopedRequest, pero ese marker solo exige sucursal activa autorizada — no
    /// garantiza que la factura cargada pertenezca a esa sucursal. El handler debe rechazar con
    /// NotFound (nunca revelar existencia cross-branch) cuando la factura pertenece a otra sucursal.
    /// </summary>
    [Fact]
    public async Task Factura_de_otra_sucursal_retorna_NotFound_y_no_la_anula()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var otherBranchId = Guid.NewGuid();
        var (handler, stockRepo, inv) = BuildHandler(
            quantity: 5m,
            conversionFactor: 1m,
            itemId: itemId,
            warehouseId: warehouseId,
            activeBranchId: otherBranchId
        );

        var result = await handler.Handle(
            new CancelSalesInvoiceCommand(inv.Id, "Motivo de prueba"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ERP.Application.Common.ApiResponseCodes.Common.NotFound);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<StockMovementType>(),
                    It.IsAny<decimal>(),
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid?>()
                ),
            Times.Never
        );
    }

    /// <summary>Misma factura, sucursal activa correcta (BranchId por defecto) — debe seguir funcionando.</summary>
    [Fact]
    public async Task Factura_de_la_misma_sucursal_sigue_anulandose_correctamente()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var (handler, _, inv) = BuildHandler(
            quantity: 5m,
            conversionFactor: 1m,
            itemId: itemId,
            warehouseId: warehouseId
        );

        var result = await handler.Handle(
            new CancelSalesInvoiceCommand(inv.Id, "Motivo de prueba"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Cancelled);
    }
}
