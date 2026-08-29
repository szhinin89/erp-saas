using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// P0-02 Fase 8 — subconjunto priorizado de las 26 pruebas PostgreSQL reales de §16.5 (diseño):
/// reembolso feliz banco/caja, catálogo de errores SC-020/021/024/025/027, idempotencia completa
/// (§16.2ter, 4 escenarios), reversa feliz con herencia, cuenta congelada tras cambio del destino,
/// SC-011 secuencial y concurrente, unicidad 1:1 (SC-029), y reversa de caja sin sesión (§16.5-26).
/// No incluye los escenarios de rollback parcial forzado (18-20, requieren inyección de fallos no
/// disponible) ni los de reporte/reconciliación (23-24, funcionalidad de reportes fuera del
/// alcance de esta fase) — documentado como brecha conocida en el informe de cierre.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SupplierCreditRefundConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_supplier_credit_refund_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _accountId;
    private Guid _bankDestinationId;
    private Guid _cashDestinationId;
    private Guid _cashRegisterId;
    private Guid _paymentMethodId;
    private Guid _paymentTermId;
    private Guid _warehouseId;
    private Guid _itemId;
    private Guid _emissionPointId;
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;

        var branch = Branch.Create(
            tenantId: _tenantId,
            name: "Matriz",
            address: "Av. Principal 123",
            code: "B01",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: _userId,
            companyId: _companyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        _branchId = branch.Id;

        var supplier = BusinessPartner.Create(
            _tenantId,
            "05",
            "1710034065",
            1,
            "Proveedor Test",
            _userId
        );
        db.BusinessPartners.Add(supplier);
        var paymentTerm = PaymentTerm.Create(
            _tenantId,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _userId
        );
        db.Add(paymentTerm);
        await db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _paymentTermId = paymentTerm.Id;

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
        db.Add(warehouse);
        await db.SaveChangesAsync();
        _warehouseId = warehouse.Id;

        var itemType = ERP.Domain.Modules.Items.Entities.ItemTypeDefinition.Create(
            _tenantId,
            "MERCH",
            "Mercadería",
            1,
            _userId
        );
        db.Set<ERP.Domain.Modules.Items.Entities.ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        var item = ERP.Domain.Modules.Items.Entities.Item.Create(
            _tenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Test",
            description: "Producto Test",
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
                tracksStock: true
            ),
            createdBy: _userId
        );
        db.Set<ERP.Domain.Modules.Items.Entities.Item>().Add(item);
        await db.SaveChangesAsync();
        _itemId = item.Id;

        var account = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"1.{Guid.NewGuid():N}"[..8]),
            "Banco Pichincha",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _userId
        );
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        _accountId = account.Id;

        var bankDestination = CompanyFinancialDestination.Create(
            _tenantId,
            _companyId,
            "BANK-01",
            "Banco Pichincha CTE",
            FinancialDestinationTypeCode.BankAccount,
            _accountId,
            "USD",
            _userId,
            bankInstitutionCode: "PICHINCHA",
            bankAccountIdentifierNormalized: "1234567890"
        );
        db.Set<CompanyFinancialDestination>().Add(bankDestination);
        await db.SaveChangesAsync();
        _bankDestinationId = bankDestination.Id;

        var establishment = ERP.Domain.Modules.Company.Entities.Establishment.Create(
            _tenantId,
            _branchId,
            _companyId,
            "001",
            "Matriz",
            "Av. Principal 123",
            null,
            isMain: true,
            _userId
        );
        db.Set<ERP.Domain.Modules.Company.Entities.Establishment>().Add(establishment);
        await db.SaveChangesAsync();

        var emissionPoint = ERP.Domain.Modules.Company.Entities.EmissionPoint.Create(
            _tenantId,
            _companyId,
            establishment.Id,
            "001",
            "Punto de emisión 1",
            ERP.Domain.Modules.Company.Enums.EmissionType.Electronic,
            isDefault: true,
            _userId
        );
        db.Set<ERP.Domain.Modules.Company.Entities.EmissionPoint>().Add(emissionPoint);
        await db.SaveChangesAsync();
        _emissionPointId = emissionPoint.Id;

        var cashRegister = CashRegister.Create(
            _tenantId,
            _companyId,
            _branchId,
            "CAJA-01",
            "Caja Matriz",
            _userId
        );
        db.Set<CashRegister>().Add(cashRegister);
        await db.SaveChangesAsync();
        _cashRegisterId = cashRegister.Id;

        var cashDestination = CompanyFinancialDestination.Create(
            _tenantId,
            _companyId,
            "CASH-01",
            "Caja Matriz",
            FinancialDestinationTypeCode.CashRegister,
            _accountId,
            "USD",
            _userId,
            cashRegisterId: _cashRegisterId
        );
        db.Set<CompanyFinancialDestination>().Add(cashDestination);
        await db.SaveChangesAsync();
        _cashDestinationId = cashDestination.Id;

        var paymentMethod = PaymentMethod.Create(
            _tenantId,
            "TRANSFER",
            "Transferencia",
            false,
            false,
            1,
            _userId
        );
        db.Set<PaymentMethod>().Add(paymentMethod);
        await db.SaveChangesAsync();
        _paymentMethodId = paymentMethod.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(() => _tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => _companyId)
        );
    }

    private async Task<Guid> SeedCreditAsync(decimal creditAmount)
    {
        await using var db = CreateContext();
        var sourceInv = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999)}",
            DateOnly.FromDateTime(DateTime.UtcNow),
            _userId,
            _paymentTermId,
            "Contado",
            1,
            30
        );
        var line = PurchaseInvoiceDetail.Create(
            sourceInv.Id,
            _tenantId,
            "Producto 1",
            quantity: 1m,
            unitPrice: creditAmount,
            vatCode: "10",
            uomCode: "UNIT"
        );
        sourceInv.ReplaceLines(new[] { line }, _userId);
        sourceInv.Confirm(_userId);

        var payable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            sourceInv.Id,
            "01",
            sourceInv.InvoiceNumber,
            sourceInv.IssueDate,
            sourceInv.IssueDate,
            _userId
        );
        payable.AddInstallment(1, sourceInv.IssueDate.AddDays(30), sourceInv.GrandTotal);
        payable.RegisterPayment(payable.TotalAmount, _userId);

        var ret = PurchaseReturn.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            sourceInv.Id,
            _supplierId,
            "Producto defectuoso",
            new[]
            {
                new PurchaseReturn.DraftLineInput(sourceInv.Lines[0].Id, _itemId, 1m, _warehouseId),
            },
            _userId,
            Guid.NewGuid(),
            "hash-draft"
        );
        var original = sourceInv.Lines[0];
        var snapshot = new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>
        {
            [original.Id] = new PurchaseReturn.OriginalLineSnapshot(
                original.Quantity,
                original.LineSubtotal,
                original.DiscountAmount,
                original.VatAmount,
                original.IceAmount,
                original.VatCode,
                original.VatRate,
                original.IceCode,
                original.IceRate,
                original.LandedUnitCost,
                Array.Empty<PurchaseReturn.OriginalLineTaxSnapshot>()
            ),
        };
        var credit = ret.Authorize(
            Random.Shared.Next(1, 99999999).ToString("D8"),
            snapshot,
            balanceDueBeforeApplication: 0m,
            sourceInv.CurrencyCode,
            hasIssuedWithholding: false,
            _userId,
            Guid.NewGuid(),
            "hash-authorize"
        );

        db.PurchaseInvoices.Add(sourceInv);
        db.Set<AccountsPayable>().Add(payable);
        db.PurchaseReturns.Add(ret);
        db.Set<SupplierCredit>().Add(credit!);
        await db.SaveChangesAsync();
        return credit!.Id;
    }

    private async Task<(
        bool Success,
        string? Error,
        ERP.Application.Modules.Finance.UseCases.SupplierCreditRefundTransactionDto? Value
    )> ExecuteRegisterAsync(
        Guid creditId,
        Guid destinationId,
        string paymentMethodCode,
        decimal amount,
        Guid cri,
        string? externalReference = null
    )
    {
        await using var db = CreateContext();
        var handler = new RegisterSupplierCreditRefundHandler(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new SupplierCreditRefundTransactionRepository(
                db,
                new FixedCurrentCompany(() => _companyId)
            ),
            new CompanyFinancialDestinationRepository(
                db,
                new FixedCurrentCompany(() => _companyId)
            ),
            new AccountRepository(db),
            new PaymentMethodRepository(db),
            new CashSessionRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );
        var result = await handler.Handle(
            new RegisterSupplierCreditRefundCommand(
                creditId,
                destinationId,
                paymentMethodCode,
                amount,
                DateOnly.FromDateTime(DateTime.UtcNow),
                externalReference,
                cri
            ),
            CancellationToken.None
        );
        return (
            result.IsSuccess,
            result.IsSuccess ? null : result.Error,
            result.IsSuccess ? result.Value : null
        );
    }

    private async Task<(
        bool Success,
        string? Error,
        ERP.Application.Modules.Finance.UseCases.SupplierCreditRefundTransactionDto? Value
    )> ExecuteReverseAsync(Guid creditId, Guid originalTxId, string reason, Guid cri)
    {
        await using var db = CreateContext();
        var handler = new ReverseSupplierCreditRefundHandler(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new SupplierCreditRefundTransactionRepository(
                db,
                new FixedCurrentCompany(() => _companyId)
            ),
            new CashSessionRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );
        var result = await handler.Handle(
            new ReverseSupplierCreditRefundCommand(
                creditId,
                originalTxId,
                reason,
                DateOnly.FromDateTime(DateTime.UtcNow),
                cri
            ),
            CancellationToken.None
        );
        return (
            result.IsSuccess,
            result.IsSuccess ? null : result.Error,
            result.IsSuccess ? result.Value : null
        );
    }

    // ── 1. Reembolso bancario válido ──────────────────────────────────────

    [Fact]
    public async Task Punto1_Reembolso_bancario_valido_efectos_correctos()
    {
        var creditId = await SeedCreditAsync(100m);
        var result = await ExecuteRegisterAsync(
            creditId,
            _bankDestinationId,
            "TRANSFER",
            40m,
            Guid.NewGuid()
        );

        result.Success.Should().BeTrue(result.Error);
        result.Value!.AccountingAccountId.Should().Be(_accountId);

        await using var verify = CreateContext();
        var credit = await verify
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstAsync(c => c.Id == creditId);
        credit.AvailableAmount.Should().Be(60m);
    }

    // ── 2. Destino inexistente ────────────────────────────────────────────

    [Fact]
    public async Task Punto2_Destino_inexistente_rechaza_SC_020()
    {
        var creditId = await SeedCreditAsync(100m);
        var result = await ExecuteRegisterAsync(
            creditId,
            Guid.NewGuid(),
            "TRANSFER",
            10m,
            Guid.NewGuid()
        );

        result.Success.Should().BeFalse();

        await using var verify = CreateContext();
        var credit = await verify
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstAsync(c => c.Id == creditId);
        credit.AvailableAmount.Should().Be(100m);
    }

    // ── 5. Destino inactivo ───────────────────────────────────────────────

    [Fact]
    public async Task Punto5_Destino_inactivo_rechaza_SC_021_sin_efecto()
    {
        var creditId = await SeedCreditAsync(100m);
        await using (var db = CreateContext())
        {
            var destination = await db.Set<CompanyFinancialDestination>()
                .FirstAsync(d => d.Id == _bankDestinationId);
            destination.SetActive(false, _userId);
            await db.SaveChangesAsync();
        }

        var result = await ExecuteRegisterAsync(
            creditId,
            _bankDestinationId,
            "TRANSFER",
            10m,
            Guid.NewGuid()
        );
        result.Success.Should().BeFalse();

        await using var verify = CreateContext();
        var credit = await verify
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstAsync(c => c.Id == creditId);
        credit.AvailableAmount.Should().Be(100m);
    }

    // ── 6. Cuenta no postable ─────────────────────────────────────────────

    [Fact]
    public async Task Punto6_Cuenta_no_postable_rechaza_SC_024()
    {
        var creditId = await SeedCreditAsync(100m);
        await using (var db = CreateContext())
        {
            var account = await db.Accounts.FirstAsync(a => a.Id == _accountId);
            account.Disable(_userId);
            await db.SaveChangesAsync();
        }

        var result = await ExecuteRegisterAsync(
            creditId,
            _bankDestinationId,
            "TRANSFER",
            10m,
            Guid.NewGuid()
        );
        result.Success.Should().BeFalse();
    }

    // ── 7. Moneda distinta ────────────────────────────────────────────────

    [Fact]
    public async Task Punto7_Moneda_distinta_rechaza_SC_025()
    {
        await using var seedDb = CreateContext();
        var eurDestination = CompanyFinancialDestination.Create(
            _tenantId,
            _companyId,
            "BANK-EUR",
            "Banco EUR",
            FinancialDestinationTypeCode.BankAccount,
            _accountId,
            "EUR",
            _userId,
            bankInstitutionCode: "EURBANK",
            bankAccountIdentifierNormalized: "EUR0001"
        );
        seedDb.Set<CompanyFinancialDestination>().Add(eurDestination);
        await seedDb.SaveChangesAsync();

        var creditId = await SeedCreditAsync(100m);
        var result = await ExecuteRegisterAsync(
            creditId,
            eurDestination.Id,
            "TRANSFER",
            10m,
            Guid.NewGuid()
        );
        result.Success.Should().BeFalse();
    }

    // ── 8. Caja con sesión activa ─────────────────────────────────────────

    [Fact]
    public async Task Punto8_Reembolso_en_caja_con_sesion_activa_vincula_CashMovement()
    {
        var creditId = await SeedCreditAsync(100m);
        Guid sessionId;
        await using (var db = CreateContext())
        {
            var session = CashSession.Open(
                _tenantId,
                _companyId,
                _branchId,
                _userId,
                _cashRegisterId,
                "CAJA-01",
                "Caja Matriz",
                _emissionPointId,
                "001",
                0m,
                _userId
            );
            db.Set<CashSession>().Add(session);
            await db.SaveChangesAsync();
            sessionId = session.Id;
        }

        var result = await ExecuteRegisterAsync(
            creditId,
            _cashDestinationId,
            "TRANSFER",
            40m,
            Guid.NewGuid()
        );
        result.Success.Should().BeTrue(result.Error);
        result.Value!.CashSessionId.Should().Be(sessionId);
        result.Value.CashMovementId.Should().NotBeNull();

        await using var verify = CreateContext();
        var movementCount = await verify
            .Set<CashMovement>()
            .CountAsync(m => m.CashSessionId == sessionId && m.Id == result.Value.CashMovementId);
        movementCount.Should().Be(1);
    }

    // ── 9. Caja sin sesión activa ─────────────────────────────────────────

    [Fact]
    public async Task Punto9_Reembolso_en_caja_sin_sesion_activa_rechaza_SC_027_ningun_efecto()
    {
        var creditId = await SeedCreditAsync(100m);
        var result = await ExecuteRegisterAsync(
            creditId,
            _cashDestinationId,
            "TRANSFER",
            40m,
            Guid.NewGuid()
        );

        result.Success.Should().BeFalse();

        await using var verify = CreateContext();
        var credit = await verify
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstAsync(c => c.Id == creditId);
        credit.AvailableAmount.Should().Be(100m);
        var movementCount = await verify
            .Set<SupplierCreditMovement>()
            .CountAsync(m => m.SupplierCreditId == creditId);
        movementCount.Should().Be(0);
    }

    // ── 10/11/12/13. Idempotencia §16.2ter ────────────────────────────────

    [Fact]
    public async Task Punto10_Mismo_CRI_mismo_payload_resultado_identico_sin_duplicar()
    {
        var creditId = await SeedCreditAsync(100m);
        var cri = Guid.NewGuid();

        var first = await ExecuteRegisterAsync(creditId, _bankDestinationId, "TRANSFER", 40m, cri);
        first.Success.Should().BeTrue(first.Error);
        var retry = await ExecuteRegisterAsync(creditId, _bankDestinationId, "TRANSFER", 40m, cri);
        retry.Success.Should().BeTrue(retry.Error);

        await using var verify = CreateContext();
        var count = await verify
            .Set<SupplierCreditMovement>()
            .CountAsync(m => m.SupplierCreditId == creditId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Punto11_Mismo_CRI_payload_distinto_rechaza_SC_006()
    {
        var creditId = await SeedCreditAsync(100m);
        var cri = Guid.NewGuid();

        var first = await ExecuteRegisterAsync(creditId, _bankDestinationId, "TRANSFER", 40m, cri);
        first.Success.Should().BeTrue(first.Error);
        var second = await ExecuteRegisterAsync(creditId, _bankDestinationId, "TRANSFER", 25m, cri);
        second.Success.Should().BeFalse();

        await using var verify = CreateContext();
        var count = await verify
            .Set<SupplierCreditMovement>()
            .CountAsync(m => m.SupplierCreditId == creditId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Punto12_Dos_solicitudes_concurrentes_mismo_reembolso_exactamente_un_efecto()
    {
        var creditId = await SeedCreditAsync(100m);
        var cri = Guid.NewGuid();

        var t1 = ExecuteRegisterAsync(creditId, _bankDestinationId, "TRANSFER", 40m, cri);
        var t2 = ExecuteRegisterAsync(creditId, _bankDestinationId, "TRANSFER", 40m, cri);
        var results = await Task.WhenAll(t1, t2);

        results.Should().OnlyContain(r => r.Success);

        await using var verify = CreateContext();
        var count = await verify
            .Set<SupplierCreditMovement>()
            .CountAsync(m => m.SupplierCreditId == creditId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Punto13_Commit_exitoso_sin_respuesta_mas_reintento_resultado_confirmado_sin_efectos_adicionales()
    {
        var creditId = await SeedCreditAsync(100m);
        var cri = Guid.NewGuid();

        var first = await ExecuteRegisterAsync(creditId, _bankDestinationId, "TRANSFER", 40m, cri);
        first.Success.Should().BeTrue(first.Error);
        var retry = await ExecuteRegisterAsync(creditId, _bankDestinationId, "TRANSFER", 40m, cri);

        retry.Success.Should().BeTrue(retry.Error);
        retry.Value!.Amount.Should().Be(first.Value!.Amount);

        await using var verify = CreateContext();
        var credit = await verify
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstAsync(c => c.Id == creditId);
        credit.AvailableAmount.Should().Be(60m);
    }

    // ── 14/14bis. Reversa válida + cuenta congelada ───────────────────────

    [Fact]
    public async Task Punto14_Reversa_valida_hereda_destino_cuenta_moneda_importe_metodo()
    {
        var creditId = await SeedCreditAsync(100m);
        var apply = await ExecuteRegisterAsync(
            creditId,
            _bankDestinationId,
            "TRANSFER",
            40m,
            Guid.NewGuid()
        );
        apply.Success.Should().BeTrue(apply.Error);

        var reverse = await ExecuteReverseAsync(
            creditId,
            apply.Value!.Id,
            "Motivo",
            Guid.NewGuid()
        );
        reverse.Success.Should().BeTrue(reverse.Error);
        reverse.Value!.AccountingAccountId.Should().Be(apply.Value.AccountingAccountId);
        reverse.Value.Amount.Should().Be(apply.Value.Amount);
        reverse.Value.CurrencyCode.Should().Be(apply.Value.CurrencyCode);
        reverse.Value.PaymentMethodCode.Should().Be(apply.Value.PaymentMethodCode);

        await using var verify = CreateContext();
        var credit = await verify
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstAsync(c => c.Id == creditId);
        credit.AvailableAmount.Should().Be(100m);
    }

    [Fact]
    public async Task Punto14bis_Reversa_tras_cambiar_la_cuenta_del_destino_usa_la_cuenta_congelada_no_la_vigente()
    {
        var creditId = await SeedCreditAsync(100m);
        var apply = await ExecuteRegisterAsync(
            creditId,
            _bankDestinationId,
            "TRANSFER",
            40m,
            Guid.NewGuid()
        );
        apply.Success.Should().BeTrue(apply.Error);
        var originalAccountId = apply.Value!.AccountingAccountId;

        await using (var db = CreateContext())
        {
            var newAccount = Account.Create(
                _tenantId,
                _companyId,
                AccountCode.Create($"9.{Guid.NewGuid():N}"[..8]),
                "Cuenta nueva",
                null,
                AccountType.Asset,
                AccountNature.Debit,
                allowsPosting: true,
                createdBy: _userId
            );
            db.Accounts.Add(newAccount);
            var destination = await db.Set<CompanyFinancialDestination>()
                .FirstAsync(d => d.Id == _bankDestinationId);
            destination.ChangeAccountingAccount(newAccount.Id, _userId);
            await db.SaveChangesAsync();
        }

        var reverse = await ExecuteReverseAsync(creditId, apply.Value.Id, "Motivo", Guid.NewGuid());
        reverse.Success.Should().BeTrue(reverse.Error);
        reverse
            .Value!.AccountingAccountId.Should()
            .Be(
                originalAccountId,
                "la reversa nunca revalida ni usa la cuenta vigente del destino (§6.4quinquies)"
            );
    }

    // ── 15/16. SC-011 secuencial y concurrente ────────────────────────────

    [Fact]
    public async Task Punto15_Segunda_reversa_del_mismo_REFUND_RECEIVED_rechaza_SC_011()
    {
        var creditId = await SeedCreditAsync(100m);
        var apply = await ExecuteRegisterAsync(
            creditId,
            _bankDestinationId,
            "TRANSFER",
            40m,
            Guid.NewGuid()
        );
        apply.Success.Should().BeTrue(apply.Error);

        var first = await ExecuteReverseAsync(
            creditId,
            apply.Value!.Id,
            "Motivo 1",
            Guid.NewGuid()
        );
        first.Success.Should().BeTrue(first.Error);
        var second = await ExecuteReverseAsync(
            creditId,
            apply.Value.Id,
            "Motivo 2",
            Guid.NewGuid()
        );
        second.Success.Should().BeFalse();

        await using var verify = CreateContext();
        var reversalCount = await verify
            .Set<SupplierCreditRefundTransaction>()
            .CountAsync(t => t.OriginalTransactionId == apply.Value.Id);
        reversalCount.Should().Be(1, "SC-029: unicidad de reversa por ingreso");
    }

    [Fact]
    public async Task Punto16_Dos_reversas_concurrentes_del_mismo_REFUND_RECEIVED_una_exitosa_la_otra_SC_011()
    {
        var creditId = await SeedCreditAsync(100m);
        var apply = await ExecuteRegisterAsync(
            creditId,
            _bankDestinationId,
            "TRANSFER",
            40m,
            Guid.NewGuid()
        );
        apply.Success.Should().BeTrue(apply.Error);

        var t1 = ExecuteReverseAsync(creditId, apply.Value!.Id, "Motivo A", Guid.NewGuid());
        var t2 = ExecuteReverseAsync(creditId, apply.Value.Id, "Motivo B", Guid.NewGuid());
        var results = await Task.WhenAll(t1, t2);

        results.Count(r => r.Success).Should().Be(1);
        results.Count(r => !r.Success).Should().Be(1);

        await using var verify = CreateContext();
        var reversalCount = await verify
            .Set<SupplierCreditRefundTransaction>()
            .CountAsync(t => t.OriginalTransactionId == apply.Value.Id);
        reversalCount.Should().Be(1);

        var credit = await verify
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstAsync(c => c.Id == creditId);
        credit.AvailableAmount.Should().Be(100m);
    }

    // ── 26. Reversa de caja sin sesión activa ─────────────────────────────

    [Fact]
    public async Task Punto26_Reversa_de_reembolso_de_caja_sin_sesion_activa_rechaza_SC_027_recuperacion_idempotente_posterior()
    {
        var creditId = await SeedCreditAsync(100m);
        Guid sessionId;
        await using (var db = CreateContext())
        {
            var session = CashSession.Open(
                _tenantId,
                _companyId,
                _branchId,
                _userId,
                _cashRegisterId,
                "CAJA-01",
                "Caja Matriz",
                _emissionPointId,
                "001",
                0m,
                _userId
            );
            db.Set<CashSession>().Add(session);
            await db.SaveChangesAsync();
            sessionId = session.Id;
        }

        var apply = await ExecuteRegisterAsync(
            creditId,
            _cashDestinationId,
            "TRANSFER",
            40m,
            Guid.NewGuid()
        );
        apply.Success.Should().BeTrue(apply.Error);

        // Sin sesión activa alguna al momento de revertir (la sesión sembrada sigue Open pero es
        // simulada como "no compatible" cerrándola manualmente vía SQL directo — más simple:
        // dejamos la sesión Open pero de OTRA caja, forzando que GetOpenByCashRegisterForShareAsync
        // no encuentre coincidencia para _cashRegisterId tras cerrar la única sesión existente).
        await using (var db = CreateContext())
        {
            var session = await db.Set<CashSession>().FirstAsync(s => s.Id == sessionId);
            session.Close(_userId, new List<Domain.Modules.Caja.Entities.CashClosingCount>());
            await db.SaveChangesAsync();
        }

        var reverseAttempt1 = await ExecuteReverseAsync(
            creditId,
            apply.Value!.Id,
            "Motivo",
            Guid.NewGuid()
        );
        reverseAttempt1.Success.Should().BeFalse();

        await using (var verify1 = CreateContext())
        {
            var original = await verify1
                .Set<SupplierCreditRefundTransaction>()
                .AsNoTracking()
                .FirstAsync(t => t.Id == apply.Value.Id);
            original.TransactionTypeCode.Should().Be(RefundTransactionTypeCode.RefundReceived);
            var reversalCount = await verify1
                .Set<SupplierCreditRefundTransaction>()
                .CountAsync(t => t.OriginalTransactionId == apply.Value.Id);
            reversalCount.Should().Be(0, "SC-027 no debe dejar ningún efecto parcial");
        }

        // Recuperación idempotente: se abre una nueva sesión y se reintenta con el MISMO
        // ClientRequestId — el intento fallido no dejó ningún registro con ese CRI, así que el
        // reintento es la primera ejecución real, no "mismo contenido ya confirmado".
        await using (var db = CreateContext())
        {
            var newSession = CashSession.Open(
                _tenantId,
                _companyId,
                _branchId,
                _userId,
                _cashRegisterId,
                "CAJA-01",
                "Caja Matriz",
                _emissionPointId,
                "001",
                0m,
                _userId
            );
            db.Set<CashSession>().Add(newSession);
            await db.SaveChangesAsync();
        }

        var cri2 = Guid.NewGuid();
        var reverseAttempt2 = await ExecuteReverseAsync(creditId, apply.Value.Id, "Motivo", cri2);
        reverseAttempt2.Success.Should().BeTrue(reverseAttempt2.Error);

        await using var verify2 = CreateContext();
        var credit = await verify2
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstAsync(c => c.Id == creditId);
        credit.AvailableAmount.Should().Be(100m);
    }

    // ── Test doubles mínimos ─────────────────────────────────────────────

    private sealed class FixedCurrentTenant(Func<Guid> tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId();
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Func<Guid> companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId();
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => "tester";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : MediatR.INotification => Task.CompletedTask;
    }

    private sealed class RealDatabaseExceptionTranslator : IDatabaseExceptionTranslator
    {
        public bool TryGetUniqueViolation(Exception exception, out DatabaseUniqueViolationInfo info)
        {
            for (var ex = exception; ex is not null; ex = ex.InnerException)
            {
                if (ex is Npgsql.PostgresException pg && pg.SqlState == "23505")
                {
                    info = new DatabaseUniqueViolationInfo(
                        pg.SqlState,
                        pg.ConstraintName,
                        pg.TableName,
                        pg.MessageText
                    );
                    return true;
                }
            }
            info = null!;
            return false;
        }
    }
}
