using ERP.Application.Common;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Application.Tests.Common;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Finance;

/// <summary>
/// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — cobertura de orquestación de
/// <see cref="RegisterCollectionCommandHandler"/>: hasta este fix, esta lógica existía sin ningún
/// test ni endpoint que la ejercitara. Los tests de reglas de negocio puras (balance, límites)
/// ya viven en <c>SalesReceivableTests</c>/<c>PaymentTests</c> (Domain) — aquí se cubre la
/// coordinación entre ambos aggregates que solo existe en este handler.
/// </summary>
public sealed class RegisterCollectionCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid InvoiceId = Guid.NewGuid();

    private static SalesReceivable CreateReceivable(decimal amount = 100m) =>
        SalesReceivable.Create(TenantId, CompanyId, InvoiceId, CustomerId, amount, UserId);

    private static readonly Guid BranchId = Guid.NewGuid();

    private static (
        Mock<IPaymentRepository> payments,
        Mock<ISalesReceivableRepository> receivables,
        Mock<ICompanyBankAccountRepository> bankAccounts,
        Mock<ICashRegisterRepository> cashRegisters,
        Mock<ICurrentTenant> tenant,
        Mock<ICurrentCompany> company,
        Mock<ICurrentUser> user
    ) BuildMocks()
    {
        var payments = new Mock<IPaymentRepository>();
        var receivables = new Mock<ISalesReceivableRepository>();
        var bankAccounts = new Mock<ICompanyBankAccountRepository>();
        var cashRegisters = new Mock<ICashRegisterRepository>();
        var tenant = new Mock<ICurrentTenant>();
        var company = new Mock<ICurrentCompany>();
        var user = new Mock<ICurrentUser>();

        tenant.Setup(t => t.TenantId).Returns(TenantId);
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        user.Setup(u => u.UserId).Returns(UserId);
        // La lectura bajo lock resuelve contra las CxC configuradas en GetByIdAsync de cada test.
        receivables
            .Setup(r =>
                r.GetByIdsForUpdateAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (Guid tid, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
                {
                    var found = new Dictionary<Guid, SalesReceivable>();
                    foreach (var id in ids.Distinct())
                        if (await receivables.Object.GetByIdAsync(tid, id, ct) is { } r)
                            found[id] = r;
                    return (IReadOnlyDictionary<Guid, SalesReceivable>)found;
                }
            );

        return (payments, receivables, bankAccounts, cashRegisters, tenant, company, user);
    }

    private static RegisterCollectionCommandHandler BuildHandler(
        Mock<IPaymentRepository> payments,
        Mock<ISalesReceivableRepository> receivables,
        Mock<ICompanyBankAccountRepository> bankAccounts,
        Mock<ICashRegisterRepository> cashRegisters,
        Mock<ICurrentTenant> tenant,
        Mock<ICurrentCompany> company,
        Mock<ICurrentUser> user
    ) =>
        new(
            payments.Object,
            receivables.Object,
            bankAccounts.Object,
            cashRegisters.Object,
            Mock.Of<IUnitOfWork>(),
            tenant.Object,
            company.Object,
            user.Object
        );

    private static CashRegister CashDestination(bool isActive = true)
    {
        var destination = CashRegister.Create(
            TenantId,
            CompanyId,
            BranchId,
            "CAJA-01",
            "Caja Principal",
            UserId
        );
        destination.SetAccountingAccount(Guid.NewGuid(), UserId);
        if (!isActive)
            destination.Disable(UserId);
        return destination;
    }

    [Fact]
    public async Task Cobro_valido_aplica_el_pago_y_actualiza_el_saldo_de_la_CxC()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) =
            BuildMocks();
        var receivable = CreateReceivable(100m);
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);

        var handler = BuildHandler(
            payments,
            receivables,
            bankAccounts,
            cashRegisters,
            tenant,
            company,
            user
        );
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            60m,
            new DateOnly(2026, 7, 30),
            null,
            "REF-1",
            new[] { new PaymentApplicationLineInput(receivable.Id, null, 60m) },
            ClientRequestId: Guid.NewGuid()
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        receivable.PaidAmount.Should().Be(60m);
        receivable.BalanceDue.Should().Be(40m);
        payments.Verify(
            p =>
                p.AddAsync(
                    It.IsAny<Domain.Modules.Finance.Entities.Payment>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cobro_valido_publica_CollectionAppliedEvent_en_el_Payment_persistido()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) =
            BuildMocks();
        var receivable = CreateReceivable(100m);
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);

        Domain.Modules.Finance.Entities.Payment? captured = null;
        payments
            .Setup(p =>
                p.AddAsync(
                    It.IsAny<Domain.Modules.Finance.Entities.Payment>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<Domain.Modules.Finance.Entities.Payment, CancellationToken>(
                (p, _) => captured = p
            )
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(
            payments,
            receivables,
            bankAccounts,
            cashRegisters,
            tenant,
            company,
            user
        );
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            100m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(receivable.Id, null, 100m) },
            ClientRequestId: Guid.NewGuid()
        );

        await handler.Handle(cmd, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DomainEvents.Should().ContainSingle(e => e is CollectionAppliedEvent);
    }

    [Fact]
    public async Task Cobro_con_InstallmentId_lo_propaga_a_la_linea_de_aplicacion_del_pago()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) =
            BuildMocks();
        var receivable = CreateReceivable(100m);
        receivable.GenerateInstallments(new DateOnly(2026, 7, 30), 30, 2);
        var installmentId = receivable.Installments[0].Id;
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);

        Domain.Modules.Finance.Entities.Payment? captured = null;
        payments
            .Setup(p =>
                p.AddAsync(
                    It.IsAny<Domain.Modules.Finance.Entities.Payment>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<Domain.Modules.Finance.Entities.Payment, CancellationToken>(
                (p, _) => captured = p
            )
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(
            payments,
            receivables,
            bankAccounts,
            cashRegisters,
            tenant,
            company,
            user
        );
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(receivable.Id, installmentId, 50m) },
            ClientRequestId: Guid.NewGuid()
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.Lines.Single().InstallmentId.Should().Be(installmentId);
    }

    [Fact]
    public async Task Cobro_que_excede_el_saldo_pendiente_retorna_ValidationFailure_sin_lanzar()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) =
            BuildMocks();
        var receivable = CreateReceivable(100m);
        receivable.RegisterCollection(70m, UserId); // saldo restante: 30
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);

        var handler = BuildHandler(
            payments,
            receivables,
            bankAccounts,
            cashRegisters,
            tenant,
            company,
            user
        );
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(receivable.Id, null, 50m) },
            ClientRequestId: Guid.NewGuid()
        );

        var result = await handler.HandleWithDomainRules(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("excede el saldo pendiente");
        receivable
            .PaidAmount.Should()
            .Be(70m, "el cobro rechazado no debe mutar el saldo ya registrado");
        payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cobro_sobre_CxC_inexistente_retorna_NotFound()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) =
            BuildMocks();
        var missingId = Guid.NewGuid();
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalesReceivable?)null);

        var handler = BuildHandler(
            payments,
            receivables,
            bankAccounts,
            cashRegisters,
            tenant,
            company,
            user
        );
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(missingId, null, 50m) },
            ClientRequestId: Guid.NewGuid()
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Cobro_repartido_entre_dos_CxC_actualiza_el_saldo_de_ambas()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) =
            BuildMocks();
        var receivableA = CreateReceivable(100m);
        var receivableB = SalesReceivable.Create(
            TenantId,
            CompanyId,
            Guid.NewGuid(),
            CustomerId,
            50m,
            UserId
        );
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivableA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivableA);
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivableB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivableB);

        var handler = BuildHandler(
            payments,
            receivables,
            bankAccounts,
            cashRegisters,
            tenant,
            company,
            user
        );
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            80m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[]
            {
                new PaymentApplicationLineInput(receivableA.Id, null, 30m),
                new PaymentApplicationLineInput(receivableB.Id, null, 50m),
            },
            ClientRequestId: Guid.NewGuid()
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        receivableA.PaidAmount.Should().Be(30m);
        receivableB.PaidAmount.Should().Be(50m);
    }

    [Fact]
    public async Task Cobro_con_destino_financiero_valido_lo_propaga_al_Payment()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) =
            BuildMocks();
        var receivable = CreateReceivable(100m);
        var destination = CashDestination();
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);
        cashRegisters
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);

        Domain.Modules.Finance.Entities.Payment? captured = null;
        payments
            .Setup(p =>
                p.AddAsync(
                    It.IsAny<Domain.Modules.Finance.Entities.Payment>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<Domain.Modules.Finance.Entities.Payment, CancellationToken>(
                (p, _) => captured = p
            )
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(
            payments,
            receivables,
            bankAccounts,
            cashRegisters,
            tenant,
            company,
            user
        );
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            60m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(receivable.Id, null, 60m) },
            CashRegisterId: destination.Id,
            ClientRequestId: Guid.NewGuid()
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.CashRegisterId.Should().Be(destination.Id);
    }

    [Fact]
    public async Task Cobro_con_destino_financiero_inactivo_retorna_ValidationFailure_sin_bloquear_por_falta_de_mapeo()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) =
            BuildMocks();
        var receivable = CreateReceivable(100m);
        var destination = CashDestination(isActive: false);
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);
        cashRegisters
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);

        var handler = BuildHandler(
            payments,
            receivables,
            bankAccounts,
            cashRegisters,
            tenant,
            company,
            user
        );
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            60m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(receivable.Id, null, 60m) },
            CashRegisterId: destination.Id,
            ClientRequestId: Guid.NewGuid()
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        payments.Verify(
            p =>
                p.AddAsync(
                    It.IsAny<Domain.Modules.Finance.Entities.Payment>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Cobro_sin_destino_financiero_no_consulta_el_repositorio_y_no_bloquea()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) =
            BuildMocks();
        var receivable = CreateReceivable(100m);
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);

        var handler = BuildHandler(
            payments,
            receivables,
            bankAccounts,
            cashRegisters,
            tenant,
            company,
            user
        );
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            60m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(receivable.Id, null, 60m) },
            ClientRequestId: Guid.NewGuid()
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        bankAccounts.Verify(
            f => f.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        cashRegisters.Verify(
            f => f.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── ZH-COLLECTIONS-CUSTOMER-SCOPE-01 ─────────────────────────────────────

    private static readonly Guid OtherCustomerId = Guid.NewGuid();

    private static SalesReceivable ReceivableOf(Guid customerId, decimal amount = 100m, int installments = 1)
    {
        var receivable = SalesReceivable.Create(TenantId, CompanyId, Guid.NewGuid(), customerId, amount, UserId);
        receivable.GenerateInstallments(new DateOnly(2026, 7, 1), 30 * installments, installments);
        return receivable;
    }

    private static void Expose(Mock<ISalesReceivableRepository> receivables, params SalesReceivable[] items)
    {
        foreach (var r in items)
            receivables
                .Setup(x => x.GetByIdAsync(TenantId, r.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(r);
    }

    private static RegisterCollectionCommand Collection(Guid customerId, params PaymentApplicationLineInput[] lines) =>
        new(
            customerId,
            lines.Sum(l => l.AppliedAmount),
            new DateOnly(2026, 7, 30),
            null,
            null,
            lines,
            ClientRequestId: Guid.NewGuid()
        );

    private static void ShouldNotPersist(Mock<IPaymentRepository> payments)
    {
        payments.Verify(
            p => p.AddAsync(It.IsAny<Domain.Modules.Finance.Entities.Payment>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cliente_A_no_puede_cobrar_la_CxC_del_cliente_B()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) = BuildMocks();
        var receivableOfB = ReceivableOf(OtherCustomerId);
        Expose(receivables, receivableOfB);
        var handler = BuildHandler(payments, receivables, bankAccounts, cashRegisters, tenant, company, user);

        var result = await handler.Handle(
            Collection(CustomerId, new PaymentApplicationLineInput(receivableOfB.Id, null, 40m)),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no pertenece al cliente del cobro");
        receivableOfB.PaidAmount.Should().Be(0m);
        ShouldNotPersist(payments);
    }

    [Fact]
    public async Task Una_linea_de_otro_cliente_rechaza_el_cobro_completo_sin_tocar_las_demas_CxC()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) = BuildMocks();
        var ownA = ReceivableOf(CustomerId);
        var ownB = ReceivableOf(CustomerId);
        var foreign = ReceivableOf(OtherCustomerId);
        Expose(receivables, ownA, ownB, foreign);
        var handler = BuildHandler(payments, receivables, bankAccounts, cashRegisters, tenant, company, user);

        var result = await handler.Handle(
            Collection(
                CustomerId,
                new PaymentApplicationLineInput(ownA.Id, null, 30m),
                new PaymentApplicationLineInput(foreign.Id, null, 30m),
                new PaymentApplicationLineInput(ownB.Id, null, 30m)
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        new[] { ownA, ownB, foreign }.Should().OnlyContain(r => r.PaidAmount == 0m);
        ShouldNotPersist(payments);
    }

    [Fact]
    public async Task Cuota_de_otra_CxC_se_rechaza()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) = BuildMocks();
        var target = ReceivableOf(CustomerId);
        var other = ReceivableOf(CustomerId);
        Expose(receivables, target, other);
        var handler = BuildHandler(payments, receivables, bankAccounts, cashRegisters, tenant, company, user);

        var result = await handler.Handle(
            Collection(CustomerId, new PaymentApplicationLineInput(target.Id, other.Installments[0].Id, 40m)),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no pertenece a la cuenta por cobrar");
        target.PaidAmount.Should().Be(0m);
        ShouldNotPersist(payments);
    }

    [Fact]
    public async Task Cuota_inexistente_se_rechaza()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) = BuildMocks();
        var target = ReceivableOf(CustomerId);
        Expose(receivables, target);
        var handler = BuildHandler(payments, receivables, bankAccounts, cashRegisters, tenant, company, user);

        var result = await handler.Handle(
            Collection(CustomerId, new PaymentApplicationLineInput(target.Id, Guid.NewGuid(), 40m)),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        ShouldNotPersist(payments);
    }

    [Fact]
    public async Task Cuota_propia_del_mismo_cliente_se_acepta()
    {
        var (payments, receivables, bankAccounts, cashRegisters, tenant, company, user) = BuildMocks();
        var target = ReceivableOf(CustomerId, 100m, installments: 2);
        Expose(receivables, target);
        var handler = BuildHandler(payments, receivables, bankAccounts, cashRegisters, tenant, company, user);

        var result = await handler.Handle(
            Collection(CustomerId, new PaymentApplicationLineInput(target.Id, target.Installments[1].Id, 50m)),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        target.PaidAmount.Should().Be(50m);
    }
}
