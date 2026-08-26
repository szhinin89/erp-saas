using ERP.Application.Common;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
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

    private static (
        Mock<IPaymentRepository> payments,
        Mock<ISalesReceivableRepository> receivables,
        Mock<ICompanyFinancialDestinationRepository> financialDestinations,
        Mock<ICurrentTenant> tenant,
        Mock<ICurrentCompany> company,
        Mock<ICurrentUser> user
    ) BuildMocks()
    {
        var payments = new Mock<IPaymentRepository>();
        var receivables = new Mock<ISalesReceivableRepository>();
        var financialDestinations = new Mock<ICompanyFinancialDestinationRepository>();
        var tenant = new Mock<ICurrentTenant>();
        var company = new Mock<ICurrentCompany>();
        var user = new Mock<ICurrentUser>();

        tenant.Setup(t => t.TenantId).Returns(TenantId);
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        user.Setup(u => u.UserId).Returns(UserId);

        return (payments, receivables, financialDestinations, tenant, company, user);
    }

    private static RegisterCollectionCommandHandler BuildHandler(
        Mock<IPaymentRepository> payments,
        Mock<ISalesReceivableRepository> receivables,
        Mock<ICompanyFinancialDestinationRepository> financialDestinations,
        Mock<ICurrentTenant> tenant,
        Mock<ICurrentCompany> company,
        Mock<ICurrentUser> user
    ) =>
        new(
            payments.Object,
            receivables.Object,
            financialDestinations.Object,
            tenant.Object,
            company.Object,
            user.Object
        );

    private static CompanyFinancialDestination CashDestination(bool isActive = true)
    {
        var destination = CompanyFinancialDestination.Create(
            TenantId,
            CompanyId,
            "CAJA-01",
            "Caja Principal",
            FinancialDestinationTypeCode.CashRegister,
            Guid.NewGuid(),
            "USD",
            UserId,
            cashRegisterId: Guid.NewGuid()
        );
        if (!isActive)
            destination.SetActive(false, UserId);
        return destination;
    }

    [Fact]
    public async Task Cobro_valido_aplica_el_pago_y_actualiza_el_saldo_de_la_CxC()
    {
        var (payments, receivables, financialDestinations, tenant, company, user) = BuildMocks();
        var receivable = CreateReceivable(100m);
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);

        var handler = BuildHandler(payments, receivables, financialDestinations, tenant, company, user);
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            60m,
            new DateOnly(2026, 7, 30),
            null,
            "REF-1",
            new[] { new PaymentApplicationLineInput(receivable.Id, null, 60m) }
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
        var (payments, receivables, financialDestinations, tenant, company, user) = BuildMocks();
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

        var handler = BuildHandler(payments, receivables, financialDestinations, tenant, company, user);
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            100m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(receivable.Id, null, 100m) }
        );

        await handler.Handle(cmd, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DomainEvents.Should().ContainSingle(e => e is CollectionAppliedEvent);
    }

    [Fact]
    public async Task Cobro_con_InstallmentId_lo_propaga_a_la_linea_de_aplicacion_del_pago()
    {
        var (payments, receivables, financialDestinations, tenant, company, user) = BuildMocks();
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

        var handler = BuildHandler(payments, receivables, financialDestinations, tenant, company, user);
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(receivable.Id, installmentId, 50m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.Lines.Single().InstallmentId.Should().Be(installmentId);
    }

    [Fact]
    public async Task Cobro_que_excede_el_saldo_pendiente_retorna_ValidationFailure_sin_lanzar()
    {
        var (payments, receivables, financialDestinations, tenant, company, user) = BuildMocks();
        var receivable = CreateReceivable(100m);
        receivable.RegisterCollection(70m, UserId); // saldo restante: 30
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);

        var handler = BuildHandler(payments, receivables, financialDestinations, tenant, company, user);
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(receivable.Id, null, 50m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

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
        var (payments, receivables, financialDestinations, tenant, company, user) = BuildMocks();
        var missingId = Guid.NewGuid();
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalesReceivable?)null);

        var handler = BuildHandler(payments, receivables, financialDestinations, tenant, company, user);
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(missingId, null, 50m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Cobro_repartido_entre_dos_CxC_actualiza_el_saldo_de_ambas()
    {
        var (payments, receivables, financialDestinations, tenant, company, user) = BuildMocks();
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

        var handler = BuildHandler(payments, receivables, financialDestinations, tenant, company, user);
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
            }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        receivableA.PaidAmount.Should().Be(30m);
        receivableB.PaidAmount.Should().Be(50m);
    }

    [Fact]
    public async Task Cobro_con_destino_financiero_valido_lo_propaga_al_Payment()
    {
        var (payments, receivables, financialDestinations, tenant, company, user) = BuildMocks();
        var receivable = CreateReceivable(100m);
        var destination = CashDestination();
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);
        financialDestinations
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

        var handler = BuildHandler(payments, receivables, financialDestinations, tenant, company, user);
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            60m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(receivable.Id, null, 60m) },
            destination.Id
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.FinancialDestinationId.Should().Be(destination.Id);
    }

    [Fact]
    public async Task Cobro_con_destino_financiero_inactivo_retorna_ValidationFailure_sin_bloquear_por_falta_de_mapeo()
    {
        var (payments, receivables, financialDestinations, tenant, company, user) = BuildMocks();
        var receivable = CreateReceivable(100m);
        var destination = CashDestination(isActive: false);
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);
        financialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);

        var handler = BuildHandler(payments, receivables, financialDestinations, tenant, company, user);
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            60m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(receivable.Id, null, 60m) },
            destination.Id
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
        var (payments, receivables, financialDestinations, tenant, company, user) = BuildMocks();
        var receivable = CreateReceivable(100m);
        receivables
            .Setup(r => r.GetByIdAsync(TenantId, receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivable);

        var handler = BuildHandler(payments, receivables, financialDestinations, tenant, company, user);
        var cmd = new RegisterCollectionCommand(
            CustomerId,
            60m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(receivable.Id, null, 60m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        financialDestinations.Verify(
            f => f.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
