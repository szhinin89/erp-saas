using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Items;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Persistence.Repositories.Retentions;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Tests.Audit;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// ZH-PURCHASE-RETENTION-CONFIRM-01 — la retención de una compra se emite DENTRO de su confirmación
/// (<c>RETENTIONS-MODULE-DESIGN-01</c> decisión 15): compra + inventario + CxP con la retención
/// aplicada + RetentionDocument + asientos de compra y de retención en un único SaveChanges.
/// <see cref="ConfirmPurchaseHandler"/> real (repositorios, <see cref="RetentionIssuer"/>,
/// <see cref="RetentionEligibilityService"/>, secuencia "07", Posting Engine y traductores reales vía
/// MediatR) contra PostgreSQL real; todo se verifica desde otro DbContext. Solo se simulan las
/// dependencias ajenas a la retención: resolución de tarifas SRI, precios y preferencias operativas.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed partial class PurchaseRetentionConfirmIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_retention_confirm_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private static readonly DateOnly IssueDate = new(2026, 9, 17);
    private const string RetentionVatCode = "725QA";

    private readonly Guid _userId = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _otherBranchId;
    private Guid _supplierId;
    private Guid _paymentTermId;
    private Guid _warehouseId;
    private Guid _itemId;
    private Guid _emissionPointId;
    private int _invoiceSeq;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("RETCONF", $"retconf-{Guid.NewGuid():N}"[..16], _userId);
        // WithholdsVat/WithholdsRenta = true por defecto: empresa agente de retención.
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Retenedora S.A.", createdBy: _userId);
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;

        var branch = NewBranch("Matriz", "001", isMain: true);
        var otherBranch = NewBranch("Sucursal 2", "002", isMain: false);
        db.Branches.AddRange(branch, otherBranch);
        var paymentTerm = PaymentTerm.Create(_tenantId, "RC-CONT", "Contado", 1, 0, _userId);
        db.PaymentTerms.Add(paymentTerm);
        await db.SaveChangesAsync();
        _branchId = branch.Id;
        _otherBranchId = otherBranch.Id;
        _paymentTermId = paymentTerm.Id;

        var warehouse = Warehouse.Create(
            _tenantId, _branchId, "Bodega Principal", "BOD-01",
            null, null, null, null, null, null, null, null, null, _userId, _companyId, isMain: true
        );
        db.Warehouses.Add(warehouse);

        var supplier = BusinessPartner.Create(_tenantId, TaxIdentification.SriRuc, "1791352688001", 2, "Proveedor Retenido", _userId);
        db.BusinessPartners.Add(supplier);
        await db.SaveChangesAsync();
        _warehouseId = warehouse.Id;
        _supplierId = supplier.Id;

        db.BusinessPartnerRoles.Add(
            BusinessPartnerRole.Create(_tenantId, supplier.Id, RoleType.Supplier, _userId,
                supplierConfig: SupplierRoleConfig.Create(isRetentionExempt: false))
        );
        var vatRetentionCode = new SriRetentionCode
        {
            Id = Guid.NewGuid(),
            TaxType = "IVA",
            Code = RetentionVatCode,
            Name = "Retención IVA 30% (fixture de prueba)",
            Percentage = 30m,
            AppliesTo = "SUPPLIER",
            IsActive = true,
        };
        db.SriRetentionCodes.Add(vatRetentionCode);
        await db.SaveChangesAsync();
        db.SupplierRetentionDefaults.Add(
            SupplierRetentionDefault.Create(_tenantId, _companyId, supplier.Id, vatRetentionCode.Id, 0, _userId)
        );

        var establishment = Establishment.Create(_tenantId, null, _companyId, "001", "Matriz", "Av. 1", null, isMain: true, _userId);
        db.Establishments.Add(establishment);
        await db.SaveChangesAsync();
        var emissionPoint = EmissionPoint.Create(_tenantId, _companyId, establishment.Id, "001", "Punto 1", EmissionType.Electronic, isDefault: true, _userId);
        db.EmissionPoints.Add(emissionPoint);

        var itemType = ItemTypeDefinition.Create(_tenantId, "MERCH", "Mercadería", 1, _userId);
        db.Set<ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();
        _emissionPointId = emissionPoint.Id;
        var item = Item.Create(
            _tenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Retención",
            description: "Producto Retención",
            itemTypeId: itemType.Id,
            defaultUomCode: "UNIT",
            taxConfig: ItemTaxConfig.Create(saleVatCode: "4", purchaseVatCode: "4"),
            saleConfig: ItemSaleConfig.Create(isForSale: true),
            stockConfig: ItemStockConfig.Create(tracksStock: true),
            createdBy: _userId
        );
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();
        _itemId = item.Id;

        // Plan de cuentas, período anual y MinimalPostingRules oficiales (incluye
        // Purchases/InvoiceReceived y Retentions/DocumentIssued).
        await new AccountingBootstrapStep(db, new ERP.Infrastructure.Tests.Seeding.AlwaysTodayCompanyClock(), NullLogger<AccountingBootstrapStep>.Instance)
            .ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _userId));
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private Branch NewBranch(string name, string code, bool isMain) =>
        Branch.Create(
            _tenantId, name, "Av. Principal 123", code,
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,
            isMain, _userId, companyId: _companyId
        );

    private ErpDbContext CreateContext(IPublisher? publisher = null) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(_tenantId),
            publisher ?? new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );

    /// <summary>DbContext con el fan-out real de producción (AddMediatR por escaneo de ensamblado): traductores de posting de compra/retención y auditoría.</summary>
    private ErpDbContext CreateWiredContext()
    {
        var deferred = new DeferredPublisher();
        var db = new ErpDbContext(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString() + ";Include Error Detail=true")
                .AddInterceptors(new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(_tenantId),
            deferred,
            new FixedCurrentCompany(_companyId)
        );
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddScoped<ICompanyClock, CompanyClock>();
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(_tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(_companyId));
        services.AddSingleton<ICurrentBranch>(new FixedCurrentBranch(_branchId));
        services.AddSingleton<ICurrentUser>(new FixedCurrentUser(_userId));
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(() => _tenantId, () => _companyId, _userId));
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(PurchaseInvoiceConfirmedPostingTranslator).Assembly));
        deferred.Inner = services.BuildServiceProvider().GetRequiredService<IPublisher>();
        return db;
    }

    private RetentionEligibilityService Eligibility(ErpDbContext db) =>
        new(new CompanyRepository(db), new BusinessPartnerRoleRepository(db), new SupplierRetentionDefaultRepository(db), new RetentionCodeResolver(db));

    private ConfirmPurchaseHandler ConfirmHandler(ErpDbContext db, Guid? branchId = null)
    {
        var company = new FixedCurrentCompany(_companyId);
        var tax = new Mock<ERP.Application.Modules.Purchases.Services.ISriTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));
        var pricing = new Mock<IPricingResolver>();
        pricing.Setup(p => p.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PricingResult>.NotFound("Sin precio"));
        var preferences = new Mock<IOperationalPreferencesResolver>();
        preferences.Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Preferences());
        var xmlGuard = new Mock<IPurchaseXmlConfirmationGuard>();
        xmlGuard.Setup(g => g.ValidateAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var postingEngine = new PostingEngine(
            new JournalEntryRepository(db), new PostingRuleRepository(db), new AccountingPeriodRepository(db),
            new JournalEntrySequenceRepository(db), new AccountRepository(db), NullLogger<PostingEngine>.Instance
        );
        return new ConfirmPurchaseHandler(
            new PurchaseInvoiceRepository(db, company),
            new StockRepository(db, company, new PostgresDatabaseExceptionTranslator(), StandardPrecisionPolicyProvider.Instance),
            new ItemRepository(db),
            new WarehouseRepository(db, company),
            new PaymentTermRepository(db),
            tax.Object,
            postingEngine,
            pricing.Object,
            new AccountsPayableService(new AccountsPayableRepository(db)),
            NullLogger<ConfirmPurchaseHandler>.Instance,
            new FixedCurrentTenant(_tenantId),
            company,
            new FixedCurrentBranch(branchId ?? _branchId),
            new FixedCurrentUser(_userId),
            preferences.Object,
            StandardPrecisionPolicyProvider.Instance,
            xmlGuard.Object,
            new RetentionIssuer(
                new RetentionDocumentRepository(db, company),
                Eligibility(db),
                new EmissionPointRepository(db),
                new EstablishmentRepository(db),
                new DocumentSequenceRepository(db)
            )
        );
    }

    private CalculateRetentionHandler PreviewHandler(ErpDbContext db) =>
        new(
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(_companyId)),
            Eligibility(db),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId),
            new FixedCurrentBranch(_branchId)
        );

    private CancelPurchaseHandler CancelHandler(ErpDbContext db, Guid? branchId = null)
    {
        var company = new FixedCurrentCompany(_companyId);
        return new CancelPurchaseHandler(
            new PurchaseInvoiceRepository(db, company),
            new AccountsPayableRepository(db),
            new StockRepository(db, company, new PostgresDatabaseExceptionTranslator(), StandardPrecisionPolicyProvider.Instance),
            new PurchaseReturnRepository(db, company),
            new RetentionDocumentRepository(db, company),
            new RetentionCanceller(new AccountsPayableRepository(db)),
            new UnitOfWork(db),
            NullLogger<CancelPurchaseHandler>.Instance,
            new FixedCurrentTenant(_tenantId),
            company,
            new FixedCurrentBranch(branchId ?? _branchId),
            new FixedCurrentUser(_userId),
            new CompanyClock(db)
        );
    }

    private static OperationalPreferences Preferences() =>
        new(
            SalesPos: new SalesPosPreferences(true, false, true, 0m, null, false, false, null, null),
            Cash: new CashPreferences(true, true, 0m, true, true, true),
            Purchases: new PurchasesPreferences(null, true, true, true, false),
            Inventory: new InventoryPreferences(false, true, false, 0m),
            Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
            ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
            Notifications: new NotificationsPreferences(true, false, "es")
        );

    /// <summary>Compra en borrador: 1 × 100 + IVA 15% = 115 (base retenible IVA 15, Renta 100).</summary>
    private async Task<Guid> SeedDraftPurchaseAsync(Guid? branchId = null)
    {
        await using var db = CreateContext();
        var number = $"001-001-{++_invoiceSeq:000000000}";
        var invoice = PurchaseInvoice.CreateDraft(
            _tenantId, _companyId, branchId ?? _branchId, _supplierId, "Proveedor Retenido", "1791352688001",
            "01", number, IssueDate, _userId, _paymentTermId, "Contado", 1, 0
        );
        var line = PurchaseInvoiceDetail.Create(invoice.Id, _tenantId, "Producto Retención", 1m, 100m, "4", "UNIT",
            itemId: _itemId, warehouseId: _warehouseId);
        line.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);
        invoice.ReplaceLines(new[] { line }, _userId);
        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private RetentionIntent VatIntent(decimal retained = 4.5m, Guid? emissionPointId = null) =>
        new(
            true,
            emissionPointId ?? _emissionPointId,
            IssueDate,
            new[] { new IssueRetentionLineInput(RetentionTaxType.Vat, RetentionVatCode, 15m, 30m, retained, "Ret. IVA 30%") }
        );

    private async Task<Result<PurchaseInvoiceDto>> ConfirmAsync(Guid invoiceId, RetentionIntent? intent, Guid? branchId = null)
    {
        await using var db = CreateWiredContext();
        return await ConfirmHandler(db, branchId).Handle(new ConfirmPurchaseCommand(invoiceId, null, intent), CancellationToken.None);
    }

    private sealed record Snapshot(
        PurchaseStatus Status,
        List<AccountsPayable> Payables,
        List<RetentionDocument> Retentions,
        int PurchaseEntries,
        int RetentionEntries,
        int StockMovements
    );

    private async Task<Snapshot> ReadAsync(Guid invoiceId)
    {
        await using var db = CreateContext();
        var invoice = await db.PurchaseInvoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        var payables = await db.Set<AccountsPayable>().AsNoTracking().Include(p => p.Installments)
            .Where(p => p.OriginId == invoiceId).ToListAsync();
        var retentions = await db.Set<RetentionDocument>().AsNoTracking().Include(r => r.Lines)
            .Where(r => r.SourceDocumentType == RetentionSourceDocumentType.PurchaseInvoice && r.SourceDocumentId == invoiceId)
            .ToListAsync();
        var retentionIds = retentions.Select(r => r.Id).ToList();
        var purchaseEntries = await db.JournalEntries.AsNoTracking()
            .CountAsync(e => e.SourceModule == "Purchases" && e.SourceEventType == "InvoiceReceived" && e.SourceEventId == invoiceId);
        var retentionEntries = await db.JournalEntries.AsNoTracking()
            .CountAsync(e => e.SourceModule == "Retentions" && e.SourceEventType == "DocumentIssued" && retentionIds.Contains(e.SourceEventId));
        var movements = await db.Set<StockMovement>().AsNoTracking().CountAsync(m => m.SourceDocId == invoiceId);
        return new Snapshot(invoice.Status, payables, retentions, purchaseEntries, retentionEntries, movements);
    }

    private static void ShouldBeUntouchedDraft(Snapshot s)
    {
        s.Status.Should().Be(PurchaseStatus.Draft);
        s.Payables.Should().BeEmpty();
        s.Retentions.Should().BeEmpty();
        s.PurchaseEntries.Should().Be(0);
        s.RetentionEntries.Should().Be(0);
        s.StockMovements.Should().Be(0);
    }

    // ── 1. Sin intención: confirmación de siempre ─────────────────────────

    [Fact]
    public async Task Borrador_sin_intencion_confirma_igual_que_antes_sin_retencion()
    {
        var invoiceId = await SeedDraftPurchaseAsync();

        var result = await ConfirmAsync(invoiceId, null);

        result.IsSuccess.Should().BeTrue(result.Error);
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Confirmed);
        var payable = s.Payables.Should().ContainSingle().Which;
        payable.TotalAmount.Should().Be(115m);
        payable.RetainedAmount.Should().Be(0m);
        s.Retentions.Should().BeEmpty();
        s.PurchaseEntries.Should().Be(1);
        s.RetentionEntries.Should().Be(0);
        s.StockMovements.Should().Be(1);
    }

    // ── 2. Elegible + intención: todo en una sola transacción ─────────────

    [Fact]
    public async Task Borrador_elegible_con_intencion_confirma_compra_CxP_neta_retencion_y_asientos_atomicamente()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        await using (var db = CreateContext())
        {
            var preview = await PreviewHandler(db).Handle(new CalculateRetentionQuery(invoiceId), CancellationToken.None);
            preview.IsSuccess.Should().BeTrue(preview.Error);
            var line = preview.Value!.Lines.Should().ContainSingle().Which;
            (line.RetentionCode, line.TaxableBase, line.RetentionPct, line.AmountRetained).Should().Be((RetentionVatCode, 15m, 30m, 4.5m));
        }

        var result = await ConfirmAsync(invoiceId, VatIntent());

        result.IsSuccess.Should().BeTrue(result.Error);
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Confirmed);
        var retention = s.Retentions.Should().ContainSingle().Which;
        retention.Status.Should().Be(RetentionStatus.Issued);
        retention.RetentionNumber.Should().Be("001-001-000000001");
        retention.TotalRetained.Should().Be(4.5m);
        retention.SourceDocumentNumber.Should().Be(result.Value!.InvoiceNumber);
        var payable = s.Payables.Should().ContainSingle().Which;
        payable.TotalAmount.Should().Be(115m);
        payable.RetainedAmount.Should().Be(4.5m);
        (payable.TotalAmount - payable.RetainedAmount).Should().Be(110.5m, "CxP original − retención = saldo exigible");
        s.PurchaseEntries.Should().Be(1);
        s.RetentionEntries.Should().Be(1);
        s.StockMovements.Should().Be(1);

        await using var verify = CreateContext();
        var retentionEntry = await verify.JournalEntries.AsNoTracking().Include(e => e.Lines)
            .SingleAsync(e => e.SourceModule == "Retentions" && e.SourceEventId == retention.Id);
        retentionEntry.Lines.Sum(l => l.Debit).Should().Be(4.5m);
        retentionEntry.Lines.Sum(l => l.Credit).Should().Be(4.5m);
    }

    // ── 3. Empresa no agente: vista previa y emisión coherentes ───────────

    [Fact]
    public async Task Empresa_no_agente_de_retencion_la_vista_previa_no_propone_y_la_confirmacion_con_intencion_se_rechaza()
    {
        await using (var db = CreateContext())
        {
            var company = await db.Companies.SingleAsync(c => c.Id == _companyId);
            company.WithholdsVat = false;
            company.WithholdsRenta = false;
            await db.SaveChangesAsync();
        }
        var invoiceId = await SeedDraftPurchaseAsync();

        string? skipReason;
        await using (var db = CreateContext())
        {
            var preview = await PreviewHandler(db).Handle(new CalculateRetentionQuery(invoiceId), CancellationToken.None);
            preview.IsSuccess.Should().BeTrue(preview.Error);
            preview.Value!.Lines.Should().BeEmpty();
            skipReason = preview.Value.SkipReason;
        }
        skipReason.Should().Contain("agente de retención de IVA");

        var result = await ConfirmAsync(invoiceId, VatIntent());

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Contain("agente de retención de IVA", "la emisión rechaza con la misma regla que mostró la vista previa");
        ShouldBeUntouchedDraft(await ReadAsync(invoiceId));
    }

    // ── 4. Intención inválida en el servidor: la compra sigue Draft ───────

    [Fact]
    public async Task Intencion_con_impuesto_no_elegible_se_rechaza_y_la_compra_sigue_en_borrador()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        var incomeIntent = new RetentionIntent(true, _emissionPointId, IssueDate,
            new[] { new IssueRetentionLineInput(RetentionTaxType.Income, "303", 100m, 10m, 10m) });

        var result = await ConfirmAsync(invoiceId, incomeIntent);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        ShouldBeUntouchedDraft(await ReadAsync(invoiceId));
    }

    [Fact]
    public async Task Punto_de_emision_inexistente_falla_cerrado_y_la_compra_sigue_en_borrador()
    {
        var invoiceId = await SeedDraftPurchaseAsync();

        var result = await ConfirmAsync(invoiceId, VatIntent(emissionPointId: Guid.NewGuid()));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        ShouldBeUntouchedDraft(await ReadAsync(invoiceId));
    }

    [Fact]
    public async Task Retencion_mayor_al_saldo_de_la_CxP_se_rechaza_y_la_compra_sigue_en_borrador()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        var oversized = new RetentionIntent(true, _emissionPointId, IssueDate, new[]
        {
            new IssueRetentionLineInput(RetentionTaxType.Vat, RetentionVatCode, 200m, 100m, 200m),
        });

        var result = await ConfirmAsync(invoiceId, oversized);

        result.IsSuccess.Should().BeFalse();
        ShouldBeUntouchedDraft(await ReadAsync(invoiceId));
    }

    // ── 5. Fallo del asiento de la retención: rollback completo ───────────

    [Fact]
    public async Task Fallo_del_asiento_de_la_retencion_revierte_compra_inventario_CxP_retencion_y_asiento_de_compra()
    {
        await using (var db = CreateContext())
        {
            var rule = await db.PostingRules.SingleAsync(r =>
                r.CompanyId == _companyId && r.SourceModule == "Retentions" && r.FactType == "DocumentIssued");
            rule.Disable(_userId);
            await db.SaveChangesAsync();
        }
        var invoiceId = await SeedDraftPurchaseAsync();

        var result = await ConfirmAsync(invoiceId, VatIntent());

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("RULE_NOT_FOUND", "falló el asiento de la retención, no otro paso");
        ShouldBeUntouchedDraft(await ReadAsync(invoiceId));
    }

    // ── 6/7. Concurrencia, reintento y 1:1 ────────────────────────────────

    private async Task<(bool Success, string? Error)> TryConfirmAsync(Guid invoiceId, RetentionIntent? intent)
    {
        try
        {
            var result = await ConfirmAsync(invoiceId, intent);
            return (result.IsSuccess, result.Error);
        }
        catch (DbUpdateException ex)
        {
            // Perdedor de la carrera: concurrencia optimista (xmin de la compra) o índice único
            // (CxP / retención por origen). La API lo traduce a 409.
            return (false, ex.GetType().Name);
        }
    }

    [Fact]
    public async Task Doble_confirmacion_concurrente_con_intencion_produce_un_solo_conjunto_de_efectos()
    {
        var invoiceId = await SeedDraftPurchaseAsync();

        var results = await Task.WhenAll(TryConfirmAsync(invoiceId, VatIntent()), TryConfirmAsync(invoiceId, VatIntent()));

        results.Count(r => r.Success).Should().Be(1, string.Join(" | ", results.Select(r => r.Error)));
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Confirmed);
        s.Payables.Should().ContainSingle().Which.RetainedAmount.Should().Be(4.5m, "la retención se aplica una sola vez");
        s.Retentions.Should().ContainSingle().Which.Status.Should().Be(RetentionStatus.Issued);
        s.PurchaseEntries.Should().Be(1);
        s.RetentionEntries.Should().Be(1);
        s.StockMovements.Should().Be(1);
    }

    [Fact]
    public async Task Reintento_de_la_confirmacion_ya_aplicada_no_duplica_la_retencion()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        (await ConfirmAsync(invoiceId, VatIntent())).IsSuccess.Should().BeTrue();

        var retry = await ConfirmAsync(invoiceId, VatIntent());

        retry.IsSuccess.Should().BeFalse();
        retry.Error.Should().Be("Esta compra ya fue confirmada.");
        var s = await ReadAsync(invoiceId);
        s.Retentions.Should().ContainSingle();
        s.Payables.Should().ContainSingle().Which.RetainedAmount.Should().Be(4.5m);
        s.RetentionEntries.Should().Be(1);
    }

    [Fact]
    public async Task Intenciones_incompatibles_concurrentes_solo_aplican_una_transicion_coherente()
    {
        var invoiceId = await SeedDraftPurchaseAsync();

        var results = await Task.WhenAll(TryConfirmAsync(invoiceId, VatIntent()), TryConfirmAsync(invoiceId, null));

        results.Count(r => r.Success).Should().Be(1, string.Join(" | ", results.Select(r => r.Error)));
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Confirmed);
        var payable = s.Payables.Should().ContainSingle().Which;
        var active = s.Retentions.Where(r => r.Status != RetentionStatus.Cancelled).ToList();
        active.Should().HaveCountLessThanOrEqualTo(1);
        payable.RetainedAmount.Should().Be(active.Sum(r => r.TotalRetained), "la CxP refleja exactamente la transición ganadora");
        s.RetentionEntries.Should().Be(active.Count);
        s.PurchaseEntries.Should().Be(1);
    }

    // ── 8. Anulación: cascada actual sin cambios ──────────────────────────

    [Fact]
    public async Task Anular_compra_confirmada_con_retencion_integrada_anula_retencion_y_CxP_y_reversa_asientos()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        (await ConfirmAsync(invoiceId, VatIntent())).IsSuccess.Should().BeTrue();

        Result<PurchaseInvoiceDto> cancel;
        await using (var db = CreateWiredContext())
            cancel = await CancelHandler(db).Handle(new CancelPurchaseCommand(invoiceId, "Error de digitación"), CancellationToken.None);

        cancel.IsSuccess.Should().BeTrue(cancel.Error);
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Cancelled);
        var retention = s.Retentions.Should().ContainSingle().Which;
        retention.Status.Should().Be(RetentionStatus.Cancelled);
        var payable = s.Payables.Should().ContainSingle().Which;
        payable.Status.Should().Be(AccountsPayableStatus.Cancelled);
        payable.RetainedAmount.Should().Be(0m, "RetentionCanceller revierte ApplyRetention");

        await using var verify = CreateContext();
        var retentionEntry = await verify.JournalEntries.AsNoTracking()
            .SingleAsync(e => e.SourceModule == "Retentions" && e.SourceEventType == "DocumentIssued" && e.SourceEventId == retention.Id);
        retentionEntry.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus.Reversed);
        retentionEntry.ReverseJournalEntryId.Should().NotBeNull();
        var reverse = await verify.JournalEntries.AsNoTracking().Include(e => e.Lines)
            .SingleAsync(e => e.Id == retentionEntry.ReverseJournalEntryId);
        reverse.Lines.Sum(l => l.Debit).Should().Be(4.5m);
        reverse.Lines.Sum(l => l.Credit).Should().Be(4.5m);
    }

    // ── ZH-RETENTION-CANCELLATION-LIFECYCLE-01: la retención solo se anula con su origen ──

    private async Task<Result<PurchaseInvoiceDto>> CancelPurchaseAsync(Guid invoiceId, Guid? branchId = null)
    {
        await using var db = CreateWiredContext();
        var handler = CancelHandler(db, branchId);
        return await handler.Handle(new CancelPurchaseCommand(invoiceId, "Error de digitación"), CancellationToken.None);
    }

    private async Task<(bool Success, string? Error)> TryCancelPurchaseAsync(Guid invoiceId)
    {
        try
        {
            var result = await CancelPurchaseAsync(invoiceId);
            return (result.IsSuccess, result.Error);
        }
        catch (DbUpdateException ex)
        {
            return (false, ex.GetType().Name);
        }
    }

    /// <summary>Reversos contables del asiento de emisión de la retención (debe ser exactamente uno tras anular).</summary>
    private async Task<(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus IssuedStatus, int Reversals)> RetentionAccountingAsync(Guid retentionId)
    {
        await using var db = CreateContext();
        var issued = await db.JournalEntries.AsNoTracking()
            .SingleAsync(e => e.SourceModule == "Retentions" && e.SourceEventType == "DocumentIssued" && e.SourceEventId == retentionId);
        var reversals = await db.JournalEntries.AsNoTracking()
            .CountAsync(e => e.SourceEventType == "Reversal" && e.SourceEventId == issued.Id);
        return (issued.Status, reversals);
    }

    [Fact]
    public async Task Retencion_anulada_con_su_compra_es_terminal_no_se_reemite_ni_se_registra_electronicamente()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        (await ConfirmAsync(invoiceId, VatIntent())).IsSuccess.Should().BeTrue();
        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue();
        var retentionId = (await ReadAsync(invoiceId)).Retentions.Single().Id;

        var reconfirm = await ConfirmAsync(invoiceId, VatIntent());
        Result<ERP.Application.Modules.ElectronicDocuments.DTOs.ElectronicDocumentDto> register;
        await using (var db = CreateContext())
            register = await new RegisterRetentionElectronicDocumentHandler(
                new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId)),
                Mock.Of<ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentIssuer>(),
                new FixedCurrentTenant(_tenantId),
                new FixedCurrentCompany(_companyId),
                new FixedCurrentUser(_userId)
            ).Handle(new RegisterRetentionElectronicDocumentCommand(retentionId), CancellationToken.None);

        reconfirm.IsSuccess.Should().BeFalse("la compra anulada no vuelve a confirmarse");
        register.IsSuccess.Should().BeFalse("una retención anulada no se registra ante el SRI");
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Cancelled);
        s.Retentions.Should().ContainSingle().Which.Status.Should().Be(RetentionStatus.Cancelled);
        s.Payables.Single().RetainedAmount.Should().Be(0m);
        var accounting = await RetentionAccountingAsync(retentionId);
        accounting.IssuedStatus.Should().Be(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus.Reversed);
        accounting.Reversals.Should().Be(1);
    }

    [Fact]
    public async Task Doble_anulacion_concurrente_de_la_compra_revierte_la_retencion_una_sola_vez()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        (await ConfirmAsync(invoiceId, VatIntent())).IsSuccess.Should().BeTrue();

        var results = await Task.WhenAll(TryCancelPurchaseAsync(invoiceId), TryCancelPurchaseAsync(invoiceId));

        results.Count(r => r.Success).Should().Be(1, string.Join(" | ", results.Select(r => r.Error)));
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Cancelled);
        var retention = s.Retentions.Should().ContainSingle().Which;
        retention.Status.Should().Be(RetentionStatus.Cancelled);
        var payable = s.Payables.Should().ContainSingle().Which;
        payable.RetainedAmount.Should().Be(0m, "ReverseRetention se aplica exactamente una vez");
        payable.Status.Should().Be(AccountsPayableStatus.Cancelled);
        (await RetentionAccountingAsync(retention.Id)).Reversals.Should().Be(1);
    }

    [Fact]
    public async Task Reintento_de_la_anulacion_no_vuelve_a_revertir_la_retencion()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        (await ConfirmAsync(invoiceId, VatIntent())).IsSuccess.Should().BeTrue();
        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue();

        var retry = await CancelPurchaseAsync(invoiceId);

        retry.IsSuccess.Should().BeFalse();
        retry.Error.Should().Be("Esta compra ya fue anulada.");
        var s = await ReadAsync(invoiceId);
        s.Payables.Single().RetainedAmount.Should().Be(0m);
        (await RetentionAccountingAsync(s.Retentions.Single().Id)).Reversals.Should().Be(1);
    }

    [Fact]
    public async Task Anular_desde_otra_sucursal_responde_NotFound_y_la_retencion_sigue_activa()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        (await ConfirmAsync(invoiceId, VatIntent())).IsSuccess.Should().BeTrue();

        var result = await CancelPurchaseAsync(invoiceId, branchId: _otherBranchId);

        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Confirmed);
        s.Retentions.Should().ContainSingle().Which.Status.Should().Be(RetentionStatus.Issued);
        s.Payables.Single().RetainedAmount.Should().Be(4.5m);
        (await RetentionAccountingAsync(s.Retentions.Single().Id)).Reversals.Should().Be(0);
    }

    /// <summary>
    /// Comportamiento VIGENTE, sin definición funcional SRI (ver ZH-RETENTION-CANCELLATION-LIFECYCLE-01
    /// § matriz SRI): anular la compra anula la retención en el ERP aunque su comprobante electrónico
    /// ya esté autorizado, y no toca el <c>ElectronicDocument</c> (no existe flujo de anulación ante
    /// el SRI). Si se define esa regla, este test debe cambiar con ella.
    /// </summary>
    [Fact]
    public async Task Vigente_anular_la_compra_con_retencion_autorizada_no_toca_el_documento_electronico()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        (await ConfirmAsync(invoiceId, VatIntent())).IsSuccess.Should().BeTrue();
        var retentionId = (await ReadAsync(invoiceId)).Retentions.Single().Id;
        Guid edocId;
        await using (var db = CreateContext())
        {
            var edoc = ERP.Domain.Modules.ElectronicDocuments.Entities.ElectronicDocument.Create(
                _tenantId, _companyId, ERP.Domain.Modules.ElectronicDocuments.Enums.ElectronicDocumentType.Retention,
                "Retentions", retentionId, _userId);
            db.ElectronicDocuments.Add(edoc);
            await db.SaveChangesAsync();
            edocId = edoc.Id;
            // Estado SRI simulado: el pipeline real (firma + SOAP) queda fuera de esta prueba.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE electronic_documents SET current_state = {(int)ERP.Domain.Modules.ElectronicDocuments.Enums.ElectronicDocumentState.Authorized} WHERE id = {edocId}");
        }

        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue();

        (await ReadAsync(invoiceId)).Retentions.Single().Status.Should().Be(RetentionStatus.Cancelled);
        await using var verify = CreateContext();
        (await verify.ElectronicDocuments.AsNoTracking().SingleAsync(e => e.Id == edocId)).CurrentState
            .Should().Be(ERP.Domain.Modules.ElectronicDocuments.Enums.ElectronicDocumentState.Authorized);
    }

    // ── 9. Fail-closed por sucursal ───────────────────────────────────────

    [Fact]
    public async Task Compra_de_otra_sucursal_responde_NotFound_sin_ningun_efecto()
    {
        var invoiceId = await SeedDraftPurchaseAsync(branchId: _otherBranchId);

        var result = await ConfirmAsync(invoiceId, VatIntent(), branchId: _branchId);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        ShouldBeUntouchedDraft(await ReadAsync(invoiceId));
    }

    // ── Test doubles ──────────────────────────────────────────────────────

    private sealed class DeferredPublisher : IPublisher
    {
        public IPublisher? Inner { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Inner!.Publish(notification, cancellationToken);

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Inner!.Publish(notification, cancellationToken);
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class FixedCurrentBranch(Guid branchId) : ICurrentBranch
    {
        public Guid BranchId => branchId;
        public bool IsAuthenticated => true;
        public bool HasBranchContext => branchId != Guid.Empty;
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
