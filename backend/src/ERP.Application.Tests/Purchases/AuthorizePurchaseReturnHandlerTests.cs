using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// P0-02 Fase 6 — <c>AuthorizePurchaseReturnHandler</c>: autorización feliz (impaga/parcial/
/// totalmente pagada), rechazos PR-005/PR-006, cálculo exacto de <c>CostVarianceTotal</c> (ejemplo
/// (g) de §11.3), herencia de <c>BranchId</c> en <c>SupplierCredit</c>, e idempotencia (§16.2).
/// </summary>
public sealed class AuthorizePurchaseReturnHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();

    private sealed record Fixture(
        PurchaseInvoice Invoice,
        PurchaseInvoiceDetail Line,
        PurchasePayable Payable,
        PurchaseReturn Return
    );

    private static Fixture BuildFixture(
        decimal quantity = 10,
        decimal unitPrice = 100m,
        decimal vatRate = 12m,
        decimal landedUnitCost = 100m,
        decimal paidAmount = 0m,
        decimal returnQuantity = 3m
    )
    {
        var invoice = PurchaseInvoice.CreateDraft(
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
            PaymentTermId,
            "Contado",
            1,
            30,
            globalWarehouseId: WarehouseId
        );
        var line = PurchaseInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Producto 1",
            quantity: quantity,
            unitPrice: unitPrice,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: ItemId,
            warehouseId: WarehouseId
        );
        line.ApplyTaxes("10", vatRate, "IVA", null, 0m, null);
        invoice.ReplaceLines(new[] { line }, UserId);
        invoice.Confirm(UserId);
        var confirmedLine = invoice.Lines.Single();

        // LandedUnitCost se congela en Confirm() vía FreezeCosts() a partir de UnitPrice (sin
        // flete/otros costos distribuidos en este fixture) — para simular el ejemplo (g) de §11.3
        // (LandedUnitCost=115 > UnitPrice=100), el caller ajusta manualmente si lo necesita.
        _ = landedUnitCost;

        var payable = PurchasePayable.Create(
            TenantId,
            CompanyId,
            invoice.Id,
            SupplierId,
            invoice.ConfirmedGrandTotal ?? confirmedLine.TaxInclusiveTotal,
            UserId
        );
        if (paidAmount > 0)
            payable.RegisterPayment(paidAmount, UserId);

        var purchaseReturn = PurchaseReturn.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            invoice.Id,
            SupplierId,
            "Producto en mal estado",
            new[]
            {
                new PurchaseReturn.DraftLineInput(
                    confirmedLine.Id,
                    ItemId,
                    returnQuantity,
                    WarehouseId
                ),
            },
            UserId,
            Guid.NewGuid(),
            "create-hash"
        );

        return new Fixture(invoice, confirmedLine, payable, purchaseReturn);
    }

    private sealed class Mocks
    {
        public Mock<IPurchaseReturnRepository> ReturnRepo { get; } = new();
        public Mock<IPurchaseInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IPurchaseReturnSequenceRepository> SequenceRepo { get; } = new();
        public Mock<IStockRepository> StockRepo { get; } = new();
        public Mock<ISupplierCreditRepository> CreditRepo { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();
        public List<Guid> AppendedItemWarehouses { get; } = new();

        public Mocks(Fixture f, decimal availableStock = 1000m, string? returnNumber = "00000001")
        {
            ReturnRepo
                .Setup(r =>
                    r.GetPurchaseInvoiceIdAsync(TenantId, f.Return.Id, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(f.Invoice.Id);
            ReturnRepo
                .Setup(r => r.GetByIdAsync(TenantId, f.Return.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Return);
            ReturnRepo
                .Setup(r =>
                    r.GetReturnedQuantitiesByInvoiceDetailIdsAsync(
                        TenantId,
                        It.IsAny<IReadOnlyCollection<Guid>>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(new Dictionary<Guid, decimal>());

            InvoiceRepo
                .Setup(r => r.GetByIdAsync(TenantId, f.Invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Invoice);
            InvoiceRepo
                .Setup(r =>
                    r.GetPayableByPurchaseIdAsync(TenantId, f.Invoice.Id, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(f.Payable);
            InvoiceRepo
                .Setup(r =>
                    r.GetWithholdingByPurchaseIdAsync(
                        TenantId,
                        f.Invoice.Id,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((IssuedWithholding?)null);

            if (returnNumber is not null)
                SequenceRepo
                    .Setup(r =>
                        r.CaptureNextAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
                    )
                    .ReturnsAsync(returnNumber);

            StockRepo
                .Setup(s =>
                    s.GetStockAsync(TenantId, WarehouseId, ItemId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(
                    CurrentStock.Create(TenantId, ItemId, WarehouseId, UserId, CompanyId)
                        .Also(cs => cs.ApplyMovement(availableStock, UserId, 1m))
                );
            StockRepo
                .Setup(s =>
                    s.AppendMovementAsync(
                        TenantId,
                        CompanyId,
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        StockMovementType.PurchaseReturn,
                        It.IsAny<decimal>(),
                        It.IsAny<string>(),
                        It.IsAny<DateOnly>(),
                        It.IsAny<string>(),
                        It.IsAny<Guid>(),
                        "PurchaseReturn",
                        UserId,
                        It.IsAny<decimal?>(),
                        null,
                        null,
                        It.IsAny<CancellationToken>(),
                        It.IsAny<Guid?>()
                    )
                )
                .Returns(
                    (IInvocation invocation) =>
                    {
                        var args = invocation.Arguments;
                        var tid = (Guid)args[0]!;
                        var cid = (Guid)args[1]!;
                        var productId = (Guid)args[2]!;
                        var warehouseId = (Guid)args[3]!;
                        var type = (StockMovementType)args[4]!;
                        var qty = (decimal)args[5]!;
                        var uom = (string)args[6]!;
                        var date = (DateOnly)args[7]!;
                        var reference = (string?)args[8];
                        var sourceDocId = (Guid?)args[9];
                        var sourceDocType = (string?)args[10];
                        var actorId = (Guid)args[11]!;
                        var unitCost = (decimal?)args[12];
                        var lotId = (Guid?)args[13];
                        var serialId = (Guid?)args[14];
                        var sourceDocLineId = (Guid?)args[16];

                        AppendedItemWarehouses.Add(warehouseId);

                        return Task.FromResult(
                            StockMovement.Create(
                                tid,
                                BranchId,
                                productId,
                                warehouseId,
                                type,
                                qty,
                                uom,
                                0m,
                                1,
                                0m,
                                0m,
                                date,
                                reference,
                                sourceDocId,
                                sourceDocType,
                                actorId,
                                cid,
                                unitCost,
                                lotId,
                                serialId,
                                sourceDocLineId
                            )
                        );
                    }
                );

            Uow.SetupGet(u => u.HasActiveTransaction).Returns(true);
        }

        public AuthorizePurchaseReturnHandler BuildHandler(ICurrentUser? user = null)
        {
            var t = new Mock<ICurrentTenant>();
            t.SetupGet(x => x.TenantId).Returns(TenantId);
            var u = new Mock<ICurrentUser>();
            u.SetupGet(x => x.UserId).Returns(UserId);

            return new AuthorizePurchaseReturnHandler(
                ReturnRepo.Object,
                InvoiceRepo.Object,
                SequenceRepo.Object,
                StockRepo.Object,
                CreditRepo.Object,
                Uow.Object,
                DbEx.Object,
                t.Object,
                user ?? u.Object
            );
        }
    }

    // ── Autorización feliz ────────────────────────────────────────────────

    [Fact]
    public async Task Autorizacion_feliz_factura_impaga_no_crea_SupplierCredit()
    {
        var f = BuildFixture(paidAmount: 0m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new AuthorizePurchaseReturnCommand(f.Return.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.Return.Status.Should().Be(PurchaseReturnStatus.Authorized);
        f.Return.SupplierCreditAmount.Should().Be(0m);
        m.CreditRepo.Verify(
            r => r.AddAsync(It.IsAny<SupplierCredit>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Autorizacion_feliz_factura_parcialmente_pagada_aplica_contra_saldo_sin_excedente()
    {
        // BalanceDue = 1000 - 600 = 400; devolución (3 uds de 10 a 100 c/u + 12% IVA) = 336 < 400
        var f = BuildFixture(quantity: 10, unitPrice: 100m, vatRate: 12m, paidAmount: 600m, returnQuantity: 3m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new AuthorizePurchaseReturnCommand(f.Return.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.Return.SupplierCreditAmount.Should().Be(0m);
        f.Payable.BalanceDue.Should().BeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public async Task Autorizacion_feliz_factura_totalmente_pagada_crea_SupplierCredit_por_el_total()
    {
        // TotalAmount=1120 (1000+120 IVA), PaidAmount=1120 → BalanceDue=0. Devolución completa (10
        // uds) → GrandTotal=1120 → todo se convierte en SupplierCredit (mismo criterio ejemplo (e) §11.3).
        var f = BuildFixture(
            quantity: 10,
            unitPrice: 100m,
            vatRate: 12m,
            paidAmount: 1120m,
            returnQuantity: 10m
        );
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new AuthorizePurchaseReturnCommand(f.Return.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.Return.AppliedToPayableAmount.Should().Be(0m);
        f.Return.SupplierCreditAmount.Should().BeGreaterThan(0m);
        m.CreditRepo.Verify(
            r => r.AddAsync(It.IsAny<SupplierCredit>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // ── SupplierCredit hereda BranchId ───────────────────────────────────

    [Fact]
    public async Task SupplierCredit_creado_hereda_literalmente_el_BranchId_de_PurchaseReturn()
    {
        var f = BuildFixture(paidAmount: 1120m, returnQuantity: 10m);
        var m = new Mocks(f);
        SupplierCredit? captured = null;
        m.CreditRepo
            .Setup(r => r.AddAsync(It.IsAny<SupplierCredit>(), It.IsAny<CancellationToken>()))
            .Callback<SupplierCredit, CancellationToken>((c, _) => captured = c)
            .Returns(Task.CompletedTask);
        var handler = m.BuildHandler();

        await handler.Handle(
            new AuthorizePurchaseReturnCommand(f.Return.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        captured.Should().NotBeNull();
        captured!.BranchId.Should().Be(f.Return.BranchId);
        captured.BranchId.Should().Be(BranchId);
    }

    // ── Rechazos ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Rechazo_por_retencion_Issued_PR_006_no_muta_nada()
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var withholding = IssuedWithholding.CreateDraft(
            TenantId,
            CompanyId,
            f.Invoice.Id,
            SupplierId,
            Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId
        );
        withholding.AddDetail(
            IssuedWithholdingDetail.Create(
                withholding.Id,
                TenantId,
                "IVA",
                "1",
                "Retención IVA",
                100m,
                30m
            )
        );
        withholding.Issue("001-001-000000001", UserId);
        m.InvoiceRepo
            .Setup(r =>
                r.GetWithholdingByPurchaseIdAsync(TenantId, f.Invoice.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(withholding);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new AuthorizePurchaseReturnCommand(f.Return.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.Return.Status.Should().Be(PurchaseReturnStatus.Draft);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rechazo_por_stock_insuficiente_PR_005_en_la_bodega_original_no_muta_nada()
    {
        var f = BuildFixture(returnQuantity: 5m);
        var m = new Mocks(f, availableStock: 2m); // menos que las 5 unidades solicitadas
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new AuthorizePurchaseReturnCommand(f.Return.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.Return.Status.Should().Be(PurchaseReturnStatus.Draft);
        m.StockRepo.Verify(
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
                    It.IsAny<Guid?>(),
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

    [Fact]
    public async Task Validacion_de_stock_siempre_usa_la_bodega_original_de_la_linea_nunca_otra()
    {
        var f = BuildFixture(returnQuantity: 3m);
        var otherWarehouseId = Guid.NewGuid();
        var m = new Mocks(f);
        // Si el handler consultara otra bodega, este stub no aplicaría y GetStockAsync devolvería
        // null (0 disponible) → falso rechazo — la prueba falla si eso ocurre.
        m.StockRepo
            .Setup(s =>
                s.GetStockAsync(TenantId, otherWarehouseId, ItemId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CurrentStock?)null);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new AuthorizePurchaseReturnCommand(f.Return.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        m.StockRepo.Verify(
            s => s.GetStockAsync(TenantId, WarehouseId, ItemId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        m.AppendedItemWarehouses.Should().OnlyContain(w => w == WarehouseId);
    }

    // ── CostVarianceTotal exacto — ejemplo (g) de §11.3 ──────────────────

    [Fact]
    public void CostVarianceTotal_reproduce_exactamente_el_ejemplo_g_de_11_3()
    {
        // Reproduce el cálculo de dominio directamente (sin mocks de infraestructura): Quantity=10,
        // LineSubtotal=1000, VatAmount=120 (12%), LandedUnitCost=115. Devolución de 3 unidades.
        var original = new PurchaseReturn.OriginalLineSnapshot(
            OriginalQuantity: 10m,
            LineSubtotal: 1000m,
            DiscountAmount: 0m,
            VatAmount: 120m,
            IceAmount: 0m,
            VatCode: "10",
            VatRate: 12m,
            IceCode: null,
            IceRate: 0m,
            LandedUnitCost: 115m
        );
        var purchaseReturn = PurchaseReturn.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            Guid.NewGuid(),
            SupplierId,
            "Motivo",
            new[] { new PurchaseReturn.DraftLineInput(Guid.NewGuid(), ItemId, 3m, WarehouseId) },
            UserId,
            Guid.NewGuid(),
            "hash"
        );
        var detailId = purchaseReturn.Lines.Single().OriginalInvoiceDetailId;

        purchaseReturn.Authorize(
            "00000001",
            new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot> { [detailId] = original },
            balanceDueBeforeApplication: 1120m,
            currencyCode: "USD",
            hasIssuedWithholding: false,
            UserId,
            Guid.NewGuid(),
            "authorize-hash"
        );

        purchaseReturn.AuthorizedSubtotal.Should().Be(300.00m);
        purchaseReturn.AuthorizedVatTotal.Should().Be(36.00m);
        purchaseReturn.AuthorizedGrandTotal.Should().Be(336.00m);
        purchaseReturn.HistoricalCostTotal.Should().Be(345.00m);
        purchaseReturn.CostVarianceTotal.Should().Be(45.00m);

        var debitTotal =
            purchaseReturn.AppliedToPayableAmount!.Value
            + purchaseReturn.SupplierCreditAmount!.Value
            + Math.Max(purchaseReturn.CostVarianceTotal!.Value, 0m);
        var creditTotal =
            purchaseReturn.HistoricalCostTotal!.Value
            + purchaseReturn.AuthorizedVatTotal!.Value
            + purchaseReturn.AuthorizedIceTotal!.Value
            + Math.Max(-purchaseReturn.CostVarianceTotal!.Value, 0m);

        debitTotal.Should().Be(381.00m);
        creditTotal.Should().Be(381.00m);
        debitTotal.Should().Be(creditTotal);
    }

    // ── Idempotencia (§16.2) ──────────────────────────────────────────────

    [Fact]
    public async Task Reintento_con_mismo_ClientRequestId_retorna_snapshot_ya_confirmado_sin_duplicar_efectos()
    {
        var f = BuildFixture(paidAmount: 0m);
        var clientRequestId = Guid.NewGuid();
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var first = await handler.Handle(
            new AuthorizePurchaseReturnCommand(f.Return.Id, clientRequestId),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue();

        // segunda llamada: el mock de GetByIdAsync ahora devuelve la MISMA instancia ya Authorized
        var retry = await handler.Handle(
            new AuthorizePurchaseReturnCommand(f.Return.Id, clientRequestId),
            CancellationToken.None
        );

        retry.IsSuccess.Should().BeTrue();
        retry.Value!.Id.Should().Be(first.Value!.Id);
        m.SequenceRepo.Verify(
            s => s.CaptureNextAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()),
            Times.Once,
            "el reintento idempotente nunca debe consumir un segundo número de secuencia"
        );
        m.StockRepo.Verify(
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
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid?>()
                ),
            Times.Once,
            "el reintento idempotente nunca debe duplicar el movimiento de inventario"
        );
    }

    [Fact]
    public async Task Mismo_ClientRequestId_contra_devolucion_ya_autorizada_con_otro_ClientRequestId_rechaza_PR_012()
    {
        var f = BuildFixture(paidAmount: 0m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var first = await handler.Handle(
            new AuthorizePurchaseReturnCommand(f.Return.Id, Guid.NewGuid()),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue();

        var result = await handler.Handle(
            new AuthorizePurchaseReturnCommand(f.Return.Id, Guid.NewGuid()), // CRI distinto
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }
}

file static class TestExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}

