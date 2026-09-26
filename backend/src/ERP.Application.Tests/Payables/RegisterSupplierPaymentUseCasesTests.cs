using ERP.Application.Common;
using ERP.Application.Modules.Payables.Exceptions;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
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
        Mock<ICompanyBankAccountRepository> BankAccounts,
        Mock<ICashRegisterRepository> CashRegisters,
        Mock<ICashSessionRepository> CashSessions,
        Mock<ISupplierCreditRepository> SupplierCredits,
        Mock<IOperationalPreferencesResolver> Preferences,
        Mock<ICompanyRepository> Companies,
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
        var bankAccounts = new Mock<ICompanyBankAccountRepository>();
        var cashRegisters = new Mock<ICashRegisterRepository>();
        var cashSessions = new Mock<ICashSessionRepository>();
        var supplierCredits = new Mock<ISupplierCreditRepository>();
        var preferences = new Mock<IOperationalPreferencesResolver>();
        var companies = new Mock<ICompanyRepository>();
        var uow = new Mock<IUnitOfWork>();
        companies
            .Setup(c => c.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyEntity { Id = CompanyId, TenantId = TenantId, CurrencyCode = "USD" });
        SetAllowWithoutPayable(preferences, false);
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
            bankAccounts,
            cashRegisters,
            cashSessions,
            supplierCredits,
            preferences,
            companies,
            uow,
            tenant,
            company,
            branch,
            user
        );
    }

    /// <summary>ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — política de empresa payables.allow_supplier_payment_without_payable.</summary>
    private static void SetAllowWithoutPayable(Mock<IOperationalPreferencesResolver> preferences, bool allow) =>
        preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalPreferences(
                    null!,
                    null!,
                    new PurchasesPreferences(null, true, true, true, false),
                    null!,
                    null!,
                    null!,
                    null!,
                    new PayablesPreferences(allow)
                )
            );

    private static RegisterSupplierPaymentCommandHandler BuildHandler(Mocks m) =>
        new(
            m.SupplierPayments.Object,
            m.Sequences.Object,
            m.AccountsPayables.Object,
            m.PaymentMethods.Object,
            m.BankAccounts.Object,
            m.CashRegisters.Object,
            m.CashSessions.Object,
            m.SupplierCredits.Object,
            m.Preferences.Object,
            m.Companies.Object,
            m.Uow.Object,
            m.Tenant.Object,
            m.Company.Object,
            m.Branch.Object,
            m.User.Object
        );

    private static PaymentMethod ActivePaymentMethod() =>
        PaymentMethod.Create(
            TenantId,
            "EFEC",
            "Efectivo",
            false,
            false,
            1,
            UserId,
            affectsPhysicalCash: true
        );

    private static PaymentMethod TransferMethod() =>
        PaymentMethod.Create(
            TenantId,
            "TRANS",
            "Transferencia",
            true,
            false,
            2,
            UserId,
            PaymentMethodDetailType.Transfer
        );

    private static CompanyBankAccount ActiveBankAccount(Guid companyId) =>
        CompanyBankAccount.Create(
            TenantId,
            companyId,
            Guid.NewGuid(),
            BankAccountType.Checking,
            "2200123456",
            "Banco Pichincha",
            Guid.NewGuid(),
            UserId
        );

    private static CashSession OpenSession(Guid cashRegisterId, decimal openingAmount = 500m) =>
        CashSession.Open(
            TenantId,
            CompanyId,
            BranchId,
            UserId,
            cashRegisterId,
            "CAJA-01",
            "Caja Principal",
            Guid.NewGuid(),
            "001",
            openingAmount,
            UserId
        );

    private static void SetupTransfer(Mocks m, PaymentMethod method, CompanyBankAccount bankAccount)
    {
        m.PaymentMethods
            .Setup(p => p.GetByIdAsync(TenantId, method.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(method);
        m.BankAccounts
            .Setup(b => b.GetByIdAsync(TenantId, bankAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bankAccount);
    }

    private static CashRegister ActiveDestination(Guid companyId)
    {
        var register = CashRegister.Create(TenantId, companyId, BranchId, "CAJA-01", "Caja Principal", UserId);
        register.SetAccountingAccount(Guid.NewGuid(), UserId);
        return register;
    }

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

    private static CashSession SetupMethodAndDestination(
        Mocks m,
        PaymentMethod method,
        CashRegister destination,
        decimal openingAmount = 500m
    )
    {
        m.PaymentMethods
            .Setup(p => p.GetByIdAsync(TenantId, method.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(method);
        m.CashRegisters
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        var session = OpenSession(destination.Id, openingAmount);
        m.CashSessions
            .Setup(r =>
                r.GetOpenByCashRegisterForUpdateAsync(TenantId, destination.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(session);
        return session;
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 300m) },
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
        var methodB = TransferMethod();
        var destination = ActiveDestination(CompanyId);
        var bankAccount = ActiveBankAccount(CompanyId);
        var payable = CreatePayableWithInstallment(300m);
        SetupMethodAndDestination(m, methodA, destination);
        SetupTransfer(m, methodB, bankAccount);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            300m,
            null,
            new[]
            {
                new SupplierPaymentMethodLineRequest(methodA.Id, null, destination.Id, 100m),
                new SupplierPaymentMethodLineRequest(methodB.Id, bankAccount.Id, null, 200m, "OP-123", TransactionDate: new DateOnly(2026, 8, 28)),
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 300m) },
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
        var methodB = TransferMethod();
        var destination = ActiveDestination(CompanyId);
        var bankAccount = ActiveBankAccount(CompanyId);
        var payableA = CreatePayableWithInstallment(150m);
        var payableB = CreatePayableWithInstallment(150m);
        SetupMethodAndDestination(m, methodA, destination);
        SetupTransfer(m, methodB, bankAccount);
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
                new SupplierPaymentMethodLineRequest(methodA.Id, null, destination.Id, 150m),
                new SupplierPaymentMethodLineRequest(methodB.Id, bankAccount.Id, null, 150m, "OP-456", TransactionDate: new DateOnly(2026, 8, 28)),
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m) },
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m) },
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 150m) },
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m) },
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m) },
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 50m) },
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 50m) },
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m) },
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
        destination.Disable(UserId);
        var payable = CreatePayableWithInstallment(100m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m) },
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
        m.CashRegisters
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(missingMethodId, null, destination.Id, 100m) },
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 100m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no existe o no está activo");
    }

    [Fact]
    public async Task Excedente_sin_confirmar_se_rechaza_sin_ningun_efecto()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(300m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var handler = BuildHandler(m);
        // ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — medios 300, aplicación 250: 50 sin aplicar
        // SIN confirmación explícita ⇒ rechazo antes de cualquier efecto (ni transacción, ni
        // secuencia, ni SupplierPayment, ni SupplierCredit, ni CxP).
        var cmd = new RegisterSupplierPaymentCommand(
            SupplierId,
            new DateOnly(2026, 8, 28),
            300m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 300m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 250m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 250m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Contain("50.00");
        m.SupplierPayments.Verify(
            r => r.AddAsync(It.IsAny<SupplierPayment>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.SupplierCredits.Verify(
            r => r.AddAsync(It.IsAny<SupplierCredit>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.Sequences.Verify(
            s => s.CaptureNextAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.Uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m) },
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
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m) },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 100m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("POSTING_ACCOUNT_INVALID");
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A
    // ══════════════════════════════════════════════════════════════════════

    private RegisterSupplierPaymentCommand SingleLineCommand(
        SupplierPaymentMethodLineRequest line,
        AccountsPayable payable,
        decimal amount
    ) =>
        new(
            SupplierId,
            new DateOnly(2026, 8, 28),
            amount,
            null,
            new[] { line },
            new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, amount) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, amount) }
        );

    [Fact]
    public async Task Efectivo_registra_egreso_SupplierPayment_en_la_sesion_abierta_y_baja_el_saldo_esperado()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(120m);
        var session = SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);
        var expectedBefore = session.CurrentBalance;

        var result = await BuildHandler(m).Handle(
            SingleLineCommand(new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 120m), payable, 120m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.MethodLines.Single();
        line.CashSessionId.Should().Be(session.Id);
        line.CashMovementId.Should().NotBeNull();
        line.TransactionDate.Should().BeNull("una fuente de caja no lleva fecha bancaria");

        var movement = session.Movements.Single(x => x.Id == line.CashMovementId);
        movement.MovementType.Should().Be(CashMovementType.SupplierPayment);
        movement.Amount.Should().Be(120m);
        movement.ReferenceType.Should().Be(CashReferenceType.SupplierPayment);
        movement.ReferenceId.Should().Be(result.Value.Id);
        movement.ReferenceNumber.Should().Be(SystemNumber);
        session.CurrentBalance.Should().Be(expectedBefore - 120m);
    }

    [Fact]
    public async Task Efectivo_sin_sesion_de_caja_abierta_se_rechaza_con_rollback()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        SetupMethodAndDestination(m, method, destination);
        m.CashSessions
            .Setup(r =>
                r.GetOpenByCashRegisterForUpdateAsync(TenantId, destination.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CashSession?)null);
        SetupPayable(m, payable);

        var result = await BuildHandler(m).Handle(
            SingleLineCommand(new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m), payable, 100m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("sesión de caja abierta");
        payable.Installments[0].PaidAmount.Should().Be(0m);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Transferencia_no_crea_movimiento_de_caja_y_guarda_cuenta_fecha_referencia_y_monto()
    {
        var m = BuildMocks();
        var method = TransferMethod();
        var bankAccount = ActiveBankAccount(CompanyId);
        var payable = CreatePayableWithInstallment(250m);
        SetupTransfer(m, method, bankAccount);
        SetupPayable(m, payable);
        var bankDate = new DateOnly(2026, 8, 27); // distinta de PaymentDate (2026-08-28)

        var result = await BuildHandler(m).Handle(
            SingleLineCommand(
                new SupplierPaymentMethodLineRequest(
                    method.Id,
                    bankAccount.Id,
                    null,
                    250m,
                    ReferenceNumber: " 000987654 ",
                    Notes: "Transferencia interbancaria",
                    TransactionDate: bankDate
                ),
                payable,
                250m
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.MethodLines.Single();
        line.PaymentMethodId.Should().Be(method.Id);
        line.CompanyBankAccountId.Should().Be(bankAccount.Id);
        line.Amount.Should().Be(250m);
        line.TransactionDate.Should().Be(bankDate, "la fecha real de la fuente no se sustituye por PaymentDate");
        line.ReferenceNumber.Should().Be("000987654");
        line.Notes.Should().Be("Transferencia interbancaria");
        line.CashRegisterId.Should().BeNull();
        line.CashSessionId.Should().BeNull();
        line.CashMovementId.Should().BeNull();
        m.CashSessions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Transferencia_sin_fecha_bancaria_se_rechaza_nunca_se_completa_con_PaymentDate()
    {
        var m = BuildMocks();
        var method = TransferMethod();
        var bankAccount = ActiveBankAccount(CompanyId);
        var payable = CreatePayableWithInstallment(80m);
        SetupTransfer(m, method, bankAccount);
        SetupPayable(m, payable);

        var result = await BuildHandler(m).Handle(
            SingleLineCommand(
                new SupplierPaymentMethodLineRequest(method.Id, bankAccount.Id, null, 80m, "OP-1", TransactionDate: null),
                payable,
                80m
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be("La fecha de la transacción bancaria es obligatoria.");
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.SupplierPayments.Verify(r => r.AddAsync(It.IsAny<SupplierPayment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Validator_rechaza_linea_bancaria_sin_fecha_de_transaccion()
    {
        var result = new SupplierPaymentMethodLineRequestValidator().Validate(
            new SupplierPaymentMethodLineRequest(Guid.NewGuid(), Guid.NewGuid(), null, 10m, "OP-1", TransactionDate: null)
        );

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Transferencia_sin_numero_de_operacion_se_rechaza_si_el_medio_exige_referencia()
    {
        var m = BuildMocks();
        var method = TransferMethod();
        var bankAccount = ActiveBankAccount(CompanyId);
        var payable = CreatePayableWithInstallment(80m);
        SetupTransfer(m, method, bankAccount);
        SetupPayable(m, payable);

        var result = await BuildHandler(m).Handle(
            SingleLineCommand(new SupplierPaymentMethodLineRequest(method.Id, bankAccount.Id, null, 80m, TransactionDate: new DateOnly(2026, 8, 28)), payable, 80m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("número de operación");
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Efectivo_con_cuenta_bancaria_se_rechaza()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var bankAccount = ActiveBankAccount(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        SetupTransfer(m, method, bankAccount);
        SetupPayable(m, payable);

        var result = await BuildHandler(m).Handle(
            SingleLineCommand(new SupplierPaymentMethodLineRequest(method.Id, bankAccount.Id, null, 100m, TransactionDate: new DateOnly(2026, 8, 28)), payable, 100m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Contain("debe ser una caja");
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Transferencia_con_caja_se_rechaza()
    {
        var m = BuildMocks();
        var method = TransferMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);

        var result = await BuildHandler(m).Handle(
            SingleLineCommand(new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m, "OP-9"), payable, 100m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Contain("debe ser una cuenta bancaria");
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Medio_de_credito_se_rechaza_para_pago_a_proveedor()
    {
        var m = BuildMocks();
        var credit = PaymentMethod.Create(TenantId, "CREDITO", "Crédito", false, true, 5, UserId);
        var bankAccount = ActiveBankAccount(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        SetupTransfer(m, credit, bankAccount);
        SetupPayable(m, payable);

        var result = await BuildHandler(m).Handle(
            SingleLineCommand(new SupplierPaymentMethodLineRequest(credit.Id, bankAccount.Id, null, 100m, TransactionDate: new DateOnly(2026, 8, 28)), payable, 100m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("crédito");
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Validator_rechaza_fecha_de_transaccion_en_una_fuente_de_caja()
    {
        var validator = new SupplierPaymentMethodLineRequestValidator();

        var result = validator.Validate(
            new SupplierPaymentMethodLineRequest(
                Guid.NewGuid(),
                null,
                Guid.NewGuid(),
                10m,
                TransactionDate: new DateOnly(2026, 8, 28)
            )
        );

        result.IsValid.Should().BeFalse();
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-CASH-HARDENING-02A-CLOSE — sin sobregiro de caja
    // ══════════════════════════════════════════════════════════════════════

    private void AssertNothingPersisted(Mocks m, CashSession session, AccountsPayable payable)
    {
        m.SupplierPayments.Verify(r => r.AddAsync(It.IsAny<SupplierPayment>(), It.IsAny<CancellationToken>()), Times.Never);
        m.SupplierPayments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.Sequences.Verify(s => s.CaptureNextAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        session.Movements.Should().ContainSingle("solo la apertura: ningún egreso de caja registrado");
        payable.Installments[0].PaidAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Efectivo_igual_al_disponible_de_caja_se_permite_y_deja_la_caja_en_cero()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(80m);
        var session = SetupMethodAndDestination(m, method, destination, openingAmount: 80m);
        SetupPayable(m, payable);

        var result = await BuildHandler(m).Handle(
            SingleLineCommand(new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 80m), payable, 80m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        session.CurrentBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Efectivo_mayor_al_disponible_de_caja_se_rechaza_con_mensaje_claro_y_no_persiste_nada()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(200m);
        var session = SetupMethodAndDestination(m, method, destination, openingAmount: 80m);
        SetupPayable(m, payable);

        var result = await BuildHandler(m).Handle(
            SingleLineCommand(new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 120m), payable, 120m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be("La caja seleccionada dispone de $80.00 y se intenta registrar un pago de $120.00.");
        session.CurrentBalance.Should().Be(80m, "el esperado nunca queda negativo");
        AssertNothingPersisted(m, session, payable);
    }

    [Fact]
    public async Task Varias_lineas_de_efectivo_contra_la_misma_caja_se_validan_acumuladas()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(120m);
        var session = SetupMethodAndDestination(m, method, destination, openingAmount: 100m);
        SetupPayable(m, payable);

        // Cada línea (60) cabe sola en 100; juntas (120) no.
        var result = await BuildHandler(m).Handle(
            new RegisterSupplierPaymentCommand(
                SupplierId,
                new DateOnly(2026, 8, 28),
                120m,
                null,
                new[]
                {
                    new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 60m),
                    new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 60m),
                },
                new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 120m) },
                new[]
                {
                    new SupplierPaymentAllocationLineRequest(0, 0, 60m),
                    new SupplierPaymentAllocationLineRequest(1, 0, 60m),
                }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La caja seleccionada dispone de $100.00 y se intenta registrar un pago de $120.00.");
        AssertNothingPersisted(m, session, payable);
    }

    [Fact]
    public async Task Pago_mixto_usa_todo_el_disponible_de_caja_y_completa_con_banco()
    {
        var m = BuildMocks();
        var cash = ActivePaymentMethod();
        var transfer = TransferMethod();
        var destination = ActiveDestination(CompanyId);
        var bankAccount = ActiveBankAccount(CompanyId);
        var payable = CreatePayableWithInstallment(200m);
        var session = SetupMethodAndDestination(m, cash, destination, openingAmount: 80m);
        SetupTransfer(m, transfer, bankAccount);
        SetupPayable(m, payable);

        var result = await BuildHandler(m).Handle(
            new RegisterSupplierPaymentCommand(
                SupplierId,
                new DateOnly(2026, 8, 28),
                200m,
                null,
                new[]
                {
                    new SupplierPaymentMethodLineRequest(cash.Id, null, destination.Id, 80m),
                    new SupplierPaymentMethodLineRequest(transfer.Id, bankAccount.Id, null, 120m, "OP-777", TransactionDate: new DateOnly(2026, 8, 28)),
                },
                new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, 200m) },
                new[]
                {
                    new SupplierPaymentAllocationLineRequest(0, 0, 80m),
                    new SupplierPaymentAllocationLineRequest(1, 0, 120m),
                }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        session.CurrentBalance.Should().Be(0m);
        payable.Installments[0].Status.Should().Be(AccountsPayableStatus.Paid);
        session.Movements.Should().ContainSingle(x => x.MovementType == CashMovementType.SupplierPayment && x.Amount == 80m);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-CASH-OWNERSHIP-02B — autoridad sobre la CashSession
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Efectivo_desde_caja_operada_por_otro_usuario_se_rechaza_sin_persistir_nada()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        SetupMethodAndDestination(m, method, destination);
        var foreignSession = CashSession.Open(
            TenantId, CompanyId, BranchId, Guid.NewGuid(), destination.Id,
            "CAJA-01", "Caja Principal", Guid.NewGuid(), "001", 500m, Guid.NewGuid()
        );
        m.CashSessions
            .Setup(r => r.GetOpenByCashRegisterForUpdateAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignSession);
        SetupPayable(m, payable);

        var result = await BuildHandler(m).Handle(
            SingleLineCommand(new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m), payable, 100m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be("La caja seleccionada está siendo operada por otro usuario.");
        foreignSession.Movements.Should().ContainSingle("la caja ajena no se toca");
        payable.Installments[0].PaidAmount.Should().Be(0m);
        m.SupplierPayments.Verify(r => r.AddAsync(It.IsAny<SupplierPayment>(), It.IsAny<CancellationToken>()), Times.Never);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Efectivo_desde_la_propia_caja_de_otra_sucursal_se_rechaza()
    {
        var m = BuildMocks();
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(100m);
        SetupMethodAndDestination(m, method, destination);
        var otherBranchSession = CashSession.Open(
            TenantId, CompanyId, Guid.NewGuid(), UserId, destination.Id,
            "CAJA-01", "Caja Principal", Guid.NewGuid(), "001", 500m, UserId
        );
        m.CashSessions
            .Setup(r => r.GetOpenByCashRegisterForUpdateAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherBranchSession);
        SetupPayable(m, payable);

        var result = await BuildHandler(m).Handle(
            SingleLineCommand(new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, 100m), payable, 100m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La caja seleccionada no pertenece a la sucursal activa.");
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Pago en efectivo de <paramref name="total"/> aplicando <paramref name="applied"/> (0 = sin CxP).</summary>
    private static RegisterSupplierPaymentCommand AdvanceCommand(
        PaymentMethod method,
        CashRegister destination,
        AccountsPayable? payable,
        decimal total,
        decimal applied,
        bool confirm
    ) =>
        new(
            SupplierId,
            new DateOnly(2026, 8, 28),
            total,
            null,
            new[] { new SupplierPaymentMethodLineRequest(method.Id, null, destination.Id, total) },
            payable is null || applied == 0
                ? []
                : new[] { new SupplierPaymentApplicationLineRequest(payable.Installments[0].Id, applied) },
            payable is null || applied == 0 ? [] : new[] { new SupplierPaymentAllocationLineRequest(0, 0, applied) },
            confirm
        );

    private (Mocks m, PaymentMethod method, CashRegister destination, AccountsPayable payable) ArrangeAdvance(
        decimal outstanding,
        bool allowWithoutPayable
    )
    {
        var m = BuildMocks();
        SetAllowWithoutPayable(m.Preferences, allowWithoutPayable);
        var method = ActivePaymentMethod();
        var destination = ActiveDestination(CompanyId);
        var payable = CreatePayableWithInstallment(outstanding);
        SetupMethodAndDestination(m, method, destination);
        SetupPayable(m, payable);
        return (m, method, destination, payable);
    }

    [Fact]
    public async Task Setting_OFF_pago_exacto_contra_CxP_confirma_sin_SupplierCredit_ni_consultar_la_politica()
    {
        var (m, method, destination, payable) = ArrangeAdvance(180m, allowWithoutPayable: false);

        var result = await BuildHandler(m).Handle(AdvanceCommand(method, destination, payable, 180m, 180m, confirm: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.UnappliedAmount.Should().Be(0m);
        result.Value.SupplierCreditId.Should().BeNull();
        m.SupplierCredits.Verify(r => r.AddAsync(It.IsAny<SupplierCredit>(), It.IsAny<CancellationToken>()), Times.Never);
        m.Preferences.Verify(p => p.ResolveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Setting_OFF_pago_parcial_confirma_y_deja_la_cuota_parcialmente_pagada()
    {
        var (m, method, destination, payable) = ArrangeAdvance(180m, allowWithoutPayable: false);

        var result = await BuildHandler(m).Handle(AdvanceCommand(method, destination, payable, 100m, 100m, confirm: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        payable.Installments[0].OutstandingAmount.Should().Be(80m);
        m.SupplierCredits.Verify(r => r.AddAsync(It.IsAny<SupplierCredit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Excedente_confirmado_crea_SupplierCredit_por_exactamente_el_remanente_con_o_sin_setting(bool setting)
    {
        var (m, method, destination, payable) = ArrangeAdvance(180m, allowWithoutPayable: setting);
        SupplierCredit? added = null;
        m.SupplierCredits
            .Setup(r => r.AddAsync(It.IsAny<SupplierCredit>(), It.IsAny<CancellationToken>()))
            .Callback<SupplierCredit, CancellationToken>((c, _) => added = c)
            .Returns(Task.CompletedTask);

        var result = await BuildHandler(m).Handle(AdvanceCommand(method, destination, payable, 200m, 180m, confirm: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.AppliedAmount.Should().Be(180m);
        result.Value.UnappliedAmount.Should().Be(20m);
        added.Should().NotBeNull();
        added!.OriginalAmount.Should().Be(20m);
        added.AvailableAmount.Should().Be(20m);
        added.SourceSupplierPaymentId.Should().Be(result.Value.Id);
        added.SourcePurchaseReturnId.Should().BeNull();
        added.BranchId.Should().Be(BranchId);
        added.SupplierId.Should().Be(SupplierId);
        added.CompanyId.Should().Be(CompanyId);
        added.DomainEvents.Should().BeEmpty("SupplierCredit no contabiliza al crearse desde un pago");
        result.Value.SupplierCreditId.Should().Be(added.Id);
        payable.Installments[0].OutstandingAmount.Should().Be(0m);
        // El setting solo gobierna el pago SIN CxP: el anticipo por sobrepago no lo consulta.
        m.Preferences.Verify(p => p.ResolveAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Setting_OFF_cero_aplicaciones_se_rechaza_sin_ningun_efecto()
    {
        var (m, method, destination, _) = ArrangeAdvance(180m, allowWithoutPayable: false);

        var result = await BuildHandler(m).Handle(AdvanceCommand(method, destination, null, 200m, 0m, confirm: true), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("sin una cuenta por pagar");
        m.Uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.SupplierPayments.Verify(r => r.AddAsync(It.IsAny<SupplierPayment>(), It.IsAny<CancellationToken>()), Times.Never);
        m.SupplierCredits.Verify(r => r.AddAsync(It.IsAny<SupplierCredit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Setting_ON_cero_aplicaciones_confirmado_crea_pago_y_SupplierCredit_por_el_total()
    {
        var (m, method, destination, _) = ArrangeAdvance(180m, allowWithoutPayable: true);
        SupplierCredit? added = null;
        m.SupplierCredits
            .Setup(r => r.AddAsync(It.IsAny<SupplierCredit>(), It.IsAny<CancellationToken>()))
            .Callback<SupplierCredit, CancellationToken>((c, _) => added = c)
            .Returns(Task.CompletedTask);

        var result = await BuildHandler(m).Handle(AdvanceCommand(method, destination, null, 200m, 0m, confirm: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.AppliedAmount.Should().Be(0m);
        result.Value.UnappliedAmount.Should().Be(200m);
        added!.OriginalAmount.Should().Be(200m);
        m.AccountsPayables.Verify(
            a => a.GetByInstallmentIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Setting_ON_cero_aplicaciones_sin_confirmar_se_rechaza_sin_efectos()
    {
        var (m, method, destination, _) = ArrangeAdvance(180m, allowWithoutPayable: true);

        var result = await BuildHandler(m).Handle(AdvanceCommand(method, destination, null, 200m, 0m, confirm: false), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        m.Uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.SupplierCredits.Verify(r => r.AddAsync(It.IsAny<SupplierCredit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Validator_acepta_cero_aplicaciones_pero_rechaza_aplicaciones_mayores_al_total()
    {
        var validator = new RegisterSupplierPaymentCommandValidator();
        var methods = new[] { new SupplierPaymentMethodLineRequest(Guid.NewGuid(), null, Guid.NewGuid(), 100m) };

        validator
            .Validate(new RegisterSupplierPaymentCommand(SupplierId, new DateOnly(2026, 8, 28), 100m, null, methods, [], [], true))
            .IsValid.Should().BeTrue();
        validator
            .Validate(
                new RegisterSupplierPaymentCommand(
                    SupplierId,
                    new DateOnly(2026, 8, 28),
                    100m,
                    null,
                    methods,
                    new[] { new SupplierPaymentApplicationLineRequest(Guid.NewGuid(), 120m) },
                    new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
                )
            )
            .IsValid.Should().BeFalse();
    }
}
