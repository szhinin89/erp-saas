using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Finance;

/// <summary>
/// P0-02 Fase 7 — ReverseSupplierCreditApplicationHandler: reversa feliz, reversa de movimiento ya
/// revertido (SC-011), reversa con destino cancelado después de la aplicación (SC-014),
/// idempotencia (SC-006), mismo orden de locks A→B.
/// </summary>
public sealed class ReverseSupplierCreditApplicationUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PurchaseInvoiceId = Guid.NewGuid();
    private static readonly Guid PayableId = Guid.NewGuid();
    private static readonly Guid OtherPurchaseInvoiceId = Guid.NewGuid();
    private static readonly Guid OtherPayableId = Guid.NewGuid();

    private sealed record Fixture(
        SupplierCredit Credit,
        AccountsPayable Payable,
        Guid ApplicationMovementId
    );

    private static Fixture BuildFixture(decimal creditAmount = 100m, decimal appliedAmount = 60m)
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

        var payable = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.PurchaseInvoice, PurchaseInvoiceId,
            "01", "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), UserId
        );
        payable.AddInstallment(1, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), 500m);

        var applyHash = ApplySupplierCreditHandler.ComputeApplyPayloadHash(
            credit.Id,
            PayableId,
            appliedAmount
        );
        var movement = credit.ApplyToPayable(
            PayableId,
            appliedAmount,
            UserId,
            Guid.NewGuid(),
            applyHash
        );
        payable.ApplySupplierCredit(appliedAmount, UserId);

        return new Fixture(credit, payable, movement.Id);
    }

    private sealed class Mocks
    {
        public Mock<ISupplierCreditRepository> CreditRepo { get; } = new();
        public Mock<IAccountsPayableRepository> PayableRepo { get; } = new();
        public Mock<IPurchaseReturnRepository> ReturnRepo { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();

        public Mocks(Fixture f)
        {
            PayableRepo
                .Setup(r =>
                    r.GetOriginIdAsync(TenantId, PayableId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(PurchaseInvoiceId);
            PayableRepo
                .Setup(r => r.GetByIdAsync(TenantId, PayableId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Payable);
            CreditRepo
                .Setup(r => r.GetByIdAsync(TenantId, f.Credit.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Credit);

            Uow.SetupGet(u => u.HasActiveTransaction).Returns(true);
        }

        /// <summary>Registra un segundo PurchasePayable real (distinto del destino verdadero del movimiento) — usado únicamente por el test que envía un TargetPurchasePayableId que no coincide.</summary>
        public void RegisterOtherPayable(AccountsPayable otherPayable)
        {
            PayableRepo
                .Setup(r =>
                    r.GetOriginIdAsync(
                        TenantId,
                        OtherPayableId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(OtherPurchaseInvoiceId);
            PayableRepo
                .Setup(r => r.GetByIdAsync(TenantId, OtherPayableId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(otherPayable);
        }

        public ReverseSupplierCreditApplicationHandler BuildHandler() =>
            new(
                CreditRepo.Object,
                PayableRepo.Object,
                ReturnRepo.Object,
                Uow.Object,
                DbEx.Object,
                new FixedCurrentTenant(),
                new FixedCurrentUser()
            );
    }

    [Fact]
    public async Task Reversa_feliz_restituye_AvailableAmount_y_reduce_SupplierCreditAmount()
    {
        var f = BuildFixture(creditAmount: 100m, appliedAmount: 60m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ReverseSupplierCreditApplicationCommand(
                f.Credit.Id,
                f.ApplicationMovementId,
                PayableId,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.AvailableAmount.Should().Be(100m);
        f.Payable.SupplierCreditAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Credito_inexistente_rechaza_SC_001_sin_ejecutar_reversa()
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
            new ReverseSupplierCreditApplicationCommand(
                missingCreditId,
                f.ApplicationMovementId,
                PayableId,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Payable.SupplierCreditAmount.Should()
            .Be(60m, "no debe mutar el destino cuando el crédito no existe");
        m.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TargetPurchasePayableId_que_no_coincide_con_el_destino_real_rechaza_la_reversa()
    {
        var f = BuildFixture(creditAmount: 100m, appliedAmount: 60m);
        var m = new Mocks(f);
        var otherPayable = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.PurchaseInvoice, OtherPurchaseInvoiceId,
            "01", "001-001-000000002",
            DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), UserId
        );
        otherPayable.AddInstallment(1, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), 300m);
        m.RegisterOtherPayable(otherPayable);
        var handler = m.BuildHandler();

        // El movimiento original apunta a PayableId, pero el comando envía OtherPayableId.
        var result = await handler.Handle(
            new ReverseSupplierCreditApplicationCommand(
                f.Credit.Id,
                f.ApplicationMovementId,
                OtherPayableId,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no corresponde al destino real");

        // Ni el crédito ni ninguno de los dos PurchasePayable deben mutar.
        f.Credit.AvailableAmount.Should().Be(40m);
        f.Credit.Movements.Should().HaveCount(1, "no debe crearse un movimiento de reversa");
        f.Payable.SupplierCreditAmount.Should()
            .Be(60m, "el destino real del movimiento original no debe mutar");
        otherPayable
            .SupplierCreditAmount.Should()
            .Be(0m, "el destino incorrecto enviado por el comando tampoco debe mutar");
        m.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Revertir_movimiento_ya_revertido_rechaza_SC_011()
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var first = await handler.Handle(
            new ReverseSupplierCreditApplicationCommand(
                f.Credit.Id,
                f.ApplicationMovementId,
                PayableId,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue();

        var second = await handler.Handle(
            new ReverseSupplierCreditApplicationCommand(
                f.Credit.Id,
                f.ApplicationMovementId,
                PayableId,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        second.IsSuccess.Should().BeFalse();
        second.Error.Should().Contain("ya fue revertido");
    }

    [Fact]
    public async Task Revertir_con_destino_cancelado_despues_de_la_aplicacion_rechaza_SC_014()
    {
        var f = BuildFixture();
        f.Payable.Cancel(UserId);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ReverseSupplierCreditApplicationCommand(
                f.Credit.Id,
                f.ApplicationMovementId,
                PayableId,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("anulada");
        f.Credit.AvailableAmount.Should()
            .Be(40m, "el crédito no debe mutar cuando la reversa queda bloqueada");
    }

    [Fact]
    public async Task Reintento_con_mismo_ClientRequestId_retorna_snapshot_sin_duplicar()
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var handler = m.BuildHandler();
        var cri = Guid.NewGuid();

        var first = await handler.Handle(
            new ReverseSupplierCreditApplicationCommand(
                f.Credit.Id,
                f.ApplicationMovementId,
                PayableId,
                cri
            ),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue();

        var retry = await handler.Handle(
            new ReverseSupplierCreditApplicationCommand(
                f.Credit.Id,
                f.ApplicationMovementId,
                PayableId,
                cri
            ),
            CancellationToken.None
        );

        retry.IsSuccess.Should().BeTrue();
        f.Credit.Movements.Should()
            .HaveCount(
                2,
                "1 aplicación (fixture) + 1 reversa — el reintento no debe agregar una tercera"
            );
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
            new ReverseSupplierCreditApplicationCommand(
                f.Credit.Id,
                f.ApplicationMovementId,
                PayableId,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        sequence.Should().Equal("LockA", "LockB");
    }

    private sealed class FixedCurrentTenant : ICurrentTenant
    {
        public Guid TenantId => ReverseSupplierCreditApplicationUseCasesTests.TenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentUser : ICurrentUser
    {
        public Guid UserId => ReverseSupplierCreditApplicationUseCasesTests.UserId;
        public bool IsAuthenticated => true;
        public string? Username => "tester";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }
}
