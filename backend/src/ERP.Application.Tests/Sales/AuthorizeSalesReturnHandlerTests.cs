using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// P0-01 Fase 5 — AuthorizeSalesReturnHandler: autorización real (advisory lock + revalidación de
/// remanente bajo lock + Authorize() del dominio + reversión de inventario). No cubre Caja/CxC/
/// Accounting/ElectronicDocuments/Audit (fases posteriores).
/// </summary>
public sealed class AuthorizeSalesReturnHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();

    // ── Fixtures de dominio (in-memory, sin persistencia) ────────────────

    private static (SalesInvoice Invoice, List<SalesInvoiceDetail> Lines) BuildAuthorizedInvoice(
        params (
            string Description,
            decimal Quantity,
            decimal UnitPrice,
            Guid? ItemId,
            Guid? WarehouseId
        )[] lineSpecs
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
            invoiceNumber: "001-001-000000001",
            issueDate: new DateOnly(2026, 7, 25),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId
        );

        var lines = lineSpecs
            .Select(s =>
                SalesInvoiceDetail.Create(
                    inv.Id,
                    TenantId,
                    s.Description,
                    s.Quantity,
                    s.UnitPrice,
                    vatCode: "0",
                    uomCode: "UNIT",
                    itemId: s.ItemId,
                    warehouseId: s.WarehouseId
                )
            )
            .ToList();
        inv.ReplaceLines(lines, UserId);

        var total = lines.Sum(l => l.TaxInclusiveTotal);
        var payment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            total
        );
        inv.ReplacePayments(new[] { payment }, UserId);

        inv.Authorize(UserId);
        return (inv, inv.Lines.ToList());
    }

    private static SalesReturn BuildDraftReturn(
        Guid invoiceId,
        IEnumerable<(SalesInvoiceDetail OriginalLine, decimal Quantity)> lineSpecs,
        string returnNumber = "DEV-000001"
    )
    {
        var salesReturn = SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            invoiceId,
            CustomerId,
            returnNumber,
            "Producto en mal estado",
            UserId
        );
        foreach (var (originalLine, quantity) in lineSpecs)
        {
            salesReturn.AddLine(
                SalesReturnDetail.Create(
                    salesReturn.Id,
                    TenantId,
                    originalLine.Id,
                    originalLine.Description,
                    quantity,
                    originalLine.UnitPrice,
                    0m,
                    originalLine.VatCode,
                    originalLine.VatRate,
                    originalLine.UomCode,
                    originalLine.ItemId,
                    originalLine.WarehouseId
                ),
                UserId
            );
        }
        return salesReturn;
    }

    // ── Fase 6 — SalesReturnRefundHandler fixture (caja abierta por defecto) ────

    /// <summary>
    /// Construye un <see cref="SalesReturnRefundHandler"/> real (no es una interfaz, no se puede
    /// mockear) con una sesión de caja ABIERTA por defecto — las pruebas de Fase 5 ya migradas a
    /// este archivo usan <see cref="FullRefundCashCommand"/> (100% efectivo) y deben seguir
    /// autorizando con éxito.
    /// </summary>
    private static SalesReturnRefundHandler BuildRefundHandler(bool openCashSession = true) =>
        BuildRefundHandler(out _, openCashSession);

    private static SalesReturnRefundHandler BuildRefundHandler(
        out Mock<ICashSessionRepository> cashRepoMock,
        bool openCashSession = true,
        Domain.Modules.Sales.Entities.SalesReceivable? receivable = null
    )
    {
        CashSession? session = null;
        if (openCashSession)
        {
            session = CashSession.Open(
                TenantId,
                CompanyId,
                BranchId,
                UserId,
                Guid.NewGuid(),
                "CAJA-01",
                "Caja Principal",
                Guid.NewGuid(),
                "001",
                0m,
                UserId
            );
        }

        cashRepoMock = new Mock<ICashSessionRepository>();
        if (session is not null)
            cashRepoMock
                .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);

        var receivableRepo = new Mock<ISalesReceivableRepository>();
        if (receivable is not null)
            receivableRepo
                .Setup(r =>
                    r.GetByInvoiceIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(receivable);

        var cashSessionCtx = new Mock<ICurrentCashSession>();
        cashSessionCtx.Setup(c => c.HasOpenSession).Returns(session is not null);
        cashSessionCtx.Setup(c => c.CashSessionId).Returns(session?.Id);

        return new SalesReturnRefundHandler(
            cashRepoMock.Object,
            receivableRepo.Object,
            cashSessionCtx.Object
        );
    }

    // ── Mocks ──────────────────────────────────────────────────────────

    private static (
        AuthorizeSalesReturnHandler Handler,
        Mock<IStockRepository> StockRepo,
        Mock<ISalesReturnRepository> ReturnRepo
    ) BuildHandler(SalesInvoice invoice, SalesReturn salesReturn, decimal alreadyReturned = 0m) =>
        BuildHandler(invoice, salesReturn, out _, alreadyReturned);

    private static (
        AuthorizeSalesReturnHandler Handler,
        Mock<IStockRepository> StockRepo,
        Mock<ISalesReturnRepository> ReturnRepo
    ) BuildHandler(
        SalesInvoice invoice,
        SalesReturn salesReturn,
        out Mock<IPostingEngine> postingEngine,
        decimal alreadyReturned = 0m
    )
    {
        var invoiceRepo = new Mock<ISalesInvoiceRepository>();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var returnRepo = new Mock<ISalesReturnRepository>();
        returnRepo
            .Setup(r => r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(salesReturn);
        returnRepo
            .Setup(r =>
                r.AcquireReturnLockAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);
        returnRepo
            .Setup(r =>
                r.GetReturnedQuantityByInvoiceDetailAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(alreadyReturned);

        var stockRepo = new Mock<IStockRepository>();
        stockRepo
            .Setup(s =>
                s.AppendMovementAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<StockMovementType>(),
                    It.IsAny<decimal>(),
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid>(),
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((StockMovement?)null!);
        stockRepo
            .Setup(s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        postingEngine = new Mock<IPostingEngine>();
        postingEngine
            .Setup(p =>
                p.IsAmountKindConfiguredAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ERP.Domain.Modules.Accounting.Enums.PostingAmountKind>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);

        var handler = new AuthorizeSalesReturnHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            stockRepo.Object,
            BuildRefundHandler(),
            Mock.Of<ERP.Domain.Modules.Company.Interfaces.IDocumentSequenceRepository>(),
            Mock.Of<ERP.Domain.Modules.Company.Interfaces.IEmissionPointRepository>(),
            Mock.Of<ERP.Domain.Modules.Company.Interfaces.IEstablishmentRepository>(),
            Mock.Of<ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentIssuer>(),
            uow.Object,
            postingEngine.Object,
            TenantCtx(),
            CompanyCtx(),
            UserCtx(),
            Mock.Of<ILogger<AuthorizeSalesReturnHandler>>()
        );

        return (handler, stockRepo, returnRepo);
    }

    private static ICurrentTenant TenantCtx() =>
        Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);

    private static ICurrentCompany CompanyCtx() =>
        Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId);

    private static ICurrentUser UserCtx() => Mock.Of<ICurrentUser>(u => u.UserId == UserId);

    private static AuthorizeSalesReturnCommand FullRefundCashCommand(
        Guid salesReturnId,
        decimal amount
    ) =>
        new(
            salesReturnId,
            new List<AuthorizeSalesReturnRefundAllocationInput> { new("Cash", amount) }
        );

    // ══════════════════════════════ Autorización correcta ══════════════════════════════

    [Fact]
    public async Task Autoriza_una_devolucion_parcial()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m, null, null));
        var salesReturn = BuildDraftReturn(invoice.Id, new[] { (lines[0], 4m) });
        var (handler, _, _) = BuildHandler(invoice, salesReturn);

        var result = await handler.Handle(
            FullRefundCashCommand(salesReturn.Id, salesReturn.GrandTotal),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(SalesReturnStatus.Authorized.ToString());
        salesReturn.Status.Should().Be(SalesReturnStatus.Authorized);
    }

    [Fact]
    public async Task Devolucion_con_IRBPNR_sin_PostingRuleLine_configurada_bloquea_con_mensaje_claro()
    {
        // TAX-LINE-SSOT-ICE-IRBPNR-01 Fase 5E — mismo criterio que el guard de Compras: si hay
        // IRBPNR y no existe PostingRuleLine configurada, la autorización debe bloquear ANTES de
        // capturar el secuencial de NC/persistir efectos.
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto con IRBPNR", 10m, 5m, null, null));
        var salesReturn = BuildDraftReturn(invoice.Id, new[] { (lines[0], 4m) });
        var returnLine = salesReturn.Lines.Single();
        returnLine.ReplaceTaxes(
            [
                SalesReturnDetailTax.Create(
                    returnLine.Id,
                    TenantId,
                    "5",
                    "5001",
                    "IRBPNR",
                    0.1m,
                    ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Specific,
                    2m
                ),
            ]
        );

        var (handler, _, _) = BuildHandler(invoice, salesReturn, out var postingEngine);
        postingEngine
            .Setup(p =>
                p.IsAmountKindConfiguredAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    ERP.Domain.Modules.Accounting.Enums.PostingAmountKind.TaxIrbpnr,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);

        var result = await handler.Handle(
            FullRefundCashCommand(salesReturn.Id, salesReturn.GrandTotal),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("IRBPNR");
        salesReturn.Status.Should().Be(SalesReturnStatus.Draft);
    }

    [Fact]
    public async Task Autoriza_con_multiples_lineas()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(
            ("Producto A", 10m, 5m, null, null),
            ("Producto B", 4m, 20m, null, null)
        );
        var salesReturn = BuildDraftReturn(invoice.Id, new[] { (lines[0], 2m), (lines[1], 1m) });
        var (handler, stockRepo, _) = BuildHandler(invoice, salesReturn);

        var result = await handler.Handle(
            FullRefundCashCommand(salesReturn.Id, salesReturn.GrandTotal),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task Autoriza_con_multiples_bodegas_y_genera_un_movimiento_por_bodega()
    {
        var warehouseA = Guid.NewGuid();
        var warehouseB = Guid.NewGuid();
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        var (invoice, lines) = BuildAuthorizedInvoice(
            ("Producto A", 10m, 5m, itemA, warehouseA),
            ("Producto B", 4m, 20m, itemB, warehouseB)
        );
        var salesReturn = BuildDraftReturn(invoice.Id, new[] { (lines[0], 3m), (lines[1], 2m) });
        var (handler, stockRepo, _) = BuildHandler(invoice, salesReturn);

        var result = await handler.Handle(
            FullRefundCashCommand(salesReturn.Id, salesReturn.GrandTotal),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    itemA,
                    warehouseA,
                    StockMovementType.SaleReturn,
                    3m,
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    salesReturn.Id,
                    "SalesReturn",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    itemB,
                    warehouseB,
                    StockMovementType.SaleReturn,
                    2m,
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    salesReturn.Id,
                    "SalesReturn",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ══════════════════════════════ Inventario ══════════════════════════════

    [Fact]
    public async Task Genera_movimiento_SaleReturn_con_cantidad_positiva_y_documento_origen_correcto()
    {
        var warehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m, itemId, warehouseId));
        var salesReturn = BuildDraftReturn(invoice.Id, new[] { (lines[0], 4m) });
        var (handler, stockRepo, _) = BuildHandler(invoice, salesReturn);

        var result = await handler.Handle(
            FullRefundCashCommand(salesReturn.Id, salesReturn.GrandTotal),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    itemId,
                    warehouseId,
                    StockMovementType.SaleReturn,
                    4m, // cantidad positiva (ingreso), no negativa como en la venta
                    "UNIT",
                    It.IsAny<DateOnly>(),
                    $"DEV-{salesReturn.ReturnNumber}",
                    salesReturn.Id, // sourceDocId = SalesReturn, nunca SalesInvoice
                    "SalesReturn", // sourceDocType = "SalesReturn", nunca "SalesInvoice"
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task No_genera_movimiento_para_lineas_sin_item_ni_bodega()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Servicio A", 10m, 5m, null, null));
        var salesReturn = BuildDraftReturn(invoice.Id, new[] { (lines[0], 4m) });
        var (handler, stockRepo, _) = BuildHandler(invoice, salesReturn);

        var result = await handler.Handle(
            FullRefundCashCommand(salesReturn.Id, salesReturn.GrandTotal),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
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
                    It.IsAny<string?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid>(),
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // ══════════════════════════════ SALES-PRESENTATIONS-02 ══════════════════════════════

    [Fact]
    public async Task Presentation_reingresa_stock_en_QuantityInBaseUom_no_en_Quantity_cruda()
    {
        // Venta: 2 CAJA x12 (24 unidades base). Devolución: 1 CAJA x12 → debe reingresar 12
        // unidades base al stock, nunca "1" (Quantity cruda en la presentación devuelta).
        var warehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);
        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "001-001-000000099",
            issueDate: new DateOnly(2026, 7, 25),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId
        );
        var line = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Caja x12",
            quantity: 2m,
            unitPrice: 120m,
            vatCode: "0",
            uomCode: "CAJA",
            itemId: itemId,
            warehouseId: warehouseId,
            conversionFactor: 12m,
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
        var authorizedLine = inv.Lines.Single();

        var salesReturn = SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            inv.Id,
            CustomerId,
            "DEV-000099",
            "Producto en mal estado",
            UserId
        );
        salesReturn.AddLine(
            SalesReturnDetail.Create(
                salesReturn.Id,
                TenantId,
                authorizedLine.Id,
                authorizedLine.Description,
                quantity: 1m, // 1 CAJA devuelta — mantiene la misma presentación de la venta
                authorizedLine.UnitPrice,
                0m,
                authorizedLine.VatCode,
                authorizedLine.VatRate,
                authorizedLine.UomCode,
                authorizedLine.ItemId,
                authorizedLine.WarehouseId,
                packagingLevelId: authorizedLine.PackagingLevelId,
                conversionFactor: authorizedLine.ConversionFactor,
                baseUomCode: authorizedLine.BaseUomCode
            ),
            UserId
        );

        var (handler, stockRepo, _) = BuildHandler(inv, salesReturn);

        var result = await handler.Handle(
            FullRefundCashCommand(salesReturn.Id, salesReturn.GrandTotal),
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
                    12m, // QuantityInBaseUom (1 CAJA * 12), nunca 1 (Quantity cruda)
                    "UNIT", // BaseUomCode, nunca "CAJA" (UomCode de la presentación devuelta)
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    salesReturn.Id,
                    "SalesReturn",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ══════════════════════════════ Validaciones ══════════════════════════════

    [Fact]
    public async Task Rechaza_devolucion_inexistente()
    {
        var returnRepo = new Mock<ISalesReturnRepository>();
        returnRepo
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalesReturn?)null);

        var handler = new AuthorizeSalesReturnHandler(
            returnRepo.Object,
            Mock.Of<ISalesInvoiceRepository>(),
            Mock.Of<IStockRepository>(),
            BuildRefundHandler(),
            Mock.Of<ERP.Domain.Modules.Company.Interfaces.IDocumentSequenceRepository>(),
            Mock.Of<ERP.Domain.Modules.Company.Interfaces.IEmissionPointRepository>(),
            Mock.Of<ERP.Domain.Modules.Company.Interfaces.IEstablishmentRepository>(),
            Mock.Of<ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentIssuer>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IPostingEngine>(),
            TenantCtx(),
            CompanyCtx(),
            UserCtx(),
            Mock.Of<ILogger<AuthorizeSalesReturnHandler>>()
        );

        var result = await handler.Handle(
            FullRefundCashCommand(Guid.NewGuid(), 1m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Rechaza_devolucion_cancelada()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m, null, null));
        var salesReturn = BuildDraftReturn(invoice.Id, new[] { (lines[0], 4m) });
        salesReturn.Cancel(UserId);
        var (handler, _, _) = BuildHandler(invoice, salesReturn);

        var result = await handler.Handle(
            FullRefundCashCommand(salesReturn.Id, 4m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Rechaza_devolucion_ya_autorizada()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m, null, null));
        var salesReturn = BuildDraftReturn(invoice.Id, new[] { (lines[0], 4m) });
        salesReturn.AddRefundAllocation(
            SalesReturnRefundAllocation.Create(
                salesReturn.Id,
                TenantId,
                SalesReturnRefundMethod.Cash,
                salesReturn.GrandTotal
            ),
            UserId
        );
        salesReturn.Authorize(UserId);
        var (handler, _, _) = BuildHandler(invoice, salesReturn);

        var result = await handler.Handle(
            FullRefundCashCommand(salesReturn.Id, salesReturn.GrandTotal),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Rechaza_cantidad_que_excede_el_remanente_bajo_lock()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 5m, 10m, null, null));
        var salesReturn = BuildDraftReturn(invoice.Id, new[] { (lines[0], 3m) });
        // Bajo lock, otra devolución ya consumió 3 de las 5 unidades — remanente real = 2.
        var (handler, _, _) = BuildHandler(invoice, salesReturn, alreadyReturned: 3m);

        var result = await handler.Handle(
            FullRefundCashCommand(salesReturn.Id, salesReturn.GrandTotal),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("remanente");
    }

    [Fact]
    public async Task Rechaza_autorizacion_sin_asignaciones_de_reembolso()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m, null, null));
        var salesReturn = BuildDraftReturn(invoice.Id, new[] { (lines[0], 4m) });
        var (handler, _, _) = BuildHandler(invoice, salesReturn);

        var result = await handler.Handle(
            new AuthorizeSalesReturnCommand(
                salesReturn.Id,
                new List<AuthorizeSalesReturnRefundAllocationInput>()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        salesReturn
            .Status.Should()
            .Be(SalesReturnStatus.Draft, "no debe autorizarse sin reembolso");
    }

    [Fact]
    public async Task Rechaza_suma_de_asignaciones_distinta_al_total()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m, null, null));
        var salesReturn = BuildDraftReturn(invoice.Id, new[] { (lines[0], 4m) }); // total = 20
        var (handler, _, _) = BuildHandler(invoice, salesReturn);

        var result = await handler.Handle(
            FullRefundCashCommand(salesReturn.Id, 5m), // no coincide con GrandTotal (20)
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        salesReturn.Status.Should().Be(SalesReturnStatus.Draft);
    }

    // ══════════════════════════════ Concurrencia real (PostgreSQL) ══════════════════════════════

    /// <summary>
    /// Suite de integración con PostgreSQL 16 real (Testcontainers) — única forma de ejercitar
    /// genuinamente el advisory lock de <c>AcquireReturnLockAsync</c> (Fase 3) bajo concurrencia
    /// real. Reutiliza los repositorios de Infrastructure ya probados (<c>SalesReturnRepository</c>,
    /// <c>SalesInvoiceRepository</c>, <c>StockRepository</c>) y <c>UnitOfWork</c>, exactamente los
    /// mismos servicios de persistencia/transacción que usa <c>AuthorizeSalesInvoiceHandler</c> en
    /// producción. Requiere Docker.
    /// </summary>
    [Trait("Category", "PostgreSql")]
    public sealed class AuthorizeSalesReturnConcurrencyTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("erp_sales_return_authorize_test")
            .WithUsername("erp")
            .WithPassword("erp_test_secret")
            .Build();

        private Guid _tenantId;
        private Guid _companyId;
        private Guid _branchId;
        private Guid _customerId;
        private Guid _cashSessionId;
        private Guid _createdBy;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            await using var db = CreateContext();
            await db.Database.MigrateAsync();

            _createdBy = Guid.NewGuid();
            var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
            var company = Company.CreateManaged(
                tenant.Id,
                "1790012345001",
                "Test S.A.",
                createdBy: _createdBy
            );
            var branch = Branch.Create(
                tenant.Id,
                "Matriz",
                "Av. Principal 123",
                "001",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                true,
                _createdBy,
                companyId: company.Id
            );
            var customer = BusinessPartner.Create(
                tenant.Id,
                "05",
                "1710034065",
                1,
                "Cliente Test",
                _createdBy
            );
            var establishment = Establishment.Create(
                tenant.Id,
                branchId: branch.Id,
                company.Id,
                code: "001",
                name: "Matriz Test",
                address: "Av. Principal 123",
                phone: null,
                isMain: true,
                createdBy: _createdBy
            );
            var cashRegister = CashRegister.Create(
                tenant.Id,
                company.Id,
                branch.Id,
                "CAJA-01",
                "Caja Principal",
                _createdBy
            );

            db.Tenants.Add(tenant);
            db.Companies.Add(company);
            db.Branches.Add(branch);
            db.BusinessPartners.Add(customer);
            db.Establishments.Add(establishment);
            db.CashRegisters.Add(cashRegister);
            await db.SaveChangesAsync();

            var emissionPoint = EmissionPoint.Create(
                tenant.Id,
                company.Id,
                establishment.Id,
                code: "001",
                name: "PE-001",
                emissionType: EmissionType.Electronic,
                isDefault: true,
                createdBy: _createdBy
            );
            db.EmissionPoints.Add(emissionPoint);
            await db.SaveChangesAsync();

            var cashSession = CashSession.Open(
                tenant.Id,
                company.Id,
                branch.Id,
                _createdBy,
                cashRegister.Id,
                "CAJA-01",
                "Caja Principal",
                emissionPoint.Id,
                "001",
                0m,
                _createdBy
            );
            db.CashSessions.Add(cashSession);
            await db.SaveChangesAsync();

            _tenantId = tenant.Id;
            _companyId = company.Id;
            _branchId = branch.Id;
            _customerId = customer.Id;
            _cashSessionId = cashSession.Id;
        }

        public async Task DisposeAsync() => await _postgres.DisposeAsync();

        private ErpDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options;

            return new ErpDbContext(
                options,
                new FixedCurrentTenant(_tenantId),
                new NoOpPublisher(),
                new FixedCurrentCompany(_companyId)
            );
        }

        private async Task<(Guid InvoiceId, Guid LineId)> SeedAuthorizedInvoiceAsync(
            string invoiceNumber,
            decimal quantity
        )
        {
            await using var db = CreateContext();

            var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
            var paymentTerm = PaymentTermSnapshot.Create(
                Guid.NewGuid(),
                "Contado",
                installments: 1,
                daysBetween: 0
            );

            var inv = SalesInvoice.CreateDraft(
                _tenantId,
                _companyId,
                _branchId,
                _customerId,
                customer,
                invoiceNumber: invoiceNumber,
                issueDate: new DateOnly(2026, 7, 25),
                createdBy: _createdBy,
                paymentTerm: paymentTerm,
                cashSessionId: _cashSessionId
            );

            var line = SalesInvoiceDetail.Create(
                inv.Id,
                _tenantId,
                "Producto Test",
                quantity: quantity,
                unitPrice: 10m,
                vatCode: "0",
                uomCode: "UNIT"
            );
            inv.ReplaceLines(new[] { line }, _createdBy);

            var payment = SalesInvoicePayment.Create(
                inv.Id,
                _tenantId,
                Guid.NewGuid(),
                "01",
                "Efectivo",
                quantity * 10m
            );
            inv.ReplacePayments(new[] { payment }, _createdBy);

            inv.Authorize(_createdBy);

            db.SalesInvoices.Add(inv);
            await db.SaveChangesAsync();

            return (inv.Id, line.Id);
        }

        private async Task<Guid> SeedDraftReturnAsync(
            Guid invoiceId,
            Guid lineId,
            decimal quantity,
            string returnNumber
        )
        {
            await using var db = CreateContext();
            var repo = new SalesReturnRepository(db, new FixedCurrentCompany(_companyId));

            var salesReturn = SalesReturn.CreateDraft(
                _tenantId,
                _companyId,
                invoiceId,
                _customerId,
                returnNumber,
                "Producto en mal estado",
                _createdBy
            );
            salesReturn.AddLine(
                SalesReturnDetail.Create(
                    salesReturn.Id,
                    _tenantId,
                    lineId,
                    "Producto Test",
                    quantity,
                    10m,
                    discountPct: 0m,
                    vatCode: "0",
                    vatRate: 0m,
                    uomCode: "UNIT"
                ),
                _createdBy
            );

            await repo.AddAsync(salesReturn);
            await repo.SaveChangesAsync();

            return salesReturn.Id;
        }

        private AuthorizeSalesReturnHandler BuildHandler(ErpDbContext db) =>
            new(
                new SalesReturnRepository(db, new FixedCurrentCompany(_companyId)),
                new ERP.Infrastructure.Persistence.Repositories.Sales.SalesInvoiceRepository(
                    db,
                    new FixedCurrentCompany(_companyId)
                ),
                new StockRepository(
                    db,
                    new FixedCurrentCompany(_companyId),
                    new ERP.Infrastructure.Persistence.PostgresDatabaseExceptionTranslator()
                ),
                new SalesReturnRefundHandler(
                    new ERP.Infrastructure.Persistence.Repositories.Caja.CashSessionRepository(
                        db,
                        new FixedCurrentCompany(_companyId)
                    ),
                    new ERP.Infrastructure.Persistence.Repositories.Sales.SalesReceivableRepository(
                        db,
                        new FixedCurrentCompany(_companyId)
                    ),
                    new FixedCurrentCashSession(_cashSessionId)
                ),
                Mock.Of<ERP.Domain.Modules.Company.Interfaces.IDocumentSequenceRepository>(),
                Mock.Of<ERP.Domain.Modules.Company.Interfaces.IEmissionPointRepository>(),
                Mock.Of<ERP.Domain.Modules.Company.Interfaces.IEstablishmentRepository>(),
                Mock.Of<ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentIssuer>(),
                new UnitOfWork(db),
                Mock.Of<IPostingEngine>(),
                new FixedCurrentTenant(_tenantId),
                new FixedCurrentCompany(_companyId),
                new FixedCurrentUser(_createdBy),
                Mock.Of<ILogger<AuthorizeSalesReturnHandler>>()
            );

        [Fact]
        public async Task Dos_autorizaciones_concurrentes_sobre_la_misma_factura_solo_una_tiene_exito()
        {
            // Factura con 10 unidades disponibles; dos devoluciones Draft piden 6 cada una
            // (suma = 12 > 10) — bajo el advisory lock, solo una puede autorizarse.
            var (invoiceId, lineId) = await SeedAuthorizedInvoiceAsync("RET-AUTH-LOCK-001", 10m);
            var returnAId = await SeedDraftReturnAsync(invoiceId, lineId, 6m, "DEV-LOCK-A");
            var returnBId = await SeedDraftReturnAsync(invoiceId, lineId, 6m, "DEV-LOCK-B");

            var go = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            async Task<Result<ERP.Application.Modules.Sales.DTOs.SalesReturnDto>> AuthorizeAsync(
                Guid salesReturnId
            )
            {
                await go.Task.ConfigureAwait(false);
                await using var db = CreateContext();
                var handler = BuildHandler(db);
                return await handler.Handle(
                    new AuthorizeSalesReturnCommand(
                        salesReturnId,
                        new List<AuthorizeSalesReturnRefundAllocationInput> { new("Cash", 60m) }
                    ),
                    CancellationToken.None
                );
            }

            var taskA = AuthorizeAsync(returnAId);
            var taskB = AuthorizeAsync(returnBId);
            go.SetResult(true);
            var results = await Task.WhenAll(taskA, taskB);

            results
                .Count(r => r.IsSuccess)
                .Should()
                .Be(1, because: "solo una autorización debe tener éxito");
            results
                .Count(r => !r.IsSuccess)
                .Should()
                .Be(1, because: "la otra debe rechazarse por remanente insuficiente");

            var rejected = results.First(r => !r.IsSuccess);
            rejected.Code.Should().Be(ApiResponseCodes.Common.ValidationError);

            // Verificación final: no existe sobre-devolución — el total autorizado nunca excede
            // las 10 unidades originales de la factura.
            await using var verifyDb = CreateContext();
            var verifyRepo = new SalesReturnRepository(
                verifyDb,
                new FixedCurrentCompany(_companyId)
            );
            var totalReturned = await verifyRepo.GetReturnedQuantityByInvoiceDetailAsync(
                _tenantId,
                lineId
            );
            totalReturned.Should().Be(6m, because: "solo la devolución ganadora quedó Authorized");
        }

        private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
        {
            public Guid TenantId => tenantId;
            public string? Slug => null;
        }

        private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
        {
            public Guid CompanyId => companyId;
            public bool IsAuthenticated => true;
            public bool HasCompanyContext => companyId != Guid.Empty;
        }

        private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
        {
            public Guid UserId => userId;
            public bool IsAuthenticated => true;
            public string? Username => "test-user";
            public string? Email => null;
            public string? FullName => null;
            public string? Role => null;
        }

        /// <summary>Simula la sesión de caja ABIERTA actual del usuario que procesa la devolución
        /// (Fase 6) — distinta de <c>SalesInvoice.CashSessionId</c> (la de la venta original).</summary>
        private sealed class FixedCurrentCashSession(Guid cashSessionId) : ICurrentCashSession
        {
            public Guid? CashSessionId => cashSessionId;
            public Guid? CashRegisterId => null;
            public Guid? EmissionPointId => null;
            public Guid? BranchId => null;
            public bool HasOpenSession => true;
            public string? CashRegisterCodeSnapshot => null;
            public string? CashRegisterNameSnapshot => null;
        }

        private sealed class NoOpPublisher : IPublisher
        {
            public Task Publish(
                object notification,
                CancellationToken cancellationToken = default
            ) => Task.CompletedTask;

            public Task Publish<TNotification>(
                TNotification notification,
                CancellationToken cancellationToken = default
            )
                where TNotification : INotification => Task.CompletedTask;
        }
    }
}
