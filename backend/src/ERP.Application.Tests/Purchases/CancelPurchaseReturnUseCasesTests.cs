using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// P0-02 Fase 10 — <c>CancelPurchaseReturnHandler</c>: cancelar Draft (sin reversas); cancelar
/// Authorized sin crédito usado (reversa completa); cancelar Authorized con crédito íntegro
/// (movimiento <c>SourceReturnCancelled</c>, <c>Amount = OriginalAmount</c> exacto); rechazo
/// PR-011 con crédito parcialmente aplicado o reembolsado; idempotencia (§16.2); PR-009 en doble
/// cancelación con <c>ClientRequestId</c> distinto.
/// </summary>
public sealed class CancelPurchaseReturnUseCasesTests
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
        AccountsPayable Payable,
        PurchaseReturn Return
    );

    private static Fixture BuildDraftFixture(decimal returnQuantity = 3m)
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
            quantity: 10m,
            unitPrice: 100m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: ItemId,
            warehouseId: WarehouseId
        );
        line.ApplyTaxes("10", 12m, "IVA", null, 0m, null);
        invoice.ReplaceLines(new[] { line }, UserId);
        invoice.Confirm(UserId);
        var confirmedLine = invoice.Lines.Single();

        var payable = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.PurchaseInvoice, invoice.Id,
            "01", "001-001-000000001",
            invoice.IssueDate, invoice.IssueDate, UserId
        );
        payable.AddInstallment(
            1,
            invoice.IssueDate.AddDays(30),
            invoice.ConfirmedGrandTotal ?? confirmedLine.TaxInclusiveTotal
        );

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

    private static (Fixture Fixture, SupplierCredit? Credit) BuildAuthorizedFixture(
        decimal paidAmount,
        decimal returnQuantity = 10m
    )
    {
        var f = BuildDraftFixture(returnQuantity);
        if (paidAmount > 0)
            f.Payable.RegisterPayment(paidAmount, UserId);

        var snapshot = new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>
        {
            [f.Line.Id] = new PurchaseReturn.OriginalLineSnapshot(
                f.Line.Quantity,
                f.Line.LineSubtotal,
                f.Line.DiscountAmount,
                f.Line.VatAmount,
                f.Line.IceAmount,
                f.Line.VatCode!,
                f.Line.VatRate,
                f.Line.IceCode,
                f.Line.IceRate,
                f.Line.LandedUnitCost,
                []
            ),
        };
        var credit = f.Return.Authorize(
            "00000001",
            snapshot,
            balanceDueBeforeApplication: f.Payable.OutstandingAmount,
            f.Invoice.CurrencyCode,
            hasIssuedRetention: false,
            UserId,
            Guid.NewGuid(),
            "authorize-hash"
        );
        if (f.Return.AppliedToPayableAmount is > 0m)
            f.Payable.ApplyReturnCredit(f.Return.AppliedToPayableAmount.Value, UserId);

        return (f, credit);
    }

    private sealed class Mocks
    {
        public Mock<IPurchaseReturnRepository> ReturnRepo { get; } = new();
        public Mock<IPurchaseInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IAccountsPayableRepository> PayableRepo { get; } = new();
        public Mock<ISupplierCreditRepository> CreditRepo { get; } = new();
        public Mock<IStockRepository> StockRepo { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();
        public List<decimal> AppendedQuantities { get; } = new();

        public Mocks(Fixture f, SupplierCredit? credit = null)
        {
            ReturnRepo
                .Setup(r =>
                    r.GetPurchaseInvoiceIdAsync(
                        TenantId,
                        f.Return.Id,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(f.Invoice.Id);
            ReturnRepo
                .Setup(r => r.GetByIdAsync(TenantId, f.Return.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Return);

            InvoiceRepo
                .Setup(r => r.GetByIdAsync(TenantId, f.Invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Invoice);
            PayableRepo
                .Setup(r =>
                    r.GetByOriginAsync(
                        TenantId,
                        CompanyId,
                        AccountsPayableOriginType.PurchaseInvoice,
                        f.Invoice.Id,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(f.Payable);

            CreditRepo
                .Setup(r =>
                    r.GetIdBySourcePurchaseReturnIdAsync(
                        TenantId,
                        f.Return.Id,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(credit?.Id);
            if (credit is not null)
                CreditRepo
                    .Setup(r => r.GetByIdAsync(TenantId, credit.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(credit);

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
                        var sourceDocLineId = (Guid?)args[16];

                        AppendedQuantities.Add(qty);

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
                                null,
                                null,
                                sourceDocLineId
                            )
                        );
                    }
                );

            Uow.SetupGet(u => u.HasActiveTransaction).Returns(true);
        }

        public CancelPurchaseReturnHandler BuildHandler()
        {
            var t = new Mock<ICurrentTenant>();
            t.SetupGet(x => x.TenantId).Returns(TenantId);
            var u = new Mock<ICurrentUser>();
            u.SetupGet(x => x.UserId).Returns(UserId);

            return new CancelPurchaseReturnHandler(
                ReturnRepo.Object,
                InvoiceRepo.Object,
                PayableRepo.Object,
                CreditRepo.Object,
                StockRepo.Object,
                Uow.Object,
                DbEx.Object,
                t.Object,
                u.Object
            );
        }
    }

    [Fact]
    public async Task Cancelar_Draft_sin_reversas()
    {
        var f = BuildDraftFixture();
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new CancelPurchaseReturnCommand(f.Return.Id, "Ya no aplica", Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Return.Status.Should().Be(PurchaseReturnStatus.Cancelled);
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
        f.Payable.ReturnCreditAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Cancelar_Authorized_sin_credito_usado_reversa_completa()
    {
        var (f, credit) = BuildAuthorizedFixture(paidAmount: 0m);
        credit.Should().BeNull("factura impaga no genera excedente de crédito");
        var appliedBefore = f.Payable.ReturnCreditAmount;
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new CancelPurchaseReturnCommand(
                f.Return.Id,
                "Producto devuelto por error",
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Return.Status.Should().Be(PurchaseReturnStatus.Cancelled);
        f.Payable.ReturnCreditAmount.Should()
            .Be(0m, "la reversa debe deshacer exactamente lo aplicado");
        appliedBefore.Should().BeGreaterThan(0m);
        m.AppendedQuantities.Should().ContainSingle().Which.Should().Be(10m);
    }

    [Fact]
    public async Task Cancelar_Authorized_con_credito_integro_genera_movimiento_SourceReturnCancelled_por_OriginalAmount_exacto()
    {
        var (f, credit) = BuildAuthorizedFixture(paidAmount: 1120m);
        credit.Should().NotBeNull();
        var originalAmount = credit!.OriginalAmount;
        originalAmount.Should().BeGreaterThan(0m);
        var m = new Mocks(f, credit);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new CancelPurchaseReturnCommand(f.Return.Id, "Anulación total", Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        credit.AvailableAmount.Should().Be(0m);
        var lastMovement = credit.Movements.Last();
        lastMovement
            .MovementType.Should()
            .Be(
                ERP.Domain.Modules.Purchases.Enums.SupplierCreditMovementType.SourceReturnCancelled
            );
        lastMovement.Amount.Should().Be(originalAmount);
    }

    [Fact]
    public async Task Rechazo_PR_011_credito_parcialmente_aplicado_no_muta_nada()
    {
        var (f, credit) = BuildAuthorizedFixture(paidAmount: 1120m);
        credit.Should().NotBeNull();
        credit!.ApplyToPayable(Guid.NewGuid(), 10m, UserId, Guid.NewGuid(), "apply-hash");
        credit.AvailableAmount.Should().BeLessThan(credit.OriginalAmount);
        var m = new Mocks(f, credit);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new CancelPurchaseReturnCommand(f.Return.Id, "Intento de anulación", Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.Return.Status.Should().Be(PurchaseReturnStatus.Authorized);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rechazo_PR_011_credito_reembolsado_no_muta_nada()
    {
        var (f, credit) = BuildAuthorizedFixture(paidAmount: 1120m);
        credit.Should().NotBeNull();
        credit!.RegisterRefund(10m, UserId, Guid.NewGuid(), "refund-hash");
        credit.AvailableAmount.Should().BeLessThan(credit.OriginalAmount);
        var m = new Mocks(f, credit);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new CancelPurchaseReturnCommand(f.Return.Id, "Intento de anulación", Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.Return.Status.Should().Be(PurchaseReturnStatus.Authorized);
    }

    [Fact]
    public async Task Reintento_con_mismo_ClientRequestId_retorna_snapshot_sin_duplicar_reversas()
    {
        var (f, _) = BuildAuthorizedFixture(paidAmount: 0m);
        var clientRequestId = Guid.NewGuid();
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var first = await handler.Handle(
            new CancelPurchaseReturnCommand(f.Return.Id, "Motivo", clientRequestId),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue(first.Error);

        var retry = await handler.Handle(
            new CancelPurchaseReturnCommand(f.Return.Id, "Motivo", clientRequestId),
            CancellationToken.None
        );

        retry.IsSuccess.Should().BeTrue(retry.Error);
        m.AppendedQuantities.Should()
            .HaveCount(1, "el reintento idempotente nunca debe duplicar la reversa de inventario");
    }

    [Fact]
    public async Task Doble_cancelacion_con_ClientRequestId_distinto_rechaza_PR_009()
    {
        var f = BuildDraftFixture();
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var first = await handler.Handle(
            new CancelPurchaseReturnCommand(f.Return.Id, "Motivo 1", Guid.NewGuid()),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue(first.Error);

        var second = await handler.Handle(
            new CancelPurchaseReturnCommand(f.Return.Id, "Motivo 2", Guid.NewGuid()),
            CancellationToken.None
        );

        second.IsSuccess.Should().BeFalse();
        second.Error.Should().Contain("ya está cancelada");
    }
}
