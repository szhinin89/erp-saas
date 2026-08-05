using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Finance;

/// <summary>
/// P0-02 Fase 7 — ApplySupplierCreditHandler: aplicación feliz, sobreaplicación (SC-003),
/// proveedor distinto (SC-004), moneda distinta (SC-005), destino cancelled (SC-002),
/// idempotencia (SC-006) y orden de locks A→B.
/// </summary>
public sealed class ApplySupplierCreditUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid OtherSupplierId = Guid.NewGuid();
    private static readonly Guid PurchaseInvoiceId = Guid.NewGuid();
    private static readonly Guid PayableId = Guid.NewGuid();

    private sealed record Fixture(
        SupplierCredit Credit,
        PurchasePayable Payable,
        PurchaseInvoice Invoice
    );

    private static Fixture BuildFixture(
        decimal creditAmount = 100m,
        decimal payableTotal = 500m,
        Guid? supplierIdOverride = null,
        string? currencyOverride = null,
        bool payableCancelled = false
    )
    {
        var sourceReturnId = Guid.NewGuid();
        var credit = SupplierCredit.CreateFromReturn(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "USD",
            sourceReturnId,
            creditAmount,
            UserId
        );

        var payable = PurchasePayable.Create(
            TenantId,
            CompanyId,
            PurchaseInvoiceId,
            supplierIdOverride ?? SupplierId,
            payableTotal,
            UserId
        );
        if (payableCancelled)
            payable.CancelPayable();

        var invoice = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            supplierIdOverride ?? SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            Guid.NewGuid(),
            "Contado",
            1,
            30,
            currencyCode: currencyOverride ?? "USD"
        );

        return new Fixture(credit, payable, invoice);
    }

    private sealed class Mocks
    {
        public Mock<ISupplierCreditRepository> CreditRepo { get; } = new();
        public Mock<IPurchasePayableRepository> PayableRepo { get; } = new();
        public Mock<IPurchaseInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IPurchaseReturnRepository> ReturnRepo { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();

        public Mocks(Fixture f)
        {
            PayableRepo
                .Setup(r =>
                    r.GetPurchaseInvoiceIdAsync(TenantId, PayableId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(PurchaseInvoiceId);
            PayableRepo
                .Setup(r => r.GetByIdAsync(TenantId, PayableId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Payable);
            CreditRepo
                .Setup(r => r.GetByIdAsync(TenantId, f.Credit.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Credit);
            InvoiceRepo
                .Setup(r =>
                    r.GetByIdAsync(TenantId, PurchaseInvoiceId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(f.Invoice);

            Uow.SetupGet(u => u.HasActiveTransaction).Returns(true);
        }

        public ApplySupplierCreditHandler BuildHandler() =>
            new(
                CreditRepo.Object,
                PayableRepo.Object,
                InvoiceRepo.Object,
                ReturnRepo.Object,
                Uow.Object,
                DbEx.Object,
                new FixedCurrentTenant(),
                new FixedCurrentUser()
            );
    }

    [Fact]
    public async Task Aplicacion_feliz_reduce_AvailableAmount_y_aplica_contra_BalanceDue()
    {
        var f = BuildFixture(creditAmount: 100m, payableTotal: 500m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ApplySupplierCreditCommand(f.Credit.Id, PayableId, 60m, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.AvailableAmount.Should().Be(40m);
        f.Payable.SupplierCreditAppliedAmount.Should().Be(60m);
        f.Payable.BalanceDue.Should().Be(440m);
    }

    [Fact]
    public async Task Sobreaplicacion_rechaza_SC_003_sin_mutar_nada()
    {
        var f = BuildFixture(creditAmount: 50m, payableTotal: 500m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ApplySupplierCreditCommand(f.Credit.Id, PayableId, 60m, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.Credit.AvailableAmount.Should().Be(50m);
        f.Payable.SupplierCreditAppliedAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Proveedor_distinto_rechaza_SC_004()
    {
        var f = BuildFixture(supplierIdOverride: OtherSupplierId);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ApplySupplierCreditCommand(f.Credit.Id, PayableId, 10m, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("proveedor");
    }

    [Fact]
    public async Task Moneda_distinta_rechaza_SC_005()
    {
        var f = BuildFixture(currencyOverride: "EUR");
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ApplySupplierCreditCommand(f.Credit.Id, PayableId, 10m, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("moneda");
    }

    [Fact]
    public async Task Destino_cancelled_rechaza_SC_002()
    {
        var f = BuildFixture(payableCancelled: true);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ApplySupplierCreditCommand(f.Credit.Id, PayableId, 10m, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("anulada");
    }

    [Fact]
    public async Task Credito_inexistente_rechaza_SC_001_sin_mutar_nada()
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var missingCreditId = Guid.NewGuid();
        m.CreditRepo.Setup(r =>
                r.GetByIdAsync(TenantId, missingCreditId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((SupplierCredit?)null);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ApplySupplierCreditCommand(missingCreditId, PayableId, 10m, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Payable.SupplierCreditAppliedAmount.Should()
            .Be(0m, "no debe mutar el destino cuando el crédito no existe");
        m.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reintento_con_mismo_ClientRequestId_y_mismo_payload_retorna_snapshot_sin_duplicar()
    {
        var f = BuildFixture(creditAmount: 100m, payableTotal: 500m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();
        var cri = Guid.NewGuid();

        var first = await handler.Handle(
            new ApplySupplierCreditCommand(f.Credit.Id, PayableId, 60m, cri),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue();

        var retry = await handler.Handle(
            new ApplySupplierCreditCommand(f.Credit.Id, PayableId, 60m, cri),
            CancellationToken.None
        );

        retry.IsSuccess.Should().BeTrue();
        retry.Value!.AvailableAmount.Should().Be(40m);
        f.Credit.Movements.Should()
            .HaveCount(1, "no debe duplicar el movimiento en un reintento idempotente");
        f.Payable.SupplierCreditAppliedAmount.Should()
            .Be(60m, "no debe duplicar el efecto sobre el destino");
    }

    [Fact]
    public async Task Mismo_ClientRequestId_con_monto_distinto_rechaza_SC_006_sin_duplicar()
    {
        var f = BuildFixture(creditAmount: 100m, payableTotal: 500m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();
        var cri = Guid.NewGuid();

        var first = await handler.Handle(
            new ApplySupplierCreditCommand(f.Credit.Id, PayableId, 60m, cri),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue();

        var second = await handler.Handle(
            new ApplySupplierCreditCommand(f.Credit.Id, PayableId, 10m, cri),
            CancellationToken.None
        );

        second.IsSuccess.Should().BeFalse();
        f.Credit.Movements.Should().HaveCount(1);
        f.Credit.AvailableAmount.Should().Be(40m);
    }

    [Fact]
    public async Task Orden_de_locks_adquiere_LockA_antes_que_LockB()
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var sequence = new List<string>();
        m.ReturnRepo.Setup(r =>
                r.AcquireFinancialLockAsync(
                    TenantId,
                    PurchaseInvoiceId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => sequence.Add("LockA"))
            .Returns(Task.CompletedTask);
        m.CreditRepo.Setup(r =>
                r.AcquireLockAsync(TenantId, f.Credit.Id, It.IsAny<CancellationToken>())
            )
            .Callback(() => sequence.Add("LockB"))
            .Returns(Task.CompletedTask);
        var handler = m.BuildHandler();

        await handler.Handle(
            new ApplySupplierCreditCommand(f.Credit.Id, PayableId, 10m, Guid.NewGuid()),
            CancellationToken.None
        );

        sequence.Should().Equal("LockA", "LockB");
    }

    private sealed class FixedCurrentTenant : ICurrentTenant
    {
        public Guid TenantId => ApplySupplierCreditUseCasesTests.TenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentUser : ICurrentUser
    {
        public Guid UserId => ApplySupplierCreditUseCasesTests.UserId;
        public bool IsAuthenticated => true;
        public string? Username => "tester";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }
}
