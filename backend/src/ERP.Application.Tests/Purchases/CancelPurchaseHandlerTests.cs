using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// P0-02 Fase 3 — cobertura de <see cref="CancelPurchaseHandler"/> tras el endurecimiento con
/// transacción explícita + Lock A por <c>PurchaseInvoiceId</c> (§15.1 del diseño) y la validación
/// nueva PI-CANC-02 (no se puede anular una compra con crédito de proveedor ya aplicado contra su
/// CxP, vía <c>PurchasePayable.SupplierCreditAppliedAmount</c> — P0-02). No existía suite previa
/// para este handler.
/// </summary>
public sealed class CancelPurchaseHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PtId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid WhId = Guid.NewGuid();

    private static PurchaseInvoice CreateConfirmedInvoice()
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000001",
            new DateOnly(2026, 7, 30),
            UserId,
            PtId,
            "Contado",
            1,
            30
        );

        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto Test",
            quantity: 1,
            unitPrice: 100m,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, UserId);
        inv.Confirm(UserId);
        return inv;
    }

    private static PurchaseInvoice CreateConfirmedInvoiceWithPackagedLine()
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000002",
            new DateOnly(2026, 7, 30),
            UserId,
            PtId,
            "Contado",
            1,
            30,
            globalWarehouseId: WhId
        );

        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Fanta Harmony NRJ 1350 PET(12)",
            quantity: 2m,
            unitPrice: 9.29m,
            vatCode: "10",
            uomCode: "PACA",
            itemId: ItemId,
            warehouseId: WhId,
            conversionFactor: 12m,
            baseUomCode: "UNIT"
        );
        inv.ReplaceLines([line], UserId);
        inv.Confirm(UserId);
        return inv;
    }

    private static (
        Mock<IPurchaseInvoiceRepository> repo,
        Mock<IStockRepository> stockRepo,
        Mock<IPurchaseReturnRepository> purchaseReturnRepo,
        Mock<IUnitOfWork> uow
    ) BuildMocks()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var stockRepo = new Mock<IStockRepository>();
        var purchaseReturnRepo = new Mock<IPurchaseReturnRepository>();
        var uow = new Mock<IUnitOfWork>();
        return (repo, stockRepo, purchaseReturnRepo, uow);
    }

    private static CancelPurchaseHandler BuildHandler(
        Mock<IPurchaseInvoiceRepository> repo,
        Mock<IStockRepository> stockRepo,
        Mock<IPurchaseReturnRepository> purchaseReturnRepo,
        Mock<IUnitOfWork> uow
    )
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        return new CancelPurchaseHandler(
            repo.Object,
            stockRepo.Object,
            purchaseReturnRepo.Object,
            uow.Object,
            Mock.Of<ILogger<CancelPurchaseHandler>>(),
            tenant.Object,
            company.Object,
            user.Object
        );
    }

    [Fact]
    public async Task Anulacion_valida_cancela_la_compra()
    {
        var (repo, stockRepo, purchaseReturnRepo, uow) = BuildMocks();
        var inv = CreateConfirmedInvoice();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        repo.Setup(r =>
                r.GetPayableByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PurchasePayable?)null);
        repo.Setup(r =>
                r.GetWithholdingByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((IssuedWithholding?)null);

        var handler = BuildHandler(repo, stockRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelPurchaseCommand(inv.Id, "Compra duplicada"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        inv.Status.Should().Be(Domain.Modules.Purchases.Enums.PurchaseStatus.Cancelled);
        stockRepo.Verify(
            s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Anulacion_con_presentacion_de_compra_revierte_inventario_en_unidad_base()
    {
        var (repo, stockRepo, purchaseReturnRepo, uow) = BuildMocks();
        var inv = CreateConfirmedInvoiceWithPackagedLine();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        repo.Setup(r =>
                r.GetPayableByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PurchasePayable?)null);
        repo.Setup(r =>
                r.GetWithholdingByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((IssuedWithholding?)null);

        var handler = BuildHandler(repo, stockRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelPurchaseCommand(inv.Id, "Compra duplicada"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    ItemId,
                    WhId,
                    StockMovementType.PurchaseReturn,
                    -24m,
                    "UNIT",
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    inv.Id,
                    "PurchaseInvoice",
                    UserId,
                    0.774167m,
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Anulacion_valida_adquiere_Lock_A_por_el_PurchaseInvoiceId()
    {
        var (repo, stockRepo, purchaseReturnRepo, uow) = BuildMocks();
        var inv = CreateConfirmedInvoice();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        repo.Setup(r =>
                r.GetPayableByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PurchasePayable?)null);
        repo.Setup(r =>
                r.GetWithholdingByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((IssuedWithholding?)null);

        var handler = BuildHandler(repo, stockRepo, purchaseReturnRepo, uow);
        await handler.Handle(
            new CancelPurchaseCommand(inv.Id, "Compra duplicada"),
            CancellationToken.None
        );

        uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        purchaseReturnRepo.Verify(
            r => r.AcquireFinancialLockAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// cmd.PurchaseInvoiceId ya identifica directamente qué Lock A adquirir (§7.4 de la
    /// remediación) — a diferencia de los demás handlers, aquí NO hay descubrimiento previo: la
    /// transacción y el lock se adquieren SIEMPRE antes de intentar cargar la factura, incluso
    /// cuando termina siendo inexistente. El rollback revierte esa transacción ya abierta.
    /// </summary>
    [Fact]
    public async Task Anulacion_sobre_compra_inexistente_retorna_NotFound_y_revierte_la_transaccion_y_lock_ya_adquiridos()
    {
        var (repo, stockRepo, purchaseReturnRepo, uow) = BuildMocks();
        var missingId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);

        var handler = BuildHandler(repo, stockRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelPurchaseCommand(missingId, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        purchaseReturnRepo.Verify(
            r => r.AcquireFinancialLockAsync(TenantId, missingId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Anulacion_de_compra_con_pagos_aplicados_retorna_ValidationFailure_y_hace_rollback()
    {
        var (repo, stockRepo, purchaseReturnRepo, uow) = BuildMocks();
        var inv = CreateConfirmedInvoice();
        var payable = PurchasePayable.Create(TenantId, CompanyId, inv.Id, SupplierId, 100m, UserId);
        payable.RegisterPayment(30m, UserId);
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        repo.Setup(r =>
                r.GetPayableByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payable);

        var handler = BuildHandler(repo, stockRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelPurchaseCommand(inv.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("pagos aplicados");
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// PI-CANC-02 (P0-02, Fase 3): no se puede anular una compra con crédito de proveedor
    /// (SupplierCredit) ya aplicado contra su CxP — aunque no tenga pagos directos registrados.
    /// </summary>
    [Fact]
    public async Task Anulacion_de_compra_con_credito_de_proveedor_aplicado_retorna_ValidationFailure_PI_CANC_02()
    {
        var (repo, stockRepo, purchaseReturnRepo, uow) = BuildMocks();
        var inv = CreateConfirmedInvoice();
        var payable = PurchasePayable.Create(TenantId, CompanyId, inv.Id, SupplierId, 100m, UserId);
        payable.ApplySupplierCredit(40m, UserId);
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        repo.Setup(r =>
                r.GetPayableByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payable);

        var handler = BuildHandler(repo, stockRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelPurchaseCommand(inv.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("crédito de proveedor");
        inv.Status.Should().NotBe(Domain.Modules.Purchases.Enums.PurchaseStatus.Cancelled);
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Estado concurrente simulado (§9.4 de la remediación, mínimo exigido para este handler):
    /// como no hay descubrimiento previo, la ÚNICA carga de PurchaseInvoice ocurre ya bajo el
    /// lock — simula que, para cuando esta transacción adquirió el lock, otra ya había anulado la
    /// factura. El guard "ya fue anulada" se evalúa sobre esa instancia post-lock y rechaza.
    /// </summary>
    [Fact]
    public async Task Anulacion_de_compra_ya_anulada_por_otra_transaccion_concurrente_retorna_ValidationFailure_y_hace_rollback()
    {
        var (repo, stockRepo, purchaseReturnRepo, uow) = BuildMocks();
        var inv = CreateConfirmedInvoice();
        inv.Cancel("Anulada por otra transacción concurrente", UserId);
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);

        var handler = BuildHandler(repo, stockRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelPurchaseCommand(inv.Id, "Segunda anulación concurrente"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ya fue anulada");
        uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        purchaseReturnRepo.Verify(
            r => r.AcquireFinancialLockAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        stockRepo.Verify(
            s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── PI-CANC-01 (Fase 3, Remediación 01) ────────────────────────────────

    /// <summary>Caso 1: existe una PurchaseReturn Authorized asociada — bloquea, hace rollback y
    /// nunca llega a evaluar PI-CANC-02 ni a mutar/persistir nada.</summary>
    [Fact]
    public async Task Anulacion_con_PurchaseReturn_Authorized_asociada_retorna_ValidationFailure_PI_CANC_01()
    {
        var (repo, stockRepo, purchaseReturnRepo, uow) = BuildMocks();
        var inv = CreateConfirmedInvoice();
        var payable = PurchasePayable.Create(TenantId, CompanyId, inv.Id, SupplierId, 100m, UserId);
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        repo.Setup(r =>
                r.GetPayableByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payable);
        purchaseReturnRepo
            .Setup(r =>
                r.ExistsAuthorizedByPurchaseInvoiceIdAsync(
                    TenantId,
                    CompanyId,
                    inv.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);

        var handler = BuildHandler(repo, stockRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelPurchaseCommand(inv.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("devolución de compra autorizada");
        inv.Status.Should().NotBe(Domain.Modules.Purchases.Enums.PurchaseStatus.Cancelled);
        payable
            .SupplierCreditAppliedAmount.Should()
            .Be(0m, "PI-CANC-02 nunca debe evaluarse/mutar nada tras bloquear por PI-CANC-01");
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        stockRepo.Verify(
            s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    /// <summary>Caso 2: sin PurchaseReturn Authorized asociada y sin crédito de proveedor aplicado
    /// — PI-CANC-01 permite continuar, se evalúa PI-CANC-02 (pasa) y la anulación se completa.</summary>
    [Fact]
    public async Task Anulacion_sin_PurchaseReturn_Authorized_y_sin_credito_aplicado_completa_la_anulacion()
    {
        var (repo, stockRepo, purchaseReturnRepo, uow) = BuildMocks();
        var inv = CreateConfirmedInvoice();
        var payable = PurchasePayable.Create(TenantId, CompanyId, inv.Id, SupplierId, 100m, UserId);
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        repo.Setup(r =>
                r.GetPayableByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payable);
        repo.Setup(r =>
                r.GetWithholdingByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((IssuedWithholding?)null);
        purchaseReturnRepo
            .Setup(r =>
                r.ExistsAuthorizedByPurchaseInvoiceIdAsync(
                    TenantId,
                    CompanyId,
                    inv.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);

        var handler = BuildHandler(repo, stockRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelPurchaseCommand(inv.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        inv.Status.Should().Be(Domain.Modules.Purchases.Enums.PurchaseStatus.Cancelled);
        purchaseReturnRepo.Verify(
            r =>
                r.ExistsAuthorizedByPurchaseInvoiceIdAsync(
                    TenantId,
                    CompanyId,
                    inv.Id,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Caso 3: la consulta PI-CANC-01 recibe exactamente TenantId/CompanyId/PurchaseInvoiceId
    /// de la operación y el CancellationToken recibido por el handler.</summary>
    [Fact]
    public async Task PI_CANC_01_recibe_exactamente_TenantId_CompanyId_PurchaseInvoiceId_y_el_CancellationToken()
    {
        var (repo, stockRepo, purchaseReturnRepo, uow) = BuildMocks();
        var inv = CreateConfirmedInvoice();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        repo.Setup(r =>
                r.GetPayableByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PurchasePayable?)null);
        repo.Setup(r =>
                r.GetWithholdingByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((IssuedWithholding?)null);

        using var cts = new CancellationTokenSource();
        var handler = BuildHandler(repo, stockRepo, purchaseReturnRepo, uow);
        await handler.Handle(new CancelPurchaseCommand(inv.Id, "Motivo"), cts.Token);

        purchaseReturnRepo.Verify(
            r => r.ExistsAuthorizedByPurchaseInvoiceIdAsync(TenantId, CompanyId, inv.Id, cts.Token),
            Times.Once
        );
    }

    /// <summary>Caso 4: orden transaccional exacto — BeginTransaction → Lock A → recarga →
    /// PI-CANC-01 → PI-CANC-02 (implícito, no requiere llamada adicional) → mutación → SaveChanges
    /// → Commit, verificado con una secuencia estricta de Moq.</summary>
    [Fact]
    public async Task Orden_transaccional_BeginTx_LockA_recarga_PI_CANC_01_mutacion_SaveChanges_Commit()
    {
        var (repo, stockRepo, purchaseReturnRepo, uow) = BuildMocks();
        var inv = CreateConfirmedInvoice();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        repo.Setup(r =>
                r.GetWithholdingByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((IssuedWithholding?)null);

        var sequence = new MockSequence();
        uow.InSequence(sequence)
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        purchaseReturnRepo
            .InSequence(sequence)
            .Setup(r =>
                r.AcquireFinancialLockAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);
        repo.InSequence(sequence)
            .Setup(r =>
                r.GetPayableByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PurchasePayable?)null);
        purchaseReturnRepo
            .InSequence(sequence)
            .Setup(r =>
                r.ExistsAuthorizedByPurchaseInvoiceIdAsync(
                    TenantId,
                    CompanyId,
                    inv.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        stockRepo
            .InSequence(sequence)
            .Setup(s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        uow.InSequence(sequence)
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(repo, stockRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelPurchaseCommand(inv.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
    }
}
