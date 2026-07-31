using ERP.Application.Common;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// P0-01 Fase 6 — <see cref="SalesReturnRefundHandler"/>: ejecución síncrona de
/// <c>SalesReturnRefundAllocation</c> (Caja para <c>Cash</c>, CxC para <c>ReceivableCredit</c>).
/// No cubre Accounting/Posting/Audit/ElectronicDocuments/Nota de Crédito/RIDE/API/Frontend/
/// Permisos (fases posteriores) — tampoco usa <c>INotificationHandler</c> (decisión de
/// arquitectura de esta fase: todo síncrono, dentro de la misma transacción de autorización).
/// </summary>
public sealed class SalesReturnRefundHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid InvoiceId = Guid.NewGuid();

    // ── Fixtures ──────────────────────────────────────────────────────

    private static CashSession OpenSession() =>
        CashSession.Open(
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

    private static SalesReturn BuildAuthorizedReturn(
        decimal lineQuantity,
        decimal lineUnitPrice,
        params (SalesReturnRefundMethod Method, decimal Amount)[] allocations
    )
    {
        var salesReturn = SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            InvoiceId,
            CustomerId,
            $"DEV-{Guid.NewGuid():N}"[..10],
            "Producto en mal estado",
            UserId
        );
        salesReturn.AddLine(
            SalesReturnDetail.Create(
                salesReturn.Id,
                TenantId,
                Guid.NewGuid(),
                "Producto Test",
                lineQuantity,
                lineUnitPrice,
                discountPct: 0m,
                vatCode: "0",
                vatRate: 0m,
                uomCode: "UNIT"
            ),
            UserId
        );
        foreach (var (method, amount) in allocations)
            salesReturn.AddRefundAllocation(
                SalesReturnRefundAllocation.Create(salesReturn.Id, TenantId, method, amount),
                UserId
            );
        salesReturn.Authorize(UserId);
        return salesReturn;
    }

    private static SalesReceivable BuildReceivable(
        decimal originalAmount,
        decimal paidAmount = 0m,
        int installmentCount = 2
    )
    {
        var receivable = SalesReceivable.Create(
            TenantId,
            CompanyId,
            InvoiceId,
            CustomerId,
            originalAmount,
            UserId
        );
        receivable.GenerateInstallments(new DateOnly(2026, 7, 1), 30, installmentCount);
        if (paidAmount > 0)
            receivable.RegisterCollection(paidAmount, UserId);
        return receivable;
    }

    private static (
        SalesReturnRefundHandler Handler,
        Mock<ICashSessionRepository> CashRepo,
        Mock<ISalesReceivableRepository> ReceivableRepo,
        Mock<ICurrentCashSession> CashSessionCtx
    ) BuildHandler(CashSession? openSession, SalesReceivable? receivable)
    {
        var cashRepo = new Mock<ICashSessionRepository>();
        if (openSession is not null)
            cashRepo
                .Setup(r => r.GetByIdAsync(TenantId, openSession.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(openSession);

        var receivableRepo = new Mock<ISalesReceivableRepository>();
        receivableRepo
            .Setup(r => r.GetByInvoiceIdAsync(TenantId, InvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);

        var cashSessionCtx = new Mock<ICurrentCashSession>();
        cashSessionCtx.Setup(c => c.HasOpenSession).Returns(openSession is not null);
        cashSessionCtx.Setup(c => c.CashSessionId).Returns(openSession?.Id);

        var handler = new SalesReturnRefundHandler(
            cashRepo.Object,
            receivableRepo.Object,
            cashSessionCtx.Object
        );

        return (handler, cashRepo, receivableRepo, cashSessionCtx);
    }

    // ══════════════════════════════ 100% efectivo ══════════════════════════════

    [Fact]
    public async Task Ejecuta_100_por_ciento_efectivo()
    {
        var session = OpenSession();
        var salesReturn = BuildAuthorizedReturn(
            4m,
            10m,
            (SalesReturnRefundMethod.Cash, 40m)
        );
        var (handler, _, _, _) = BuildHandler(session, receivable: null);

        var error = await handler.ExecuteAsync(salesReturn, TenantId, UserId, CancellationToken.None);

        error.Should().BeNull();
        session.CurrentBalance.Should().Be(-40m, because: "OpeningAmount(0) - 40 de reembolso");
        var movement = session.Movements.Should().ContainSingle(m => m.MovementType == CashMovementType.SaleRefund).Which;
        movement.Amount.Should().Be(40m);
        movement.ReferenceType.Should().Be(CashReferenceType.SalesReturn);
        movement.ReferenceId.Should().Be(salesReturn.Id);
    }

    // ══════════════════════════════ 100% crédito ══════════════════════════════

    [Fact]
    public async Task Ejecuta_100_por_ciento_credito()
    {
        var receivable = BuildReceivable(originalAmount: 100m, paidAmount: 20m, installmentCount: 2);
        var salesReturn = BuildAuthorizedReturn(
            4m,
            10m,
            (SalesReturnRefundMethod.ReceivableCredit, 40m)
        );
        var (handler, _, _, _) = BuildHandler(openSession: null, receivable);

        var error = await handler.ExecuteAsync(salesReturn, TenantId, UserId, CancellationToken.None);

        error.Should().BeNull();
        receivable.OriginalAmount.Should().Be(60m); // 100 - 40
        receivable.PaidAmount.Should().Be(20m, because: "el crédito nunca toca lo ya cobrado");
        receivable.BalanceDue.Should().Be(40m); // 60 - 20
        receivable.Installments.Should().HaveCount(2);
        receivable.Installments.Sum(i => i.Amount).Should().Be(receivable.BalanceDue);
    }

    // ══════════════════════════════ Mixto efectivo + crédito ══════════════════════════════

    [Fact]
    public async Task Ejecuta_reembolso_mixto_efectivo_y_credito()
    {
        var session = OpenSession();
        var receivable = BuildReceivable(originalAmount: 100m, paidAmount: 0m, installmentCount: 1);
        var salesReturn = BuildAuthorizedReturn(
            5m,
            10m,
            (SalesReturnRefundMethod.Cash, 30m),
            (SalesReturnRefundMethod.ReceivableCredit, 20m)
        );
        var (handler, _, _, _) = BuildHandler(session, receivable);

        var error = await handler.ExecuteAsync(salesReturn, TenantId, UserId, CancellationToken.None);

        error.Should().BeNull();
        session.CurrentBalance.Should().Be(-30m);
        session.Movements.Should().ContainSingle(m => m.MovementType == CashMovementType.SaleRefund);
        receivable.OriginalAmount.Should().Be(80m); // 100 - 20
        receivable.BalanceDue.Should().Be(80m);
    }

    // ══════════════════════════════ Validaciones ══════════════════════════════

    [Fact]
    public async Task Sin_CashSession_abierta_falla()
    {
        var salesReturn = BuildAuthorizedReturn(
            4m,
            10m,
            (SalesReturnRefundMethod.Cash, 40m)
        );
        var (handler, _, _, _) = BuildHandler(openSession: null, receivable: null);

        var error = await handler.ExecuteAsync(salesReturn, TenantId, UserId, CancellationToken.None);

        error.Should().NotBeNull();
        error.Should().Contain("caja abierta");
    }

    [Fact]
    public async Task BalanceDue_insuficiente_falla()
    {
        var receivable = BuildReceivable(originalAmount: 100m, paidAmount: 70m, installmentCount: 1); // BalanceDue = 30
        var salesReturn = BuildAuthorizedReturn(
            4m,
            10m,
            (SalesReturnRefundMethod.ReceivableCredit, 40m) // > BalanceDue (30)
        );
        var (handler, _, _, _) = BuildHandler(openSession: null, receivable);

        var error = await handler.ExecuteAsync(salesReturn, TenantId, UserId, CancellationToken.None);

        error.Should().NotBeNull();
        error.Should().Contain("excede el saldo pendiente");
        receivable.OriginalAmount.Should().Be(100m, because: "no debe mutar si la validación falla");
    }

    [Fact]
    public async Task BalanceDue_cero_rechaza_credito()
    {
        var receivable = BuildReceivable(originalAmount: 100m, paidAmount: 100m, installmentCount: 1); // BalanceDue = 0
        var salesReturn = BuildAuthorizedReturn(
            4m,
            10m,
            (SalesReturnRefundMethod.ReceivableCredit, 40m)
        );
        var (handler, _, _, _) = BuildHandler(openSession: null, receivable);

        var error = await handler.ExecuteAsync(salesReturn, TenantId, UserId, CancellationToken.None);

        error.Should().NotBeNull();
        receivable.OriginalAmount.Should().Be(100m);
        receivable.BalanceDue.Should().Be(0m);
    }

    // ══════════════════════════════ Rollback completo (AuthorizeSalesReturnHandler) ══════════════════════════════

    private static (SalesInvoice Invoice, SalesInvoiceDetail Line) BuildAuthorizedInvoiceWithLine(
        decimal quantity,
        decimal unitPrice
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", 1, 0);
        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            "001-001-000000001",
            new DateOnly(2026, 7, 25),
            UserId,
            paymentTerm,
            Guid.NewGuid()
        );
        var line = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto Test",
            quantity,
            unitPrice,
            vatCode: "0",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, UserId);
        var payment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            quantity * unitPrice
        );
        inv.ReplacePayments(new[] { payment }, UserId);
        inv.Authorize(UserId);
        return (inv, inv.Lines[0]);
    }

    private static (
        AuthorizeSalesReturnHandler Handler,
        Mock<IUnitOfWork> Uow,
        Mock<IStockRepository> StockRepo
    ) BuildAuthorizeHandler(
        SalesInvoice invoice,
        SalesReturn draftReturn,
        SalesReturnRefundHandler refundHandler
    )
    {
        var invoiceRepo = new Mock<ISalesInvoiceRepository>();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var returnRepo = new Mock<ISalesReturnRepository>();
        returnRepo
            .Setup(r => r.GetByIdAsync(TenantId, draftReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draftReturn);
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
            .ReturnsAsync(0m);

        var stockRepo = new Mock<IStockRepository>();
        stockRepo
            .Setup(s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var tenant = Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);
        var company = Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId);
        var user = Mock.Of<ICurrentUser>(u => u.UserId == UserId);

        var handler = new AuthorizeSalesReturnHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            stockRepo.Object,
            refundHandler,
            Mock.Of<ERP.Domain.Modules.Company.Interfaces.IDocumentSequenceRepository>(),
            Mock.Of<ERP.Domain.Modules.Company.Interfaces.IEmissionPointRepository>(),
            Mock.Of<ERP.Domain.Modules.Company.Interfaces.IEstablishmentRepository>(),
            Mock.Of<ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentIssuer>(),
            uow.Object,
            tenant,
            company,
            user,
            Mock.Of<ILogger<AuthorizeSalesReturnHandler>>()
        );

        return (handler, uow, stockRepo);
    }

    [Fact]
    public async Task Rollback_completo_cuando_falla_Caja()
    {
        var (invoice, line) = BuildAuthorizedInvoiceWithLine(4m, 10m);
        var draftReturn = SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            invoice.Id,
            CustomerId,
            $"DEV-{Guid.NewGuid():N}"[..10],
            "Motivo",
            UserId
        );
        draftReturn.AddLine(
            SalesReturnDetail.Create(
                draftReturn.Id,
                TenantId,
                line.Id,
                line.Description,
                4m,
                line.UnitPrice,
                0m,
                line.VatCode,
                line.VatRate,
                line.UomCode
            ),
            UserId
        );

        // Sin sesión de caja abierta — el reembolso en efectivo debe fallar.
        var (refundHandler, _, _, _) = BuildHandler(openSession: null, receivable: null);
        var (handler, uow, stockRepo) = BuildAuthorizeHandler(invoice, draftReturn, refundHandler);

        var result = await handler.Handle(
            new AuthorizeSalesReturnCommand(
                draftReturn.Id,
                new List<AuthorizeSalesReturnRefundAllocationInput> { new("Cash", 40m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("caja abierta");

        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        stockRepo.Verify(
            s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "no debe persistirse nada si el reembolso falla"
        );
    }

    [Fact]
    public async Task Rollback_completo_cuando_falla_CxC()
    {
        var (invoice, line) = BuildAuthorizedInvoiceWithLine(4m, 10m);
        var draftReturn = SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            invoice.Id,
            CustomerId,
            $"DEV-{Guid.NewGuid():N}"[..10],
            "Motivo",
            UserId
        );
        draftReturn.AddLine(
            SalesReturnDetail.Create(
                draftReturn.Id,
                TenantId,
                line.Id,
                line.Description,
                4m,
                line.UnitPrice,
                0m,
                line.VatCode,
                line.VatRate,
                line.UomCode
            ),
            UserId
        );

        // Sin CxC asociada a la factura — el crédito debe fallar.
        var (refundHandler, _, _, _) = BuildHandler(openSession: null, receivable: null);
        var (handler, uow, stockRepo) = BuildAuthorizeHandler(invoice, draftReturn, refundHandler);

        var result = await handler.Handle(
            new AuthorizeSalesReturnCommand(
                draftReturn.Id,
                new List<AuthorizeSalesReturnRefundAllocationInput>
                {
                    new("ReceivableCredit", 40m),
                }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cuenta por cobrar");

        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        stockRepo.Verify(
            s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "no debe persistirse nada si el reembolso falla"
        );
    }
}
