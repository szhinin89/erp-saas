using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — lectura enriquecida de saldos a favor sobre PostgreSQL 16
/// real con handlers reales (aplicar/reembolsar/reversar): nombre real del proveedor, origen
/// devolución (número + fecha de autorización en zona de la empresa) y pago (SystemNumber +
/// PaymentDate), filtros en BD (proveedor/origen/abierto), aislamiento por empresa, historial con
/// CxP destino, destino/forma/referencia del reembolso, enlace de reversas y paginación estable.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SupplierCreditReadModelIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_supplier_credit_read_model_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly DateOnly _today = new(2026, 9, 17);
    private Guid _userId;
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _otherCompanyId;
    private Guid _branchId;
    private Guid _otherBranchId;
    private Guid _supplierId;
    private Guid _otherSupplierId;
    private Guid _paymentTermId;
    private Guid _transferMethodId;
    private Guid _companyBankAccountId;
    private Guid _warehouseId;
    private Guid _itemId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var user = IdentityUser.Create(
            "tesorero",
            "Ana",
            "Tesorera",
            "ana@example.com",
            "hash",
            Guid.NewGuid()
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        _userId = user.Id;

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _userId
        );
        var otherCompany = Company.CreateManaged(
            tenant.Id,
            "1790098765001",
            "Otra S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.AddRange(company, otherCompany);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;
        _otherCompanyId = otherCompany.Id;

        var branch = NewBranch(_companyId, "001");
        var otherBranch = NewBranch(_otherCompanyId, "002");
        var supplier = BusinessPartner.Create(
            _tenantId,
            "05",
            "1710034065",
            1,
            "Distribuidora Andina",
            _userId
        );
        var otherSupplier = BusinessPartner.Create(
            _tenantId,
            "05",
            "1710034073",
            1,
            "Comercial Costa",
            _userId
        );
        var paymentTerm = PaymentTerm.Create(
            _tenantId,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _userId
        );
        db.Branches.AddRange(branch, otherBranch);
        db.BusinessPartners.AddRange(supplier, otherSupplier);
        db.Add(paymentTerm);
        await db.SaveChangesAsync();
        _branchId = branch.Id;
        _otherBranchId = otherBranch.Id;
        _supplierId = supplier.Id;
        _otherSupplierId = otherSupplier.Id;
        _paymentTermId = paymentTerm.Id;

        var ledger = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create("1.1.02.001"),
            "Bancos",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _userId
        );
        var transfer = PaymentMethod.Create(
            _tenantId,
            "TRANSFER",
            "Transferencia",
            true,
            false,
            1,
            _userId,
            PaymentMethodDetailType.Transfer
        );
        var bank = Bank.Create(_tenantId, "PICHINCHA", "Banco Pichincha", "Pichincha", _userId);
        db.Accounts.Add(ledger);
        db.PaymentMethods.Add(transfer);
        db.Banks.Add(bank);
        await db.SaveChangesAsync();
        var bankAccount = CompanyBankAccount.Create(
            _tenantId,
            _companyId,
            bank.Id,
            BankAccountType.Checking,
            "2200123456",
            "Banco Pichincha CTE",
            ledger.Id,
            _userId
        );
        db.CompanyBankAccounts.Add(bankAccount);

        var warehouse = ERP.Domain.Modules.Inventory.Entities.Warehouse.Create(
            _tenantId,
            _branchId,
            "Bodega Principal",
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
            _userId,
            _companyId,
            isMain: true
        );
        var itemType = ERP.Domain.Modules.Items.Entities.ItemTypeDefinition.Create(
            _tenantId,
            "MERCH",
            "Mercadería",
            1,
            _userId
        );
        db.Add(warehouse);
        db.Add(itemType);
        await db.SaveChangesAsync();
        var item = ERP.Domain.Modules.Items.Entities.Item.Create(
            _tenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto",
            description: "Producto",
            itemTypeId: itemType.Id,
            defaultUomCode: "UNIT",
            taxConfig: ERP.Domain.Modules.Items.ValueObjects.ItemTaxConfig.Create(
                saleVatCode: "10",
                purchaseVatCode: "10"
            ),
            saleConfig: ERP.Domain.Modules.Items.ValueObjects.ItemSaleConfig.Create(
                isForSale: true
            ),
            stockConfig: ERP.Domain.Modules.Items.ValueObjects.ItemStockConfig.Create(
                stockControlEnabled: true
            ),
            createdBy: _userId,
            companyId: _companyId
        );
        db.Add(item);
        await db.SaveChangesAsync();

        _transferMethodId = transfer.Id;
        _companyBankAccountId = bankAccount.Id;
        _warehouseId = warehouse.Id;
        _itemId = item.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private Branch NewBranch(Guid companyId, string code) =>
        Branch.Create(
            _tenantId,
            $"Sucursal {code}",
            "Av. Principal 123",
            code,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            _userId,
            companyId: companyId
        );

    private ErpDbContext CreateContext(Guid? companyId = null) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(() => _tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => companyId ?? _companyId)
        );

    // ── Seeds ──────────────────────────────────────────────────────────────

    /// <summary>Anticipo real: pago sin CxP (100% remanente) → crédito origen SupplierPayment.</summary>
    private async Task<(Guid CreditId, SupplierPayment Payment)> SeedAdvanceAsync(
        decimal amount,
        Guid? supplierId = null,
        Guid? companyId = null,
        Guid? branchId = null,
        DateOnly? paymentDate = null
    )
    {
        await using var db = CreateContext(companyId);
        var payment = SupplierPayment.Create(
            _tenantId,
            companyId ?? _companyId,
            branchId ?? _branchId,
            supplierId ?? _supplierId,
            paymentDate ?? _today,
            amount,
            $"SP-{Guid.NewGuid():N}"[..12],
            $"REC-{Guid.NewGuid():N}"[..12],
            [
                new SupplierPaymentMethodLineInput(
                    _transferMethodId,
                    _companyBankAccountId,
                    null,
                    amount,
                    "OP-1",
                    TransactionDate: paymentDate ?? _today
                ),
            ],
            [],
            [],
            _userId,
            unappliedAmountConfirmed: true,
            allowWithoutPayable: true
        );
        var credit = SupplierCredit.CreateFromSupplierPayment(
            _tenantId,
            companyId ?? _companyId,
            branchId ?? _branchId,
            supplierId ?? _supplierId,
            "USD",
            payment.Id,
            amount,
            _userId
        );
        db.SupplierPayments.Add(payment);
        db.Set<SupplierCredit>().Add(credit);
        await db.SaveChangesAsync();
        return (credit.Id, payment);
    }

    /// <summary>Devolución autorizada real sobre una compra ya pagada → crédito origen PurchaseReturn.</summary>
    private async Task<(Guid CreditId, PurchaseReturn Return)> SeedReturnCreditAsync(decimal amount)
    {
        await using var db = CreateContext();
        var invoice = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Distribuidora Andina",
            "1710034065001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999)}",
            _today,
            _userId,
            _paymentTermId,
            "Contado",
            1,
            30
        );
        invoice.ReplaceLines(
            [
                PurchaseInvoiceDetail.Create(
                    invoice.Id,
                    _tenantId,
                    "Producto",
                    quantity: 1m,
                    unitPrice: amount,
                    vatCode: "10",
                    uomCode: "UNIT"
                ),
            ],
            _userId
        );
        invoice.Confirm(_userId);
        var payable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            invoice.Id,
            "01",
            invoice.InvoiceNumber,
            _today,
            _today,
            _userId
        );
        payable.AddInstallment(1, _today.AddDays(30), invoice.GrandTotal);
        payable.RegisterPayment(payable.TotalAmount, _userId);

        var ret = PurchaseReturn.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            invoice.Id,
            _supplierId,
            "Producto defectuoso",
            [new PurchaseReturn.DraftLineInput(invoice.Lines[0].Id, _itemId, 1m, _warehouseId)],
            _userId,
            Guid.NewGuid(),
            "hash-draft"
        );
        var line = invoice.Lines[0];
        var credit = ret.Authorize(
            Random.Shared.Next(1, 99999999).ToString("D8"),
            new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>
            {
                [line.Id] = new(
                    line.Quantity,
                    line.LineSubtotal,
                    line.DiscountAmount,
                    line.VatAmount,
                    line.IceAmount,
                    line.VatCode,
                    line.VatRate,
                    line.IceCode,
                    line.IceRate,
                    line.LandedUnitCost,
                    Array.Empty<PurchaseReturn.OriginalLineTaxSnapshot>()
                ),
            },
            balanceDueBeforeApplication: 0m,
            invoice.CurrencyCode,
            hasIssuedRetention: false,
            _userId,
            Guid.NewGuid(),
            "hash-authorize"
        )!;

        db.PurchaseInvoices.Add(invoice);
        db.AccountsPayables.Add(payable);
        db.PurchaseReturns.Add(ret);
        db.Set<SupplierCredit>().Add(credit);
        await db.SaveChangesAsync();
        return (credit.Id, ret);
    }

    private async Task<Guid> SeedExpensePayableAsync(decimal total)
    {
        await using var db = CreateContext();
        var payable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.ExpenseDocument,
            Guid.NewGuid(),
            "EXP",
            "GAS-000777",
            _today,
            _today,
            _userId
        );
        payable.AddInstallment(1, _today.AddDays(30), total);
        db.AccountsPayables.Add(payable);
        await db.SaveChangesAsync();
        return payable.Id;
    }

    // ── Queries / comandos reales ──────────────────────────────────────────

    private async Task<SupplierCreditListResultDto> ListAsync(
        GetSupplierCreditListQuery query,
        Guid? companyId = null
    )
    {
        await using var db = CreateContext(companyId);
        var company = new FixedCurrentCompany(() => companyId ?? _companyId);
        var result = await new GetSupplierCreditListHandler(
            new SupplierCreditRepository(db, company),
            new BusinessPartnerRepository(db),
            new CompanyRepository(db),
            new FixedCurrentTenant(() => _tenantId),
            company
        ).Handle(query, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!;
    }

    private async Task<Result<SupplierCreditDto>> DetailAsync(Guid creditId, Guid? companyId = null)
    {
        await using var db = CreateContext(companyId);
        var company = new FixedCurrentCompany(() => companyId ?? _companyId);
        return await new GetSupplierCreditByIdHandler(
            new SupplierCreditRepository(db, company),
            new SupplierCreditRefundTransactionRepository(db, company),
            new AccountsPayableRepository(db),
            new BusinessPartnerRepository(db),
            new AccessRepository(db),
            new CompanyRepository(db),
            new PaymentMethodRepository(db),
            new FixedCurrentTenant(() => _tenantId)
        ).Handle(new GetSupplierCreditByIdQuery(creditId), CancellationToken.None);
    }

    private async Task<T> RunAsync<T>(Func<ErpDbContext, FixedCurrentCompany, Task<Result<T>>> run)
    {
        await using var db = CreateContext();
        var result = await run(db, new FixedCurrentCompany(() => _companyId));
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!;
    }

    private Task<SupplierCreditDto> ApplyAsync(Guid creditId, Guid payableId, decimal amount) =>
        RunAsync(
            (db, c) =>
                new ApplySupplierCreditHandler(
                    new SupplierCreditRepository(db, c),
                    new AccountsPayableRepository(db),
                    new PurchaseInvoiceRepository(db, c),
                    new PurchaseReturnRepository(db, c),
                    new CompanyRepository(db),
                    new UnitOfWork(db),
                    new PostgresDatabaseExceptionTranslator(),
                    new FixedCurrentTenant(() => _tenantId),
                    new FixedCurrentUser(_userId)
                ).Handle(
                    new ApplySupplierCreditCommand(creditId, payableId, amount, Guid.NewGuid()),
                    CancellationToken.None
                )
        );

    private Task<SupplierCreditDto> ReverseApplicationAsync(
        Guid creditId,
        Guid movementId,
        Guid payableId
    ) =>
        RunAsync(
            (db, c) =>
                new ReverseSupplierCreditApplicationHandler(
                    new SupplierCreditRepository(db, c),
                    new AccountsPayableRepository(db),
                    new PurchaseReturnRepository(db, c),
                    new UnitOfWork(db),
                    new PostgresDatabaseExceptionTranslator(),
                    new FixedCurrentTenant(() => _tenantId),
                    new FixedCurrentUser(_userId)
                ).Handle(
                    new ReverseSupplierCreditApplicationCommand(
                        creditId,
                        movementId,
                        payableId,
                        Guid.NewGuid()
                    ),
                    CancellationToken.None
                )
        );

    private Task<SupplierCreditRefundTransactionDto> RefundAsync(
        Guid creditId,
        decimal amount,
        string reference
    ) =>
        RunAsync(
            (db, c) =>
                new RegisterSupplierCreditRefundHandler(
                    new SupplierCreditRepository(db, c),
                    new SupplierCreditRefundTransactionRepository(db, c),
                    new CompanyBankAccountRepository(db, c),
                    new CashRegisterRepository(db, c),
                    new AccountRepository(db),
                    new PaymentMethodRepository(db),
                    new CashSessionRepository(db, c),
                    new CompanyRepository(db),
                    new UnitOfWork(db),
                    new PostgresDatabaseExceptionTranslator(),
                    new FixedCurrentTenant(() => _tenantId),
                    new FixedCurrentUser(_userId)
                ).Handle(
                    new RegisterSupplierCreditRefundCommand(
                        creditId,
                        _companyBankAccountId,
                        null,
                        "TRANSFER",
                        amount,
                        _today,
                        reference,
                        Guid.NewGuid()
                    ),
                    CancellationToken.None
                )
        );

    private Task<SupplierCreditRefundTransactionDto> ReverseRefundAsync(
        Guid creditId,
        Guid refundTransactionId,
        string reason
    ) =>
        RunAsync(
            (db, c) =>
                new ReverseSupplierCreditRefundHandler(
                    new SupplierCreditRepository(db, c),
                    new SupplierCreditRefundTransactionRepository(db, c),
                    new CashSessionRepository(db, c),
                    new UnitOfWork(db),
                    new PostgresDatabaseExceptionTranslator(),
                    new FixedCurrentTenant(() => _tenantId),
                    new FixedCurrentUser(_userId)
                ).Handle(
                    new ReverseSupplierCreditRefundCommand(
                        creditId,
                        refundTransactionId,
                        reason,
                        _today.AddDays(1),
                        Guid.NewGuid()
                    ),
                    CancellationToken.None
                )
        );

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Listado_expone_proveedor_real_y_origen_de_devolucion_y_de_pago_con_fecha()
    {
        var (returnCreditId, ret) = await SeedReturnCreditAsync(50m);
        var (advanceCreditId, payment) = await SeedAdvanceAsync(
            20m,
            paymentDate: new DateOnly(2026, 9, 10)
        );

        var items = (await ListAsync(new GetSupplierCreditListQuery())).Items;

        var fromReturn = items.Single(i => i.Id == returnCreditId);
        fromReturn.SupplierName.Should().Be("Distribuidora Andina");
        fromReturn.SourceType.Should().Be("PurchaseReturn");
        fromReturn.SourceDocumentId.Should().Be(ret.Id);
        fromReturn.SourceDocumentNumber.Should().Be(ret.ReturnNumber);
        fromReturn
            .SourceDate.Should()
            .Be(
                CompanyTimeZone.LocalDate(
                    ret.AuthorizedAtUtc!.Value,
                    CompanyTimeZone.Resolve("America/Guayaquil")
                ),
                "la devolución no tiene fecha de negocio: su fecha es la autorización en la zona de la empresa"
            );
        fromReturn.IsOpen.Should().BeTrue();

        var fromPayment = items.Single(i => i.Id == advanceCreditId);
        fromPayment.SupplierName.Should().Be("Distribuidora Andina");
        fromPayment.SourceType.Should().Be("SupplierPayment");
        fromPayment.SourceDocumentId.Should().Be(payment.Id);
        fromPayment
            .SourceDocumentNumber.Should()
            .Be(payment.SystemNumber, "número del sistema, no el recibo externo");
        fromPayment.SourceDate.Should().Be(new DateOnly(2026, 9, 10));
        (fromPayment.OriginalAmount, fromPayment.AvailableAmount, fromPayment.CurrencyCode)
            .Should()
            .Be((20m, 20m, "USD"));
    }

    [Fact]
    public async Task Filtros_por_proveedor_origen_y_abierto_se_aplican_en_BD()
    {
        var (returnCreditId, _) = await SeedReturnCreditAsync(50m);
        var (advanceCreditId, _) = await SeedAdvanceAsync(20m);
        var (otherSupplierCreditId, _) = await SeedAdvanceAsync(30m, supplierId: _otherSupplierId);
        var (closedCreditId, _) = await SeedAdvanceAsync(15m);
        var payableId = await SeedExpensePayableAsync(100m);
        await ApplyAsync(closedCreditId, payableId, 15m);

        (await ListAsync(new GetSupplierCreditListQuery(SupplierId: _otherSupplierId)))
            .Items.Select(i => i.Id)
            .Should()
            .Equal(otherSupplierCreditId);
        (
            await ListAsync(
                new GetSupplierCreditListQuery(SourceType: SupplierCreditSourceType.PurchaseReturn)
            )
        )
            .Items.Select(i => i.Id)
            .Should()
            .Equal(returnCreditId);
        var payments = await ListAsync(
            new GetSupplierCreditListQuery(SourceType: SupplierCreditSourceType.SupplierPayment)
        );
        payments
            .Items.Select(i => i.Id)
            .Should()
            .BeEquivalentTo([advanceCreditId, otherSupplierCreditId, closedCreditId]);
        payments.Total.Should().Be(3);

        var closed = await ListAsync(new GetSupplierCreditListQuery(IsOpen: false));
        closed.Items.Select(i => i.Id).Should().Equal(closedCreditId);
        closed.Items.Single().IsOpen.Should().BeFalse();
        (await ListAsync(new GetSupplierCreditListQuery(IsOpen: true)))
            .Items.Should()
            .NotContain(i => i.Id == closedCreditId)
            .And.HaveCount(3);
        (
            await ListAsync(
                new GetSupplierCreditListQuery(
                    SupplierId: _supplierId,
                    SourceType: SupplierCreditSourceType.SupplierPayment,
                    IsOpen: true
                )
            )
        )
            .Items.Select(i => i.Id)
            .Should()
            .Equal(advanceCreditId);
    }

    [Fact]
    public async Task Saldos_de_otra_empresa_no_aparecen_ni_se_pueden_leer()
    {
        var (ownCreditId, _) = await SeedAdvanceAsync(20m);
        var (foreignCreditId, _) = await SeedAdvanceAsync(
            40m,
            companyId: _otherCompanyId,
            branchId: _otherBranchId
        );

        var own = await ListAsync(new GetSupplierCreditListQuery());
        own.Items.Select(i => i.Id).Should().Equal(ownCreditId);
        own.Total.Should().Be(1);
        (await DetailAsync(foreignCreditId))
            .IsSuccess.Should()
            .BeFalse("fail-closed: fuera de la empresa operativa");

        (await ListAsync(new GetSupplierCreditListQuery(), _otherCompanyId))
            .Items.Select(i => i.Id)
            .Should()
            .Equal(foreignCreditId);
    }

    [Fact]
    public async Task Paginacion_es_estable_y_completa()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
            ids.Add((await SeedAdvanceAsync(10m + i)).CreditId);

        var pages = new List<SupplierCreditListItemDto>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await ListAsync(new GetSupplierCreditListQuery(page, 2));
            result.Total.Should().Be(5);
            result.Items.Should().HaveCountLessThanOrEqualTo(2);
            pages.AddRange(result.Items);
        }

        pages.Select(p => p.Id).Should().OnlyHaveUniqueItems().And.BeEquivalentTo(ids);
        (await ListAsync(new GetSupplierCreditListQuery(1, 2)))
            .Items.Select(i => i.Id)
            .Should()
            .Equal(pages.Take(2).Select(p => p.Id), "mismo orden en lecturas repetidas");
    }

    [Fact]
    public async Task Detalle_expone_CxP_destino_reembolso_con_destino_y_referencia_y_enlaza_reversas()
    {
        var (creditId, payment) = await SeedAdvanceAsync(100m);
        var payableId = await SeedExpensePayableAsync(80m);

        var applied = await ApplyAsync(creditId, payableId, 30m);
        var applicationId = applied.Movements.Single(m => m.MovementType == "Application").Id;
        var refund = await RefundAsync(creditId, 25m, "TRX-445566");
        await ReverseRefundAsync(creditId, refund.Id, "Transferencia devuelta por el banco");
        await ReverseApplicationAsync(creditId, applicationId, payableId);

        var detail = await DetailAsync(creditId);

        detail.IsSuccess.Should().BeTrue(detail.Error);
        var dto = detail.Value!;
        (
            dto.SupplierName,
            dto.SourceType,
            dto.SourceSupplierPaymentId,
            dto.SourcePurchaseReturnId,
            dto.SourceDocumentNumber,
            dto.SourceDate
        )
            .Should()
            .Be(
                (
                    "Distribuidora Andina",
                    "SupplierPayment",
                    payment.Id,
                    (Guid?)null,
                    payment.SystemNumber,
                    _today
                )
            );
        (dto.OriginalAmount, dto.AvailableAmount).Should().Be((100m, 100m));
        dto.Movements.Select(m => m.MovementType)
            .Should()
            .Equal("Application", "Refund", "ReversalOfRefund", "ReversalOfApplication");
        dto.Movements.Should().BeInAscendingOrder(m => m.CreatedAtUtc);
        dto.Movements.Should()
            .OnlyContain(m => m.CreatedByUserId == _userId && m.CreatedByName == "Ana Tesorera");

        var application = dto.Movements[0];
        (
            application.Amount,
            application.AccountsPayableId,
            application.PayableDocumentNumber,
            application.PayableOriginType
        )
            .Should()
            .Be((30m, payableId, "GAS-000777", "ExpenseDocument"));
        application.RefundTransactionId.Should().BeNull();

        var refundMovement = dto.Movements[1];
        (
            refundMovement.Amount,
            refundMovement.RefundTransactionId,
            refundMovement.EffectiveDate,
            refundMovement.DestinationType,
            refundMovement.DestinationName,
            refundMovement.PaymentMethodCode,
            refundMovement.PaymentMethodName,
            refundMovement.ReferenceNumber,
            refundMovement.Reason
        )
            .Should()
            .Be(
                (
                    25m,
                    refund.Id,
                    _today,
                    "Bank",
                    "Banco Pichincha CTE",
                    "TRANSFER",
                    "Transferencia",
                    "TRX-445566",
                    (string?)null
                )
            );
        refundMovement.AccountsPayableId.Should().BeNull();

        var refundReversal = dto.Movements[2];
        refundReversal.ReversalOfMovementId.Should().Be(refundMovement.Id);
        refundMovement.ReversedByMovementId.Should().Be(refundReversal.Id);
        (
            refundReversal.Reason,
            refundReversal.EffectiveDate,
            refundReversal.DestinationType,
            refundReversal.ReferenceNumber
        )
            .Should()
            .Be(("Transferencia devuelta por el banco", _today.AddDays(1), "Bank", (string?)null));

        var applicationReversal = dto.Movements[3];
        applicationReversal.ReversalOfMovementId.Should().Be(application.Id);
        application.ReversedByMovementId.Should().Be(applicationReversal.Id);
        (
            applicationReversal.AccountsPayableId,
            applicationReversal.PayableDocumentNumber,
            applicationReversal.Reason
        )
            .Should()
            .Be(
                (payableId, "GAS-000777", (string?)null),
                "la reversa de aplicación no guarda motivo en el dominio"
            );
    }

    /// <summary>
    /// ZH-SUPPLIER-BALANCES-CROSS-LINKS-02D-F — saldo abierto agregado por proveedor para el aviso de
    /// la CxP: suma solo saldos abiertos del proveedor en la empresa operativa (nunca de otra empresa
    /// ni de otro proveedor), sin cargar agregados; con uno solo expone su Id.
    /// </summary>
    [Fact]
    public async Task Saldo_abierto_por_proveedor_agrega_en_BD_con_aislamiento_por_empresa()
    {
        var (a, _) = await SeedAdvanceAsync(20m);
        var (b, _) = await SeedAdvanceAsync(30m);
        var (closed, _) = await SeedAdvanceAsync(15m);
        await SeedAdvanceAsync(99m, supplierId: _otherSupplierId);
        await SeedAdvanceAsync(77m, companyId: _otherCompanyId, branchId: _otherBranchId);
        await ApplyAsync(closed, await SeedExpensePayableAsync(100m), 15m);

        async Task<SupplierCreditOpenBalance> BalanceAsync()
        {
            await using var db = CreateContext();
            return await new SupplierCreditRepository(
                db,
                new FixedCurrentCompany(() => _companyId)
            ).GetOpenBalanceBySupplierAsync(_tenantId, _supplierId, CancellationToken.None);
        }

        (await BalanceAsync()).Should().Be(new SupplierCreditOpenBalance(50m, 2, null));

        await ApplyAsync(b, await SeedExpensePayableAsync(100m), 30m);
        (await BalanceAsync()).Should().Be(new SupplierCreditOpenBalance(20m, 1, a));

        await ApplyAsync(a, await SeedExpensePayableAsync(100m), 20m);
        (await BalanceAsync()).Should().Be(new SupplierCreditOpenBalance(0m, 0, null));
    }

    // ── Dobles ─────────────────────────────────────────────────────────────

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => null;
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }
}
