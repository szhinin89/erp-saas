using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// Fase 4 (ADR — Rediseño del módulo de Caja) — CreateSalesDraftHandler ya no recibe
/// EmissionPointId del cliente: lo resuelve exclusivamente desde ICurrentCashSession, junto con
/// CashSessionId, y rechaza la operación si el usuario no tiene una caja abierta.
/// </summary>
public sealed class CreateSalesDraftHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ISalesInvoiceRepository> Repo { get; } = new();
        public Mock<IBusinessPartnerRepository> BpRepo { get; } = new();
        public Mock<IBusinessPartnerRoleRepository> RoleRepo { get; } = new();
        public Mock<IBusinessPartnerContactRepository> BpContactRepo { get; } = new();
        public Mock<IBusinessPartnerLocationRepository> BpLocationRepo { get; } = new();
        public Mock<IPaymentTermDefaultResolver> PtResolver { get; } = new();
        public Mock<IPaymentMethodRepository> PmRepo { get; } = new();
        public Mock<IItemRepository> ItemRepo { get; } = new();
        public Mock<IEmissionPointRepository> EpRepo { get; } = new();
        public Mock<ISriTaxResolver> Tax { get; } = new();
        public Mock<IPricingResolver> Pricing { get; } = new();
        public Mock<ICompanySpecialTaxResponsibilityRepository> CompanyTaxRepo { get; } = new();
        public Mock<ERP.Domain.Modules.Inventory.Interfaces.IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<ERP.Application.Common.Interfaces.IAverageCostService> CostService { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public Mock<ICurrentCashSession> CashSession { get; } = new();
        public Mock<IOperationalPreferencesResolver> Preferences { get; } = new();
        public Mock<ERP.Application.Modules.Sales.Services.ISalesCreditRequirementPolicy> CreditPolicy { get; } = new();
        public Mock<ERP.Domain.Modules.Finance.Interfaces.ICompanyBankAccountRepository> BankAccountRepo { get; } = new();

        public Fixture()
        {
            CreditPolicy
                .Setup(p => p.GetCashFallbackAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    Result<PaymentTerm>.Success(PaymentTerm.Create(TenantId, "CONTADO", "Contado", 1, 0, UserId))
                );
            // Réplica mínima del comportamiento real (b: dueDate manual, c: schedule manual, e:
            // sin default de empresa configurado por defecto) — suficiente para que los tests que
            // no mockean explícitamente esta política ejerzan la misma rama que produciría la
            // implementación real de SalesCreditRequirementPolicy.
            CreditPolicy
                .Setup(p =>
                    p.ResolveCompanyOrManualAsync(
                        It.IsAny<DateOnly?>(),
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    (DateOnly? dueDate, bool hasManualSchedule, CancellationToken _) =>
                        dueDate.HasValue || hasManualSchedule
                            ? Result<PaymentTerm?>.Success(null)
                            : Result<PaymentTerm?>.ValidationFailure(
                                "Debe definir una fecha de vencimiento, cuotas o una condición de pago para el saldo pendiente."
                            )
                );
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Preferences
                .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new OperationalPreferences(
                        SalesPos: new SalesPosPreferences(true, false, true, 0m, null, false, false, null, null),
                        Cash: new CashPreferences(true, true, 0m, true, true, true),
                        Purchases: new PurchasesPreferences(null, true, true, true, false),
                        Inventory: new InventoryPreferences(false, true, false, 0m),
                        Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
                        ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
                        Notifications: new NotificationsPreferences(true, false, "es")
                    )
                );
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            Branch.Setup(b => b.BranchId).Returns(BranchId);
            User.Setup(u => u.UserId).Returns(UserId);

            var bp = BusinessPartner.Create(
                TenantId,
                "05",
                "1710034065",
                1,
                "Cliente Test",
                UserId
            );
            BpRepo
                .Setup(r => r.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(bp);
            // El mock siempre devuelve `bp`, independientemente del Id pedido — suficiente para
            // este test, que solo crea un draft con un único cliente.
            BpRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(bp);

            BpContactRepo
                .Setup(r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<BusinessPartnerContact>());
            BpLocationRepo
                .Setup(r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<BusinessPartnerLocation>());

            var role = BusinessPartnerRole.Create(TenantId, bp.Id, RoleType.Customer, UserId);
            RoleRepo
                .Setup(r =>
                    r.GetByTypeAsync(
                        It.IsAny<Guid>(),
                        RoleType.Customer,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(role);

            var pt = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId);
            PtResolver
                .Setup(r => r.ResolveForSaleAsync(CustomerId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<PaymentTerm>.Success(pt));

            Tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));
            // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.4) — por defecto la empresa no es responsable
            // de ningún impuesto especial en ventas (comportamiento por defecto: nunca ICE/IRBPNR
            // falso). Los tests de 5B lo sobreescriben explícitamente cuando lo necesitan.
            CompanyTaxRepo
                .Setup(r =>
                    r.GetResponsibleSriTaxCategoryCodesAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Array.Empty<string>());
        }

        public CreateSalesDraftHandler BuildHandler() =>
            new(
                Repo.Object,
                BpRepo.Object,
                RoleRepo.Object,
                BpContactRepo.Object,
                BpLocationRepo.Object,
                PtResolver.Object,
                PmRepo.Object,
                ItemRepo.Object,
                EpRepo.Object,
                Tax.Object,
                Pricing.Object,
                CompanyTaxRepo.Object,
                WarehouseRepo.Object,
                CostService.Object,
                Tenant.Object,
                Company.Object,
                Branch.Object,
                User.Object,
                CashSession.Object,
                Preferences.Object,
                CreditPolicy.Object,
                BankAccountRepo.Object
            );

        public static CreateSalesDraftCommand ValidCommand() =>
            new(
                CustomerId,
                DateOnly.FromDateTime(DateTime.UtcNow),
                new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") }
            );
    }

    [Fact]
    public async Task Con_caja_abierta_crea_la_factura_y_asigna_CashSessionId_y_EmissionPointId_de_la_sesion()
    {
        var f = new Fixture();
        var cashSessionId = Guid.NewGuid();
        var emissionPointId = Guid.NewGuid();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(cashSessionId);
        f.CashSession.Setup(c => c.EmissionPointId).Returns(emissionPointId);
        f.EpRepo.Setup(r =>
                r.GetByIdAsync(emissionPointId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((ERP.Domain.Modules.Company.Entities.EmissionPoint?)null);

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var handler = f.BuildHandler();
        var result = await handler.Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured.Should().NotBeNull();
        captured!.CashSessionId.Should().Be(cashSessionId);
        captured.EmissionPointId.Should().Be(emissionPointId);
        captured.BranchId.Should().Be(BranchId);
    }

    [Fact]
    public async Task Fase4_genera_cronograma_automatico_al_crear_el_borrador()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var result = await f.BuildHandler().Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured!.PaymentSchedules.Should().ContainSingle();
        captured.PaymentSchedules[0].Amount.Should().Be(captured.GrandTotal);
        captured.IsPaymentScheduleManual.Should().BeFalse();
    }

    [Fact]
    public async Task Fase4_Schedule_explicito_persiste_exacto_y_marca_manual()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var command = new CreateSalesDraftCommand(
            CustomerId,
            issueDate,
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") },
            Schedule: new List<SalesScheduleInput>
            {
                new(1, issueDate.AddDays(15), 60m),
                new(2, issueDate.AddDays(30), 55m),
            }
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured!.PaymentSchedules.Should().HaveCount(2);
        captured.PaymentSchedules.Sum(s => s.Amount).Should().Be(captured.GrandTotal);
        captured.IsPaymentScheduleManual.Should().BeTrue();
    }

    [Fact]
    public async Task ADR033_rechaza_PaymentTermId_explicito_inactivo()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        f.PtResolver
            .Setup(r => r.ResolveForSaleAsync(CustomerId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.ValidationFailure("La condición de pago se encuentra inactiva."));

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") },
            PaymentTermId: Guid.NewGuid()
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactiva");
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── SALES-TRANSFER-BANK-ACCOUNT-01 ──────────────────────────────────────────────────────

    private static readonly Guid TransferMethodId = Guid.NewGuid();
    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly Guid AccountingAccountId = Guid.NewGuid();

    private static Domain.Modules.Sales.Entities.PaymentMethod TransferPaymentMethod() =>
        Domain.Modules.Sales.Entities.PaymentMethod.Create(
            TenantId,
            "TRANSFERENCIA",
            "Transferencia Bancaria",
            requiresReference: true,
            isCreditAllowed: false,
            sortOrder: 2,
            createdBy: UserId,
            detailType: Domain.Modules.Sales.Enums.PaymentMethodDetailType.Transfer
        );

    private static ERP.Domain.Modules.Finance.Entities.CompanyBankAccount ActiveBankAccount(
        bool isActive = true
    )
    {
        var bankAccount = ERP.Domain.Modules.Finance.Entities.CompanyBankAccount.Create(
            TenantId,
            CompanyId,
            BankId,
            ERP.Domain.Modules.Finance.Enums.BankAccountType.Checking,
            "2200123456",
            "Cuenta corriente Pichincha",
            AccountingAccountId,
            UserId
        );
        if (!isActive)
            bankAccount.Disable(UserId);
        return bankAccount;
    }

    private static CreateSalesDraftCommand CommandWithTransferPayment(Guid? companyBankAccountId) =>
        new(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") },
            Payments: new List<SalesPaymentInput>
            {
                new(
                    TransferMethodId,
                    115m,
                    Reference: "TRX-001",
                    TransferDetail: new TransferDetailInput(
                        companyBankAccountId,
                        "TRX-001",
                        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
                    )
                ),
            }
        );

    [Fact]
    public async Task Transferencia_sin_CompanyBankAccountId_rechaza_el_borrador()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());
        f.PmRepo
            .Setup(r => r.GetByIdAsync(TenantId, TransferMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferPaymentMethod());

        var result = await f.BuildHandler()
            .Handle(CommandWithTransferPayment(companyBankAccountId: null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cuenta bancaria destino");
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Transferencia_con_cuenta_bancaria_de_otra_empresa_rechaza_el_borrador()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());
        f.PmRepo
            .Setup(r => r.GetByIdAsync(TenantId, TransferMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferPaymentMethod());
        var bankAccountId = Guid.NewGuid();
        f.BankAccountRepo
            .Setup(r => r.GetByIdAsync(TenantId, bankAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ERP.Domain.Modules.Finance.Entities.CompanyBankAccount?)null);

        var result = await f.BuildHandler()
            .Handle(CommandWithTransferPayment(bankAccountId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no existe o no pertenece a esta empresa");
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Transferencia_con_cuenta_bancaria_inactiva_rechaza_el_borrador()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());
        f.PmRepo
            .Setup(r => r.GetByIdAsync(TenantId, TransferMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferPaymentMethod());
        var bankAccount = ActiveBankAccount(isActive: false);
        f.BankAccountRepo
            .Setup(r => r.GetByIdAsync(TenantId, bankAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bankAccount);

        var result = await f.BuildHandler()
            .Handle(CommandWithTransferPayment(bankAccount.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactiva");
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Transferencia_valida_persiste_CompanyBankAccountId_en_el_detalle()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());
        f.PmRepo
            .Setup(r => r.GetByIdAsync(TenantId, TransferMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferPaymentMethod());
        var bankAccount = ActiveBankAccount();
        f.BankAccountRepo
            .Setup(r => r.GetByIdAsync(TenantId, bankAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bankAccount);

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var result = await f.BuildHandler()
            .Handle(CommandWithTransferPayment(bankAccount.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured!.Payments.Should().ContainSingle();
        captured.Payments[0].TransferDetail.Should().NotBeNull();
        captured.Payments[0].TransferDetail!.CompanyBankAccountId.Should().Be(bankAccount.Id);
        captured.Payments[0].TransferDetail!.ReceiptNumber.Should().Be("TRX-001");
    }

    [Fact]
    public async Task TransferDetail_adjunto_a_un_metodo_que_no_es_Transferencia_rechaza_el_borrador()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());
        var cashMethodId = Guid.NewGuid();
        f.PmRepo
            .Setup(r => r.GetByIdAsync(TenantId, cashMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Domain.Modules.Sales.Entities.PaymentMethod.Create(
                    TenantId,
                    "EFECTIVO",
                    "Efectivo",
                    requiresReference: false,
                    isCreditAllowed: false,
                    sortOrder: 1,
                    createdBy: UserId
                )
            );

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") },
            Payments: new List<SalesPaymentInput>
            {
                new(
                    cashMethodId,
                    115m,
                    TransferDetail: new TransferDetailInput(Guid.NewGuid(), "TRX-001", "2026-07-13")
                ),
            }
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no admite datos de transferencia");
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SALES_SETTLEMENT_CREDIT_01_sin_default_ni_dueDate_ni_schedule_con_saldo_pendiente_exige_regla_de_credito()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        // Sin PaymentTermId explícito, sin CompanyBpSalesSettings válido para el cliente, sin
        // pagos (saldo pendiente = total), sin dueDate/schedule manual y sin default de empresa —
        // nunca cae al catálogo ("primer registro") ni a un fallback silencioso.
        f.PtResolver
            .Setup(r => r.ResolveForSaleAsync(CustomerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.ValidationFailure(
                "Debe seleccionar una condición de pago; no hay una configurada para esta empresa."
            ));

        var handler = f.BuildHandler();
        var result = await handler.Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(
            "Debe definir una fecha de vencimiento, cuotas o una condición de pago para el saldo pendiente."
        );
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ADR033_usa_default_de_CompanyBpSalesSettings_del_cliente()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        var defaultPt = PaymentTerm.Create(TenantId, "30D", "Crédito 30 días", 1, 30, UserId);
        f.PtResolver
            .Setup(r => r.ResolveForSaleAsync(CustomerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.Success(defaultPt));

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var handler = f.BuildHandler();
        var result = await handler.Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured!.PaymentTerm.Id.Should().Be(defaultPt.Id);
    }

    [Fact]
    public async Task POS_DISCOUNT_RULES_01_rechaza_descuento_manual_por_linea_si_no_esta_permitido()
    {
        var f = new Fixture();
        f.Preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalPreferences(
                    SalesPos: new SalesPosPreferences(true, false, false, 0m, null, false, false, null, null),
                    Cash: new CashPreferences(true, true, 0m, true, true, true),
                    Purchases: new PurchasesPreferences(null, true, true, true, false),
                    Inventory: new InventoryPreferences(false, true, false, 0m),
                    Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
                    ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
                    Notifications: new NotificationsPreferences(true, false, "es")
                )
            );
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10", DiscountPct: 10m) }
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no permite aplicar descuentos manuales");
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task POS_DISCOUNT_RULES_01_rechaza_descuento_manual_por_linea_por_encima_del_tope()
    {
        var f = new Fixture();
        f.Preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalPreferences(
                    SalesPos: new SalesPosPreferences(true, false, true, 5m, null, false, false, null, null),
                    Cash: new CashPreferences(true, true, 0m, true, true, true),
                    Purchases: new PurchasesPreferences(null, true, true, true, false),
                    Inventory: new InventoryPreferences(false, true, false, 0m),
                    Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
                    ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
                    Notifications: new NotificationsPreferences(true, false, "es")
                )
            );
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10", DiscountPct: 10m) }
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("descuento máximo permitido es 5");
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task POS_DISCOUNT_RULES_01_acepta_descuento_manual_por_linea_dentro_del_tope()
    {
        var f = new Fixture();
        f.Preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalPreferences(
                    SalesPos: new SalesPosPreferences(true, false, true, 20m, null, false, false, null, null),
                    Cash: new CashPreferences(true, true, 0m, true, true, true),
                    Purchases: new PurchasesPreferences(null, true, true, true, false),
                    Inventory: new InventoryPreferences(false, true, false, 0m),
                    Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
                    ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
                    Notifications: new NotificationsPreferences(true, false, "es")
                )
            );
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        var emissionPointId = Guid.NewGuid();
        f.CashSession.Setup(c => c.EmissionPointId).Returns(emissionPointId);
        f.EpRepo.Setup(r =>
                r.GetByIdAsync(emissionPointId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((ERP.Domain.Modules.Company.Entities.EmissionPoint?)null);
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10", DiscountPct: 10m) }
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task Sin_caja_abierta_rechaza_con_el_mensaje_esperado()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(false);

        var handler = f.BuildHandler();
        var result = await handler.Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No existe una caja abierta para realizar ventas.");
        f.Repo.Verify(
            r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.BpRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "debe rechazar antes de tocar cualquier otro repositorio si no hay caja abierta"
        );
    }

    // ── SALES-PRESENTATIONS-02 ──────────────────────────────────────────

    private static Item CreateItemWithPackaging()
    {
        var item = Item.Create(
            TenantId,
            "SKU-CAJA",
            "Item con presentación",
            "Item con presentación",
            Guid.NewGuid(),
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(tracksStock: false),
            UserId
        );
        item.ReplacePackagingLevels(
            [
                ("UNIDAD X1", 1, 1m, "UNIT", null, null, true, false, true),
                ("CAJA X12", 2, 12m, "CAJA", null, null, false, false, false),
            ],
            UserId
        );
        return item;
    }

    [Fact]
    public async Task Con_PackagingLevelId_resuelve_ConversionFactor_y_QuantityInBaseUom()
    {
        var f = new Fixture();
        var item = CreateItemWithPackaging();
        var caja = item.PackagingLevels.Single(p => p.Name == "CAJA X12");

        f.ItemRepo
            .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.Pricing
            .Setup(p => p.ResolveAsync(item.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PricingResult>.Failure("Sin precio configurado."));
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput>
            {
                new(item.Id, "Caja x12", 2m, 120m, "10", PackagingLevelId: caja.Id),
            }
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured.Should().NotBeNull();
        var line = captured!.Lines.Single();
        line.PackagingLevelId.Should().Be(caja.Id);
        line.UomCode.Should().Be("CAJA");
        line.BaseUomCode.Should().Be("UNIT");
        line.ConversionFactor.Should().Be(12m);
        line.Quantity.Should().Be(2m);
        line.QuantityInBaseUom.Should().Be(24m);
    }

    [Fact]
    public async Task Sin_PackagingLevelId_preserva_el_comportamiento_actual_venta_en_unidad_base()
    {
        var f = new Fixture();
        var item = CreateItemWithPackaging();

        f.ItemRepo
            .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.Pricing
            .Setup(p => p.ResolveAsync(item.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PricingResult>.Failure("Sin precio configurado."));
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(item.Id, "Unidad suelta", 5m, 10m, "10") }
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = captured!.Lines.Single();
        line.PackagingLevelId.Should().BeNull();
        line.UomCode.Should().Be("UNIT");
        line.ConversionFactor.Should().Be(1m);
        line.QuantityInBaseUom.Should().Be(5m);
    }

    // ── SALES-SETTLEMENT-CREDIT-01 ──────────────────────────────────────

    private PaymentMethod CashMethod(Guid id) =>
        PaymentMethod.Create(TenantId, "EFECTIVO", "Efectivo", false, false, 1, UserId);

    private PaymentMethod CreditMethod(Guid id) =>
        PaymentMethod.Create(TenantId, "CREDITO", "Crédito", false, true, 2, UserId);

    [Fact]
    public async Task A_Contado_completo_en_efectivo_no_exige_condicion_de_pago_ni_genera_cronograma()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        // Sin default de cliente ni de empresa — un pago que cubre el total no debe necesitarlos.
        f.PtResolver
            .Setup(r => r.ResolveForSaleAsync(CustomerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.ValidationFailure("no configurada"));

        var cashMethodId = Guid.NewGuid();
        f.PmRepo
            .Setup(r => r.GetByIdAsync(TenantId, cashMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashMethod(cashMethodId));

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") },
            Payments: new List<SalesPaymentInput> { new(cashMethodId, 115m) }
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured!.PaymentSchedules.Should().BeEmpty();
        captured.Payments.Should().ContainSingle();
    }

    [Fact]
    public async Task F_Pago_parcial_con_dueDate_manual_genera_cronograma_por_el_saldo_pendiente()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());
        f.PtResolver
            .Setup(r => r.ResolveForSaleAsync(CustomerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.ValidationFailure("no configurada"));

        var cashMethodId = Guid.NewGuid();
        f.PmRepo
            .Setup(r => r.GetByIdAsync(TenantId, cashMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashMethod(cashMethodId));

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var command = new CreateSalesDraftCommand(
            CustomerId,
            issueDate,
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") }, // total 115
            DueDate: issueDate.AddDays(30),
            Payments: new List<SalesPaymentInput> { new(cashMethodId, 60m) }
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured!.PaymentSchedules.Should().ContainSingle();
        captured.PaymentSchedules[0].Amount.Should().Be(55m); // 115 - 60
        captured.PaymentSchedules[0].DueDate.Should().Be(issueDate.AddDays(30));
    }

    [Fact]
    public async Task G_Pago_parcial_con_schedule_manual_usa_el_saldo_pendiente_no_el_total()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());
        f.PtResolver
            .Setup(r => r.ResolveForSaleAsync(CustomerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.ValidationFailure("no configurada"));

        var cashMethodId = Guid.NewGuid();
        f.PmRepo
            .Setup(r => r.GetByIdAsync(TenantId, cashMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashMethod(cashMethodId));

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var command = new CreateSalesDraftCommand(
            CustomerId,
            issueDate,
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") }, // total 115
            Payments: new List<SalesPaymentInput> { new(cashMethodId, 60m) },
            Schedule: new List<SalesScheduleInput>
            {
                new(1, issueDate.AddDays(15), 30m),
                new(2, issueDate.AddDays(30), 25m),
            }
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured!.PaymentSchedules.Should().HaveCount(2);
        captured.PaymentSchedules.Sum(s => s.Amount).Should().Be(55m); // saldo pendiente, no el total (115)
        captured.IsPaymentScheduleManual.Should().BeTrue();
    }

    [Fact]
    public async Task H_Credito_puro_con_dueDate_genera_cronograma_por_el_total()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());
        f.PtResolver
            .Setup(r => r.ResolveForSaleAsync(CustomerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.ValidationFailure("no configurada"));

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var command = new CreateSalesDraftCommand(
            CustomerId,
            issueDate,
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") }, // total 115
            DueDate: issueDate.AddDays(45)
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured!.PaymentSchedules.Should().ContainSingle();
        captured.PaymentSchedules[0].Amount.Should().Be(captured.GrandTotal);
        captured.Payments.Should().BeEmpty();
    }

    [Fact]
    public async Task L_Default_de_empresa_cubre_saldo_pendiente_cuando_el_cliente_no_tiene_default()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        f.PtResolver
            .Setup(r => r.ResolveForSaleAsync(CustomerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.ValidationFailure("no configurada"));

        var companyPt = PaymentTerm.Create(TenantId, "30D", "Crédito 30 días", 1, 30, UserId);
        f.CreditPolicy
            .Setup(p =>
                p.ResolveCompanyOrManualAsync(null, false, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<PaymentTerm?>.Success(companyPt));

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var result = await f.BuildHandler().Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured!.PaymentTerm.Id.Should().Be(companyPt.Id);
        captured.PaymentSchedules.Should().ContainSingle();
        captured.PaymentSchedules[0].Amount.Should().Be(captured.GrandTotal);
    }

    [Fact]
    public async Task N_Metodo_credito_no_cuenta_como_pago_aplicado_deja_saldo_pendiente()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        // Sin default de cliente/empresa ni dueDate/schedule — si "Crédito" contara como pago
        // real, el saldo quedaría en 0 y esto pasaría sin exigir nada. Debe rechazar.
        f.PtResolver
            .Setup(r => r.ResolveForSaleAsync(CustomerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.ValidationFailure("no configurada"));

        var creditMethodId = Guid.NewGuid();
        f.PmRepo
            .Setup(r => r.GetByIdAsync(TenantId, creditMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreditMethod(creditMethodId));

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") }, // total 115
            Payments: new List<SalesPaymentInput> { new(creditMethodId, 115m) }
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(
            "Debe definir una fecha de vencimiento, cuotas o una condición de pago para el saldo pendiente."
        );
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── SALES-HISTORICAL-PRICING-SNAPSHOT-01 ────────────────────────────

    [Fact]
    public async Task Congela_el_snapshot_comercial_historico_con_bodega_costo_y_pricing_resueltos()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        var item = Item.Create(
            TenantId,
            "SKU-HIST",
            "Item con historial",
            "Item con historial",
            Guid.NewGuid(),
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(tracksStock: true),
            UserId
        );
        f.ItemRepo
            .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var warehouse = ERP.Domain.Modules.Inventory.Entities.Warehouse.Create(
            TenantId,
            BranchId,
            "Bodega Central",
            "BOD-01",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            UserId,
            CompanyId
        );
        f.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, warehouse.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouse);

        var priceListId = Guid.NewGuid();
        f.Pricing
            .Setup(p => p.ResolveAsync(item.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<PricingResult>.Success(
                    new PricingResult(
                        item.Id,
                        priceListId,
                        "MAYORISTA",
                        "Lista Mayorista",
                        "USD",
                        BasePrice: 100m,
                        RuleApplied: "PercentDiscount:5 (lista)",
                        UnitPrice: 95m,
                        RuleDescription: "Descuento 5% (regla general)"
                    )
                )
            );

        f.CostService
            .Setup(s =>
                s.ObtenerCostoPromedioAsync(
                    TenantId,
                    item.Id,
                    warehouse.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(60.5m);

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput>
            {
                new(item.Id, "Item con historial", 3m, 95m, "10", WarehouseId: warehouse.Id),
            }
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = captured!.Lines.Single();

        line.WarehouseName.Should().Be("Bodega Central");
        line.UnitCostAtSale.Should().Be(60.5m);
        line.TotalCostAtSale.Should().Be(60.5m * line.QuantityInBaseUom);
        // BUGFIX-SALES-LIST-PRICE-AT-SALE-BASE-PRICE-01: ListPriceAtSale es el PVP/precio base
        // ANTES de cualquier descuento (BasePrice=100), nunca el precio ya descontado por la
        // regla (UnitPrice=95) — antes de este fix ambos quedaban en 95, ocultando que sí hubo
        // un descuento de regla real.
        line.ListPriceAtSale.Should().Be(100m);
        line.UnitPrice.Should().Be(95m);
        line.ListPriceAtSale.Should().NotBe(line.UnitPrice);
        line.PriceListId.Should().Be(priceListId);
        line.PriceListName.Should().Be("Lista Mayorista");
        line.PricingSource.Should().Be("PercentDiscount:5 (lista)");
        line.DiscountSource.Should().Be("PricingRule");
        line.DiscountDescription.Should().Be("Descuento 5% (regla general)");

        // GET /api/v1/sales/{id} — GetSalesInvoiceByIdHandler debe exponer exactamente lo ya
        // congelado en la línea (SalesMapper.MapDetail), sin recalcular nada.
        var repo = new Mock<ISalesInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, captured.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(captured);
        var edocRepo =
            new Mock<ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository>();
        edocRepo
            .Setup(r =>
                r.GetBySourceAsync(TenantId, "Sales", captured.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((ERP.Domain.Modules.ElectronicDocuments.Entities.ElectronicDocument?)null);

        var getHandler = new GetSalesInvoiceByIdHandler(
            repo.Object,
            edocRepo.Object,
            f.Tenant.Object,
            f.Branch.Object
        );
        var getResult = await getHandler.Handle(
            new GetSalesInvoiceByIdQuery(captured.Id),
            CancellationToken.None
        );

        getResult.IsSuccess.Should().BeTrue(getResult.Error);
        var lineDto = getResult.Value!.Lines.Single();
        lineDto.WarehouseName.Should().Be("Bodega Central");
        lineDto.UnitCostAtSale.Should().Be(60.5m);
        // La query de detalle expone exactamente lo persistido en el Draft — sin recalcular.
        lineDto.ListPriceAtSale.Should().Be(100m);
        lineDto.PriceListName.Should().Be("Lista Mayorista");
        lineDto.PricingSource.Should().Be("PercentDiscount:5 (lista)");
        lineDto.DiscountSource.Should().Be("PricingRule");
        lineDto.DiscountDescription.Should().Be("Descuento 5% (regla general)");
    }

    // ── SALES-INVOICE-FINAL-SEMANTIC-INTEGRITY-01 (Fase 6D) — descuento de regla confiable ──

    [Fact]
    public async Task Regla_del_catalogo_sin_rebaja_real_no_marca_DiscountSource_PricingRule()
    {
        // Caso exacto del bug confirmado en auditoría: RuleApplied != null (la regla existe en el
        // catálogo del Pricing Engine), pero BasePrice == UnitPrice — no hubo ninguna rebaja
        // económica real. Antes de este fix, discount_description quedaba poblado ("Descuento 5%
        // (regla general)") aunque list_price_at_sale == unit_price.
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        var item = Item.Create(
            TenantId,
            "SKU-NO-REBAJA",
            "Item sin rebaja real",
            "Item sin rebaja real",
            Guid.NewGuid(),
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(tracksStock: false),
            UserId
        );
        f.ItemRepo
            .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        f.Pricing
            .Setup(p => p.ResolveAsync(item.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<PricingResult>.Success(
                    new PricingResult(
                        item.Id,
                        Guid.NewGuid(),
                        "MAYORISTA",
                        "Lista Mayorista",
                        "USD",
                        BasePrice: 2.00m,
                        RuleApplied: "PercentDiscount:5 (lista)",
                        UnitPrice: 2.00m,
                        RuleDescription: "Descuento 5% (regla general)"
                    )
                )
            );

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput>
            {
                new(item.Id, "Item sin rebaja real", 1m, 2.00m, "10"),
            }
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = captured!.Lines.Single();
        line.ListPriceAtSale.Should().Be(2.00m);
        line.UnitPrice.Should().Be(2.00m);
        // PricingSource conserva la trazabilidad técnica de la regla (sin cambios) — solo
        // DiscountSource/DiscountDescription (semántica comercial) no deben mentir.
        line.PricingSource.Should().Be("PercentDiscount:5 (lista)");
        line.DiscountSource.Should().BeNull();
        line.DiscountDescription.Should().BeNull();
    }

    [Fact]
    public async Task Descuento_manual_de_linea_marca_DiscountSource_Manual_con_texto_legible()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput>
            {
                new(null, "Producto Test", 1, 100m, "10", DiscountPct: 5m),
            }
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = captured!.Lines.Single();
        line.DiscountSource.Should().Be("Manual");
        line.DiscountDescription.Should().Be("Descuento manual de 5% aplicado en la línea.");
        // Sin ItemId no hay Pricing Engine resuelto — ListPriceAtSale/PricingSource quedan null,
        // nunca inventados.
        line.ListPriceAtSale.Should().BeNull();
        line.PricingSource.Should().BeNull();
        // Línea sin ItemId: el ítem no controla inventario aquí (no aplica bodega/costo) —
        // ningún dato de stock se fabrica.
        line.WarehouseName.Should().BeNull();
        line.UnitCostAtSale.Should().BeNull();
        line.TotalCostAtSale.Should().BeNull();
    }

    // ── SALES-INVOICE-FINAL-SEMANTIC-INTEGRITY-01 (Fase 6E) — CustomerSnapshot completo ──

    [Fact]
    public async Task Cliente_con_contacto_y_ubicacion_primarios_congela_email_y_direccion_en_el_snapshot()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        var bp = BusinessPartner.Create(TenantId, "05", "1710034065", 1, "Cliente Con Datos", UserId);
        f.BpRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bp);

        // El resolver se invoca con cmd.CustomerId (la constante CustomerId de la clase), no con
        // bp.Id — BpRepo.GetByIdAsync está mockeado con It.IsAny<Guid>() y siempre devuelve `bp`,
        // pero BpContactRepo/BpLocationRepo se consultan por el Id real pedido en el comando.
        var contact = BusinessPartnerContact.Create(
            TenantId,
            CustomerId,
            "Juan",
            ContactRole.Billing,
            UserId,
            email: "juan@cliente-test.com",
            isPrimary: true
        );
        f.BpContactRepo
            .Setup(r => r.GetByBusinessPartnerAsync(CustomerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { contact });

        var location = BusinessPartnerLocation.Create(
            TenantId,
            CustomerId,
            "Matriz",
            LocationType.Matrix,
            LocationPurpose.Fiscal,
            "Av. Siempre Viva 123",
            UserId,
            isPrimary: true
        );
        f.BpLocationRepo
            .Setup(r => r.GetByBusinessPartnerAsync(CustomerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { location });

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") }
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured!.Customer.Email.Should().Be("juan@cliente-test.com");
        captured.Customer.Address.Should().Be("Av. Siempre Viva 123");
    }

    [Fact]
    public async Task Cliente_sin_contacto_ni_ubicacion_congela_snapshot_sin_fallar()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        // Fixture por defecto ya deja BpContactRepo/BpLocationRepo devolviendo listas vacías.

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") }
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured!.Customer.Email.Should().BeNull();
        captured.Customer.Address.Should().BeNull();
    }

    [Fact]
    public void El_cliente_nunca_puede_enviar_EmissionPointId_ni_CashSessionId()
    {
        typeof(CreateSalesDraftCommand)
            .GetProperty("EmissionPointId")
            .Should()
            .BeNull(
                "el punto de emisión se resuelve desde ICurrentCashSession, nunca desde el cliente"
            );
        typeof(CreateSalesDraftCommand)
            .GetProperty("CashSessionId")
            .Should()
            .BeNull(
                "la sesión de caja se resuelve desde ICurrentCashSession, nunca desde el cliente"
            );
    }
}
