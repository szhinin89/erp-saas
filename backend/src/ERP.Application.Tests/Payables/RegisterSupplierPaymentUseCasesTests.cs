using ERP.Application.Common;
using ERP.Application.Modules.Payables.Exceptions;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Payables;

/// <summary>
/// SUPPLIER-PAYMENTS-REGISTER-15C — cobertura de orquestación de
/// <see cref="RegisterSupplierPaymentCommandHandler"/>: validaciones que requieren lecturas de
/// repositorio (medios de pago, destinos financieros, cuotas de <c>AccountsPayable</c>) y la
/// coordinación entre <c>SupplierPayment</c> y <c>AccountsPayable</c>/<c>AccountsPayableInstallment</c>
/// dentro de una única transacción. Las reglas de balance puras del agregado ya están cubiertas en
/// <c>SupplierPaymentTests</c> (Domain).
/// </summary>
public sealed class RegisterSupplierPaymentUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private const string SystemNumber = "00000001";

    private sealed record Mocks(
        Mock<ISupplierPaymentRepository> SupplierPayments,
        Mock<ISupplierPaymentSequenceRepository> Sequences,
        Mock<IAccountsPayableRepository> AccountsPayables,
        Mock<IPaymentMethodRepository> PaymentMethods,
        Mock<ICompanyFinancialDestinationRepository> FinancialDestinations,
        Mock<IUnitOfWork> Uow,
        Mock<ICurrentTenant> Tenant,
        Mock<ICurrentCompany> Company,
        Mock<ICurrentBranch> Branch,
        Mock<ICurrentUser> User
    );

    private static Mocks BuildMocks()
    {
        var supplierPayments = new Mock<ISupplierPaymentRepository>();
        var sequences = new Mock<ISupplierPaymentSequenceRepository>();
        var accountsPayables = new Mock<IAccountsPayableRepository>();
        var paymentMethods = new Mock<IPaymentMethodRepository>();
        var financialDestinations = new Mock<ICompanyFinancialDestinationRepository>();
        var uow = new Mock<IUnitOfWork>();
        var tenant = new Mock<ICurrentTenant>();
        var company = new Mock<ICurrentCompany>();
        var branch = new Mock<ICurrentBranch>();
        var user = new Mock<ICurrentUser>();

        tenant.Setup(t => t.TenantId).Returns(TenantId);
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        branch.Setup(b => b.BranchId).Returns(BranchId);
        user.Setup(u => u.UserId).Returns(UserId);
        sequences
            .Setup(s => s.CaptureNextAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SystemNumber);
        supplierPayments
            .Setup(r =>
                r.ExistsByReceiptNumberAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);

        return new Mocks(
            supplierPayments,
            sequences,
            accountsPayables,
            paymentMethods,
            financialDestinations,
            uow,
            tenant,
            company,
            branch,
            user
        );
    }

    private static RegisterSupplierPaymentCommandHandler BuildHandler(Mocks m) =>
        new(
            m.SupplierPayments.Object,
            m.Sequences.Object,
            m.AccountsPayables.Object,
            m.PaymentMethods.Object,
            m.FinancialDestinations.Object,
            m.Uow.Object,
            m.Tenant.Object,
            m.Company.Object,
            m.Branch.Object,
            m.User.Object
        );

    private static PaymentMethod ActivePaymentMethod() =>
        PaymentMethod.Create(TenantId, "EFEC", "Efectivo", false, false, 1, UserId);

    private static CompanyFinancialDestination ActiveDestination(Guid companyId) =>
        CompanyFinancialDestination.Create(
            TenantId,
            companyId,
            "CAJA-01",
            "Caja Principal",
            FinancialDestinationTypeCode.CashRegister,
            Guid.NewGuid(),
            "USD",
            UserId,
            cashRegisterId: Guid.NewGuid()
        );

    private static AccountsPayable CreatePayableWithInstallment(
        decimal amount,
        Guid? supplierId = null,
        Guid? companyId = null
    )
    {
        var payable = AccountsPayable.CreateFromOrigin(
            TenantId,
            companyId ?? CompanyId,
            BranchId,
            supplierId ?? SupplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(),
            "01",
            "001-001-000000001",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 1),
            UserId
        );
        payable.AddInstallment(1, new DateOnly(2026, 9, 1), amount);
        return payable;
    }

    private void SetupMethodAndDestination(
        Mocks m,
        PaymentMethod method,
        CompanyFinancialDestination destination
    )
    {
        m.PaymentMethods
            .Setup(p => p.GetByIdAsync(TenantId, method.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(method);
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
    }

    private void SetupPayable(Mocks m, AccountsPayable payable)
    {
        var installmentId = payable.Installments[0].Id;
        m.AccountsPayables
            .Setup(a => a.GetByInstallmentIdAsync(TenantId, installmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);
    }

    [Fact]
    public async Task Pago_valido_1_medio_1_cuota_confirma_y_paga_la_cuota_por_completo()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(300m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            300m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 300m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 300m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 300m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SystemNumber.Should().Be(SystemNumber);
        result.Value!.DisplayNumber.Should().Be(SystemNumber);
        payable.Installments[0].Status.Should().Be(AccountsPayableStatus.Paid);
        payable.OutstandingAmount.Should().Be(0m);
        m.SupplierPayments.Verify(
            r => r.AddAsync(It.IsAny<SupplierPayment>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pago_valido_2_medios_1_cuota()
    {
        var m = BuildMocks();
        var methodA = ActivePaymentMethod();
        var methodB = PaymentMethod.Create(TenantId, "TRANS", "Transferencia", true, false, 2, UserId);
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(300m);
        SetupMethodAndDestination(m, methodA, destination);
        m.PaymentMethods
            .Setup(p => p.GetByIdAsync(TenantId, methodB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(methodB);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            300m,
            null,
            new[]
            {
                new SupplierPaymentMethodLineRequest(methodA.Id, destination.Id, 100m),
                new SupplierPaymentMethodLineRequest(methodB.Id, destination.Id, 200m),
            },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 300m) },
            new[]
            {
                new SupplierPaymentAllocationLineRequest(0, 0, 100m),
                new SupplierPaymentAllocationLineRequest(1, 0, 200m),
            }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MethodLines.Should().HaveCount(2);
        payable.Installments[0].Status.Should().Be(AccountsPayableStatus.Paid);
    }

    [Fact]
    public async Task Pago_valido_1_medio_2_cuotas_actualiza_ambas_cuotas_y_la_cabecera_de_cada_payable()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payableA = CreatePayableWithInstallment(100m);
        var payableB = CreatePayableWithInstallment(200m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payableA);
        SetupPayable(m, payableB);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            300m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 300m) },
            new[]
            {
                new SupplierPaymentApplicationLineRequest(payableA.Installments[0].Id, 100m),
                new SupplierPaymentApplicationLineRequest(payableB.Installments[0].Id, 200m),
            },
            new[]
            {
                new SupplierPaymentAllocationLineRequest(0, 0, 100m),
                new SupplierPaymentAllocationLineRequest(0, 1, 200m),
            }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payableA.Installments[0].Status.Should().Be(AccountsPayableStatus.Paid);
        payableB.Installments[0].Status.Should().Be(AccountsPayableStatus.Paid);
        payableA.OutstandingAmount.Should().Be(0m);
        payableB.OutstandingAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Pago_valido_2_medios_2_cuotas_matriz_cruzada()
    {
        var m = BuildMocks();
        var methodA = ActivePaymentMethod();
        var methodB = PaymentMethod.Create(TenantId, "TRANS", "Transferencia", true, false, 2, UserId);
        var destination = ActiveDestination(CompanyId);
        var payableA = CreatePayableWithInstallment(150m);
        var payableB = CreatePayableWithInstallment(150m);
        SetupMethodAndDestination(m, methodA, destination);
        m.PaymentMethods
            .Setup(p => p.GetByIdAsync(TenantId, methodB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(methodB);
        SetupPayable(m, payableA);
        SetupPayable(m, payableB);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            300m,
            "CHK-001",
            new[]
            {
                new SupplierPaymentMethodLineRequest(methodA.Id, destination.Id, 150m),
                new SupplierPaymentMethodLineRequest(methodB.Id, destination.Id, 150m),
            },
            new[]
            {
                new SupplierPaymentApplicationLineRequest(payableA.Installments[0].Id, 150m),
                new SupplierPaymentApplicationLineRequest(payableB.Installments[0].Id, 150m),
            },
            new[]
            {
                new SupplierPaymentAllocationLineRequest(0, 0, 100m),
                new SupplierPaymentAllocationLineRequest(0, 1, 50m),
                new SupplierPaymentAllocationLineRequest(1, 0, 50m),
                new SupplierPaymentAllocationLineRequest(1, 1, 100m),
            }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ReceiptNumber.Should().Be("CHK-001");
        result.Value!.DisplayNumber.Should().Be("CHK-001");
        payableA.Installments[0].Status.Should().Be(AccountsPayableStatus.Paid);
        payableB.Installments[0].Status.Should().Be(AccountsPayableStatus.Paid);
    }

    [Fact]
    public async Task ReceiptNumber_vacio_se_guarda_como_null_y_DisplayNumber_usa_SystemNumber()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            "   ",
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 100m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 100m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ReceiptNumber.Should().BeNull();
        result.Value!.DisplayNumber.Should().Be(SystemNumber);
    }

    [Fact]
    public async Task Cuota_pagada_parcialmente_queda_PartiallyPaid()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(300m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 100m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 100m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payable.Installments[0].Status.Should().Be(AccountsPayableStatus.PartiallyPaid);
        payable.OutstandingAmount.Should().Be(200m);
    }

    [Fact]
    public async Task Bloquea_pago_mayor_al_saldo_pendiente_de_la_cuota()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            150m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 150m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 150m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 150m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        payable.Installments[0].PaidAmount.Should().Be(0m, "el rechazo no debe mutar el saldo");
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Bloquea_cuota_de_otro_proveedor()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m, supplierId: Guid.NewGuid());
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 100m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 100m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("distintos proveedores");
    }

    [Fact]
    public async Task Bloquea_cuota_de_otra_empresa()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m, companyId: Guid.NewGuid());
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 100m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 100m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no pertenece a esta empresa");
    }

    [Fact]
    public async Task Bloquea_cuota_ya_pagada()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        payable.RegisterPaymentToInstallment(payable.Installments[0].Id, 100m, UserId);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            50m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 50m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 50m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 50m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no admite pagos");
    }

    [Fact]
    public async Task Bloquea_cuota_anulada()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        payable.Cancel(UserId);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            50m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 50m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 50m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 50m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no admite pagos");
    }

    [Fact]
    public async Task Bloquea_destino_financiero_inexistente_o_de_otra_empresa()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(Guid.NewGuid()); // otra empresa
        var payable = CreatePayableWithInstallment(100m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 100m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 100m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Bloquea_destino_financiero_inactivo()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        destination.SetActive(false, UserId);
        var payable = CreatePayableWithInstallment(100m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 100m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 100m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Bloquea_payment_method_inexistente()
    {
        var m = BuildMocks();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        var missingMethodId = Guid.NewGuid();
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(missingMethodId, destination.Id, 100m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 100m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no existe o no está activo");
    }

    [Fact]
    public async Task Bloquea_payment_method_inactivo()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        method.Disable(UserId);
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 100m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 100m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no existe o no está activo");
    }

    [Fact]
    public async Task Bloquea_desbalance_entre_medios_y_aplicaciones_y_hace_rollback()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(300m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        // Medios suman 300, aplicación solo 250 — el agregado de dominio rechaza el desbalance.
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            300m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 300m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 250m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 250m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        m.SupplierPayments.Verify(
            r => r.AddAsync(It.IsAny<SupplierPayment>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        payable.Installments[0].PaidAmount.Should().Be(0m);
    }

    [Fact]
    public async Task ReceiptNumber_duplicado_para_el_mismo_proveedor_retorna_Conflict()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);
        m.SupplierPayments
            .Setup(r =>
                r.ExistsByReceiptNumberAsync(
                    TenantId,
                    CompanyId,
                    SupplierId,
                    "CHK-001",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            "CHK-001",
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 100m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 100m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
    }

    /// <summary>
    /// SUPPLIER-PAYMENTS-POSTING-15D — <c>SupplierPaymentConfirmedPostingTranslator</c> lanza
    /// <see cref="SupplierPaymentPostingFailedException"/> dentro del <c>Publish()</c> interno de
    /// <c>ErpDbContext.SaveChangesAsync</c> (ADR-026 §8) cuando el asiento no puede generarse — esa
    /// excepción se propaga hacia arriba y sale por el mismo <c>_supplierPayments.SaveChangesAsync</c>
    /// que este handler ya envuelve. Verifica el cableado de ese catch específico: rollback completo,
    /// nunca commit, "no confirmar pago sin asiento".
    /// </summary>
    [Fact]
    public async Task Fallo_de_posting_hace_rollback_completo_y_no_confirma_el_pago()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);
        m.SupplierPayments
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new SupplierPaymentPostingFailedException(
                    "El destino financiero no tiene una cuenta contable configurada.",
                    "POSTING_ACCOUNT_INVALID"
                )
            );

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, destination.Id, 100m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 100m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("POSTING_ACCOUNT_INVALID");
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
