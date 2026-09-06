using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.DocTypes.Services;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.DocTypes.Entities;
using ERP.Domain.Modules.DocTypes.Enums;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Constants;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Expenses;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Retentions;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Seeding.Steps;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Expenses;

/// <summary>
/// RETENTIONS-EXPENSES-E2E-QA-01G — suite de integración end-to-end (PostgreSQL 16 real vía
/// Testcontainers, mismo patrón que <c>SupplierPaymentEndToEndTests</c>/
/// <c>PurchaseInvoiceConfirmedPostingIntegrationTests</c>, sin mocks de EF Core) para el flujo
/// completo Gastos + Retenciones: ConfirmExpenseDocumentCommand (con RetentionIntent) →
/// ExpenseDocument.Confirm() + RetentionIssuer.IssueForExpenseAsync() + AccountsPayable.ApplyRetention()
/// → posting de ambos hechos contables (Expenses/DocumentConfirmed + Retentions/DocumentIssued) →
/// CancelExpenseDocumentCommand (reversa completa o bloqueo si hay pagos aplicados).
///
/// Todos los datos (empresa, proveedores, códigos de retención, cuenta de gasto operativo,
/// DocumentFlowPolicy) son fixtures mínimos creados y aislados dentro de este test — nunca seed
/// global de producción/desarrollo. Nombres/IDs se identifican explícitamente como datos de prueba.
///
/// TECH-DEBT-RETENTION-E2E-POSTING-SEED-CLEANUP-01: el Plan de Cuentas y las PostingRule de
/// "Expenses"/"DocumentConfirmed" y "Retentions"/"DocumentIssued" ya NO son fixture local — se
/// siembran vía <see cref="AccountingBootstrapStep"/> real (ver <see cref="SeedAccountingChartAsync"/>),
/// el mismo seed/backfill oficial que corre para toda Company nueva del ERP.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class RetentionExpenseEndToEndTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_retention_expense_e2e_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _createdBy;
    private Guid _paymentTermId;

    private Guid _supplierNonExemptId; // WithholdsVat/Renta OK, código retención IVA "701" activo
    private Guid _supplierExemptId; // IsRetentionExempt = true
    private Guid _supplierMissingCodeId; // no exento, pero código retención "999" no existe en catálogo

    private Guid _subcategoryId;
    private Guid _expenseAccountId;

    private Guid _emissionPointId;

    private const string RetentionVatCode = "701QA"; // fixture de prueba, no un código SRI real de producción
    private const string MissingRetentionVatCode = "999QA";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("RETQA Tenant", $"retqa-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "RETQA Empresa Retenedora S.A.",
            createdBy: _createdBy
        );
        // WithholdsVat/WithholdsRenta ya vienen en true por defecto (Company.cs) — empresa agente
        // de retención sin necesidad de mutarlos aquí (ver RETENTIONS-MODULE-DESIGN-01.md).
        var branch = Branch.Create(
            tenant.Id,
            "Matriz QA",
            "Av. Retenciones 123",
            "001",
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
            createdBy: _createdBy,
            companyId: company.Id
        );

        var paymentTerm = PaymentTerm.Create(tenant.Id, "RETQA-CONTADO", "Contado QA", 1, 0, _createdBy);

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.PaymentTerms.Add(paymentTerm);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _paymentTermId = paymentTerm.Id;

        // ── Proveedores de prueba ──────────────────────────────────────────
        var supplierNonExempt = BusinessPartner.Create(tenant.Id, "05", "1710034065", 1, "RETQA Proveedor No Exento", _createdBy);
        var supplierExempt = BusinessPartner.Create(tenant.Id, "05", "1710034073", 1, "RETQA Proveedor Exento", _createdBy);
        var supplierMissingCode = BusinessPartner.Create(tenant.Id, "05", "1710034081", 1, "RETQA Proveedor Codigo Faltante", _createdBy);
        db.BusinessPartners.AddRange(supplierNonExempt, supplierExempt, supplierMissingCode);
        await db.SaveChangesAsync();

        var roleNonExempt = BusinessPartnerRole.Create(
            tenant.Id,
            supplierNonExempt.Id,
            RoleType.Supplier,
            _createdBy,
            supplierConfig: SupplierRoleConfig.Create(
                paymentTerm.Id,
                defaultRetentionVatCode: RetentionVatCode,
                isRetentionExempt: false
            )
        );
        var roleExempt = BusinessPartnerRole.Create(
            tenant.Id,
            supplierExempt.Id,
            RoleType.Supplier,
            _createdBy,
            supplierConfig: SupplierRoleConfig.Create(
                paymentTerm.Id,
                defaultRetentionVatCode: RetentionVatCode,
                isRetentionExempt: true
            )
        );
        var roleMissingCode = BusinessPartnerRole.Create(
            tenant.Id,
            supplierMissingCode.Id,
            RoleType.Supplier,
            _createdBy,
            supplierConfig: SupplierRoleConfig.Create(
                paymentTerm.Id,
                defaultRetentionVatCode: MissingRetentionVatCode,
                isRetentionExempt: false
            )
        );
        db.BusinessPartnerRoles.AddRange(roleNonExempt, roleExempt, roleMissingCode);
        await db.SaveChangesAsync();

        _supplierNonExemptId = supplierNonExempt.Id;
        _supplierExemptId = supplierExempt.Id;
        _supplierMissingCodeId = supplierMissingCode.Id;

        // ── Código de retención IVA activo en catálogo SRI (fixture, 70%) ──────
        db.SriRetentionCodes.Add(
            new SriRetentionCode
            {
                Id = Guid.NewGuid(),
                TaxType = "IVA",
                Code = RetentionVatCode,
                Name = "RETQA IVA 70% (fixture de prueba)",
                Percentage = 70m,
                AppliesTo = "SUPPLIER",
                IsActive = true,
            }
        );
        await db.SaveChangesAsync();
        // Deliberadamente NO se siembra un SriRetentionCode para MissingRetentionVatCode — ese es
        // precisamente el gap que el escenario 7 (MissingRetentionCode) necesita.

        // TECH-DEBT-RETENTION-E2E-POSTING-SEED-CLEANUP-01: el Plan de Cuentas canónico (incluidas
        // las cuentas fijas que "Expenses"/"DocumentConfirmed" y "Retentions"/"DocumentIssued"
        // referencian — 1.1.05.001/2.1.01.001/2.1.02.002) y las PostingRule correspondientes ya no
        // son un fixture local: se siembran vía AccountingBootstrapStep real (ver
        // SeedAccountingChartAsync), el mismo mecanismo que corre para toda Company nueva del ERP.
        // Solo queda como fixture local la cuenta de gasto operativo (Debe dinámico por
        // categoría/subcategoría vía PostingFact.Allocations, no representable como PostingRuleLine
        // fija — mismo criterio documentado en AccountingBootstrapStep/MinimalPostingRules).
        var expenseAccount = Account.Create(
            tenant.Id, company.Id, AccountCode.Create($"5.1.{Guid.NewGuid():N}"[..8]),
            "RETQA Gasto Operativo", null, AccountType.Expense, AccountNature.Debit,
            allowsPosting: true, createdBy: _createdBy
        );
        db.Accounts.Add(expenseAccount);
        await db.SaveChangesAsync();

        _expenseAccountId = expenseAccount.Id;

        var type = ExpenseCategoryNode.CreateType(tenant.Id, company.Id, "RETQA-TIPO", "RETQA Tipo Gasto", _createdBy);
        db.ExpenseCategoryNodes.Add(type);
        await db.SaveChangesAsync();
        var category = ExpenseCategoryNode.CreateCategory(tenant.Id, company.Id, type, "RETQA-CAT", "RETQA Categoria", _createdBy);
        db.ExpenseCategoryNodes.Add(category);
        await db.SaveChangesAsync();
        var subcategory = ExpenseCategoryNode.CreateSubcategory(
            tenant.Id, company.Id, category, "RETQA-SUB", "RETQA Subcategoria", expenseAccount.Id, _createdBy
        );
        db.ExpenseCategoryNodes.Add(subcategory);
        await db.SaveChangesAsync();
        _subcategoryId = subcategory.Id;

        // RETENTIONS-DOCUMENT-SEQUENCE-02E: EmissionPoint real (con Establishment), ya que
        // RetentionIssuer ahora resuelve ambos y llama CaptureNextAsync — la FK física de
        // document_sequence.emission_point_id (ADR-019) exige que la fila exista de verdad.
        var establishment = Establishment.Create(
            _tenantId,
            branchId: null,
            _companyId,
            code: "001",
            name: "RETQA Establecimiento Matriz",
            address: "Av. Retenciones 123",
            phone: null,
            isMain: true,
            createdBy: _createdBy
        );
        db.Establishments.Add(establishment);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            _tenantId,
            _companyId,
            establishment.Id,
            code: "001",
            name: "RETQA Punto de Emision",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy: _createdBy
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();
        _emissionPointId = emissionPoint.Id;

        // ── DocumentFlowPolicy obligatoria para GASDOC (mismos defaults que
        // DocumentFlowPolicyBootstrapStep.BuildExpenseDocumentDefault) ────────────────
        var policy = DocumentFlowPolicy.Create(
            tenant.Id, company.Id, DocTypeCodes.ExpenseDocument, isActive: true,
            creationMode: CreationMode.DraftRequired,
            confirmationMode: ConfirmationMode.ManualConfirmation,
            authorizationMode: AuthorizationMode.None,
            pendingDocumentMode: PendingDocumentMode.None,
            cancellationMode: CancellationMode.AllowedAfterConfirmationWithReversal,
            requiresCancellationReason: true,
            requiresAttachment: false,
            requiresSupplier: true,
            requiresDueDate: true,
            payableGenerationMode: PayableGenerationMode.OnConfirmation,
            accountingPostingMode: AccountingPostingMode.OnConfirmation,
            inventoryImpactMode: InventoryImpactMode.None,
            notificationMode: NotificationMode.None,
            createdBy: _createdBy
        );
        db.DocumentFlowPolicies.Add(policy);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(IPublisher? publisher = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new ErpDbContext(options, new FixedCurrentTenant(_tenantId), publisher ?? new NoOpPublisher(), new FixedCurrentCompany(_companyId));
    }

    /// <summary>Mismo mecanismo de producción (AddMediatR con escaneo de ensamblado) que
    /// SupplierPaymentEndToEndTests — confirma que los posting translators de Expenses/Retentions
    /// se registran automáticamente como INotificationHandler.</summary>
    private (ErpDbContext db, IPublisher publisher) BuildWiredContext()
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString() + ";Include Error Detail=true")
            .EnableSensitiveDataLogging()
            .Options;
        var db = new ErpDbContext(options, new FixedCurrentTenant(_tenantId), deferred, new FixedCurrentCompany(_companyId));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(_tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(_companyId));
        services.AddSingleton<ICurrentBranch>(new FixedCurrentBranch(_branchId));
        services.AddSingleton<ICurrentUser>(new FixedCurrentUser(_createdBy));
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICompanyFinancialDestinationRepository, CompanyFinancialDestinationRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ExpenseDocumentConfirmedPostingTranslator).Assembly));

        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<IPublisher>();

        return (db, deferred);
    }

    /// <summary>
    /// TECH-DEBT-RETENTION-E2E-POSTING-SEED-CLEANUP-01 — reemplaza el fixture local que este test
    /// mantenía para "Expenses"/"DocumentConfirmed" y "Retentions"/"DocumentIssued" (PostingRule +
    /// AccountingPeriod construidos a mano) por el seed/backfill oficial: el mismo
    /// <see cref="AccountingBootstrapStep"/> que <c>CompanyBootstrapOrchestrator</c> corre para toda
    /// Company nueva del ERP, y que <c>AccountingChartBackfillServicePostingRuleTests</c>/
    /// <c>ExpensesPostingRuleSeedIntegrationTests</c>/<c>RetentionsPostingRuleSeedIntegrationTests</c>
    /// ya verifican de forma aislada. Siembra el Plan de Cuentas retail completo, un
    /// AccountingPeriod anual (cubre cualquier fecha del año en curso, incluidas las fechas fijas de
    /// este archivo) y las 10 MinimalPostingRules — sin fixture de PostingRule/AccountingPeriod
    /// propio de este test.
    /// </summary>
    private async Task SeedAccountingChartAsync(ErpDbContext db)
    {
        var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _createdBy));
    }

    // ── Construcción de gasto Draft (bypassa CreateDraftCommand: fixture directo de dominio) ──
    private async Task<Guid> CreateDraftExpenseAsync(
        ErpDbContext db,
        Guid supplierId,
        string documentNumber,
        decimal unitAmount = 100m,
        decimal vatRate = 15m,
        string? taxSupportCode = null
    )
    {
        var supplier = await db.BusinessPartners.FirstAsync(x => x.Id == supplierId);
        var document = ExpenseDocument.CreateDraft(
            _tenantId, _companyId, _branchId, supplierId,
            supplier.Name.LegalName, supplier.Identification.Number,
            new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 15),
            "01", documentNumber, _paymentTermId, "Contado QA", 1, 0, _createdBy,
            dueDate: new DateOnly(2026, 8, 15),
            taxSupportCode: taxSupportCode
        );
        var line = ExpenseLine.Create(
            document.Id, _tenantId, _subcategoryId, _expenseAccountId,
            "RETQA Linea de gasto", 1m, unitAmount, "IVA15", vatRate
        );
        document.ReplaceLines(new[] { line }, _createdBy);

        db.ExpenseDocuments.Add(document);
        await db.SaveChangesAsync();
        return document.Id;
    }

    private ConfirmExpenseDocumentHandler BuildConfirmHandler(ErpDbContext db) =>
        new(
            new ExpenseDocumentRepository(db, new FixedCurrentCompany(_companyId)),
            new ExpenseCategoryRepository(db, new FixedCurrentCompany(_companyId)),
            new AccountRepository(db),
            new AccountsPayableService(new AccountsPayableRepository(db)),
            new DocumentFlowPolicyService(db),
            new RetentionIssuer(
                new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId)),
                new RetentionEligibilityService(
                    new CompanyRepository(db),
                    new BusinessPartnerRoleRepository(db),
                    new RetentionCodeResolver(db)
                ),
                new EmissionPointRepository(db),
                new EstablishmentRepository(db),
                new DocumentSequenceRepository(db)
            ),
            new PaymentTermRepository(db),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId),
            new FixedCurrentBranch(_branchId),
            new FixedCurrentUser(_createdBy),
            NullLogger<ConfirmExpenseDocumentHandler>.Instance
        );

    private CancelExpenseDocumentHandler BuildCancelHandler(ErpDbContext db) =>
        new(
            new ExpenseDocumentRepository(db, new FixedCurrentCompany(_companyId)),
            new AccountsPayableRepository(db),
            new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId)),
            new RetentionCanceller(new AccountsPayableRepository(db)),
            new UnitOfWork(db),
            new DocumentFlowPolicyService(db),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId),
            new FixedCurrentBranch(_branchId),
            new FixedCurrentUser(_createdBy),
            NullLogger<CancelExpenseDocumentHandler>.Instance
        );

    private GetRetentionEligibilityHandler BuildEligibilityHandler(ErpDbContext db) =>
        new(
            new ExpenseDocumentRepository(db, new FixedCurrentCompany(_companyId)),
            new RetentionEligibilityService(
                new CompanyRepository(db),
                new BusinessPartnerRoleRepository(db),
                new RetentionCodeResolver(db)
            ),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId),
            new FixedCurrentBranch(_branchId)
        );

    private GetRetentionBySourceHandler BuildGetRetentionHandler(ErpDbContext db) =>
        new(
            new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId)),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId),
            new FixedCurrentBranch(_branchId)
        );

    private RetentionIntent BuildVatRetentionIntent(decimal vatAmount) =>
        new(
            AppliesRetention: true,
            EmissionPointId: _emissionPointId,
            IssueDate: new DateOnly(2026, 8, 15),
            Lines: new[]
            {
                new IssueRetentionLineInput(
                    RetentionTaxType.Vat,
                    RetentionVatCode,
                    vatAmount,
                    70m,
                    Math.Round(vatAmount * 0.70m, 2),
                    "RETQA retencion IVA 70%"
                ),
            }
        );

    // ══════════════════════════════════════════════════════════════════════
    // FLUJO FELIZ
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Flujo_feliz_confirma_gasto_con_retencion_neta_CxP_y_asiento_balanceado()
    {
        var (db, _) = BuildWiredContext();
        await SeedAccountingChartAsync(db);
        var expenseId = await CreateDraftExpenseAsync(db, _supplierNonExemptId, "RETQA-001");

        // Paso 2: elegibilidad sobre el borrador (Draft) — debe dar elegible para IVA.
        var eligibility = await BuildEligibilityHandler(db)
            .Handle(new GetRetentionEligibilityQuery(RetentionSourceDocumentType.ExpenseDocument, expenseId), CancellationToken.None);
        eligibility.IsSuccess.Should().BeTrue(because: eligibility.Error);
        eligibility.Value!.IsSupportedInThisPhase.Should().BeTrue();
        eligibility.Value.CanRetainVat.Should().BeTrue();
        eligibility.Value.IsSupplierExempt.Should().BeFalse();
        eligibility.Value.SuggestedVatRetentionCode.Should().Be(RetentionVatCode);
        // MissingRetentionCode agrega IVA e Income (RetentionEligibilityService.cs) — este fixture
        // deliberadamente NO configura DefaultRetentionIncomeCode (fuera de alcance de este
        // escenario, solo IVA), así que MissingRetentionCode=true es el resultado correcto y
        // esperado aquí (falta el código de Renta, no el de IVA que sí se está probando).

        // Paso 3: confirmar con RetentionIntent (VAT = 15, retenido 70% = 10.5).
        var confirmResult = await BuildConfirmHandler(db)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId, BuildVatRetentionIntent(15m)), CancellationToken.None);

        confirmResult.IsSuccess.Should().BeTrue(because: confirmResult.Error);

        // Paso 4: verificaciones.
        await using var verifyDb = CreateContext();
        var document = await verifyDb.ExpenseDocuments.Include(x => x.Lines).FirstAsync(x => x.Id == expenseId);
        document.Status.Should().Be(ExpenseStatus.Confirmed);
        document.GrandTotal.Should().Be(115m);

        var retention = await verifyDb.RetentionDocuments.Include(x => x.Lines)
            .FirstAsync(x => x.SourceDocumentId == expenseId && x.SourceDocumentType == RetentionSourceDocumentType.ExpenseDocument);
        retention.Status.Should().Be(RetentionStatus.Issued);
        retention.TotalRetained.Should().Be(10.5m);
        retention.TotalRetainedVat.Should().Be(10.5m);
        retention.RetentionNumber.Should().Be("001-001-000000001");

        // RETENTIONS-TAX-COMPONENT-MODEL-02B: periodo fiscal derivado de la IssueDate de la
        // retención (2026-08-15, ver BuildVatRetentionIntent) y snapshot del documento sustento
        // resuelto contra Postgres real (sin mocks) — confirma que el flujo E2E existente sigue
        // funcionando con los campos nuevos poblados correctamente, no solo con ellos en null.
        retention.FiscalPeriod.Should().Be("08/2026");
        retention.SourceDocumentSriTypeCode.Should().Be(document.DocumentType);
        retention.SourceDocumentNumber.Should().Be(document.DocumentNumber);
        retention.SourceDocumentIssueDate.Should().Be(document.IssueDate);
        retention.SourceDocumentSubtotal.Should().Be(document.Subtotal);
        retention.SourceDocumentTotal.Should().Be(document.GrandTotal);
        // BuildVatRetentionIntent no envía RetentionCodeDescription (contrato opcional, ver
        // IssueRetentionLineInput) — RetentionIssuer usa RetentionCode como respaldo, y "RETQA
        // retencion IVA 70%" sigue siendo la nota libre en Description (parámetro sin cambios).
        retention.Lines.Should().OnlyContain(l => l.RetentionCodeDescription == RetentionVatCode);
        retention.Lines.Should().OnlyContain(l => l.Description == "RETQA retencion IVA 70%");

        var payable = await verifyDb.AccountsPayables.Include(x => x.Installments)
            .FirstAsync(x => x.OriginType == AccountsPayableOriginType.ExpenseDocument && x.OriginId == expenseId);
        payable.OutstandingAmount.Should().Be(104.5m, "neto = bruto (115) - retenido (10.5)");
        payable.RetainedAmount.Should().Be(10.5m);

        var retentionEntry = await verifyDb.JournalEntries.Include(x => x.Lines)
            .FirstAsync(x => x.SourceModule == "Retentions" && x.SourceEventId == retention.Id);
        retentionEntry.Status.Should().Be(JournalEntryStatus.Posted);
        retentionEntry.Lines.Sum(l => l.Debit).Should().Be(retentionEntry.Lines.Sum(l => l.Credit));
        retentionEntry.Lines.Sum(l => l.Debit).Should().Be(10.5m);

        var expenseEntry = await verifyDb.JournalEntries.Include(x => x.Lines)
            .FirstAsync(x => x.SourceModule == "Expenses" && x.SourceEventId == expenseId);
        expenseEntry.Lines.Sum(l => l.Debit).Should().Be(expenseEntry.Lines.Sum(l => l.Credit));
        expenseEntry.Lines.Sum(l => l.Debit).Should().Be(115m);

        // Retención consultable desde el gasto vía GetRetentionBySourceQuery.
        var getRetention = await BuildGetRetentionHandler(db)
            .Handle(new GetRetentionBySourceQuery(RetentionSourceDocumentType.ExpenseDocument, expenseId), CancellationToken.None);
        getRetention.IsSuccess.Should().BeTrue();
        getRetention.Value.Should().NotBeNull();
        getRetention.Value!.TotalRetained.Should().Be(10.5m);
    }

    // ══════════════════════════════════════════════════════════════════════
    // BLOQUEOS
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bloqueo_empresa_no_retiene_IVA_impide_confirmar_con_retencion()
    {
        var (db, _) = BuildWiredContext();
        await SeedAccountingChartAsync(db);

        // Empresa deja de retener IVA — mutación directa (setter público en Company.cs).
        var company = await db.Companies.FirstAsync(x => x.Id == _companyId);
        company.WithholdsVat = false;
        await db.SaveChangesAsync();

        var expenseId = await CreateDraftExpenseAsync(db, _supplierNonExemptId, "RETQA-002");

        var eligibility = await BuildEligibilityHandler(db)
            .Handle(new GetRetentionEligibilityQuery(RetentionSourceDocumentType.ExpenseDocument, expenseId), CancellationToken.None);
        eligibility.Value!.CanRetainVat.Should().BeFalse();

        var confirmResult = await BuildConfirmHandler(db)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId, BuildVatRetentionIntent(15m)), CancellationToken.None);

        confirmResult.IsSuccess.Should().BeFalse("la empresa no está habilitada para retener IVA");

        await using var verifyDb = CreateContext();
        var document = await verifyDb.ExpenseDocuments.FirstAsync(x => x.Id == expenseId);
        document.Status.Should().Be(ExpenseStatus.Draft, "la confirmación completa debe abortar, no quedar Confirmed sin retención");
        (await verifyDb.RetentionDocuments.AnyAsync(x => x.SourceDocumentId == expenseId)).Should().BeFalse();
        (await verifyDb.AccountsPayables.AnyAsync(x => x.OriginId == expenseId)).Should().BeFalse();
    }

    [Fact]
    public async Task Bloqueo_proveedor_exento_impide_confirmar_con_retencion()
    {
        var (db, _) = BuildWiredContext();
        await SeedAccountingChartAsync(db);
        var expenseId = await CreateDraftExpenseAsync(db, _supplierExemptId, "RETQA-003");

        var eligibility = await BuildEligibilityHandler(db)
            .Handle(new GetRetentionEligibilityQuery(RetentionSourceDocumentType.ExpenseDocument, expenseId), CancellationToken.None);
        eligibility.Value!.IsSupplierExempt.Should().BeTrue();
        eligibility.Value.CanRetainVat.Should().BeFalse();

        var confirmResult = await BuildConfirmHandler(db)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId, BuildVatRetentionIntent(15m)), CancellationToken.None);

        confirmResult.IsSuccess.Should().BeFalse("el proveedor esta exento de retencion");

        await using var verifyDb = CreateContext();
        (await verifyDb.ExpenseDocuments.FirstAsync(x => x.Id == expenseId)).Status.Should().Be(ExpenseStatus.Draft);
        (await verifyDb.RetentionDocuments.AnyAsync(x => x.SourceDocumentId == expenseId)).Should().BeFalse();
    }

    [Fact]
    public async Task Bloqueo_sin_codigo_retencion_activo_impide_confirmar_con_retencion()
    {
        var (db, _) = BuildWiredContext();
        await SeedAccountingChartAsync(db);
        var expenseId = await CreateDraftExpenseAsync(db, _supplierMissingCodeId, "RETQA-004");

        var eligibility = await BuildEligibilityHandler(db)
            .Handle(new GetRetentionEligibilityQuery(RetentionSourceDocumentType.ExpenseDocument, expenseId), CancellationToken.None);
        eligibility.Value!.MissingRetentionCode.Should().BeTrue();
        eligibility.Value.CanRetainVat.Should().BeFalse();

        var intent = new RetentionIntent(
            true, _emissionPointId, new DateOnly(2026, 8, 15),
            new[] { new IssueRetentionLineInput(RetentionTaxType.Vat, MissingRetentionVatCode, 15m, 70m, 10.5m) }
        );
        var confirmResult = await BuildConfirmHandler(db)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId, intent), CancellationToken.None);

        confirmResult.IsSuccess.Should().BeFalse("no existe codigo de retencion activo en el catalogo SRI");

        await using var verifyDb = CreateContext();
        (await verifyDb.ExpenseDocuments.FirstAsync(x => x.Id == expenseId)).Status.Should().Be(ExpenseStatus.Draft);
    }

    [Fact]
    public void Bloqueo_RetentionIntent_incompleto_falla_validacion_antes_de_tocar_nada()
    {
        // AppliesRetention=true pero sin punto de emision/fecha/líneas — el validador real
        // (RetentionIntentValidator, el mismo que corre en el pipeline de MediatR vía
        // FluentValidation) debe rechazarlo. No se invoca ningún handler ni se toca BD: la guarda
        // ocurre antes de eso. RETENTIONS-DOCUMENT-SEQUENCE-02E: ya no valida RetentionNumber —
        // el número lo genera siempre el servidor, nunca es un input a validar.
        var intent = new RetentionIntent(true, null, null, null);
        var validation = new RetentionIntentValidator().Validate(intent);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.PropertyName == nameof(RetentionIntent.EmissionPointId));
        validation.Errors.Should().Contain(e => e.PropertyName == nameof(RetentionIntent.IssueDate));
        validation.Errors.Should().Contain(e => e.PropertyName == nameof(RetentionIntent.Lines));
    }

    // ══════════════════════════════════════════════════════════════════════
    // CANCELACIÓN
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Cancelar_gasto_confirmado_con_retencion_revierte_todo_sin_pagos_aplicados()
    {
        var (db, _) = BuildWiredContext();
        await SeedAccountingChartAsync(db);
        var expenseId = await CreateDraftExpenseAsync(db, _supplierNonExemptId, "RETQA-005");
        var confirmResult = await BuildConfirmHandler(db)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId, BuildVatRetentionIntent(15m)), CancellationToken.None);
        confirmResult.IsSuccess.Should().BeTrue(because: confirmResult.Error);

        var (dbCancel, _) = BuildWiredContext();
        var cancelResult = await BuildCancelHandler(dbCancel)
            .Handle(new CancelExpenseDocumentCommand(expenseId, "RETQA cancelacion de prueba"), CancellationToken.None);

        cancelResult.IsSuccess.Should().BeTrue(because: cancelResult.Error);

        await using var verifyDb = CreateContext();
        var document = await verifyDb.ExpenseDocuments.FirstAsync(x => x.Id == expenseId);
        document.Status.Should().Be(ExpenseStatus.Cancelled);

        var retention = await verifyDb.RetentionDocuments.FirstAsync(x => x.SourceDocumentId == expenseId);
        retention.Status.Should().Be(RetentionStatus.Cancelled);

        var payable = await verifyDb.AccountsPayables.Include(x => x.Installments)
            .FirstAsync(x => x.OriginId == expenseId);
        payable.RetainedAmount.Should().Be(0m, "la retencion se revierte completa");
        payable.Status.Should().Be(AccountsPayableStatus.Cancelled);

        // Asiento original de la retención queda Reversed, y su reverso balanceado existe.
        var retentionEntry = await verifyDb.JournalEntries.FirstAsync(x => x.SourceModule == "Retentions" && x.SourceEventId == retention.Id);
        retentionEntry.Status.Should().Be(JournalEntryStatus.Reversed);
        var retentionReversal = await verifyDb.JournalEntries.Include(x => x.Lines)
            .FirstAsync(x => x.SourceEventType == "Reversal" && x.SourceEventId == retentionEntry.Id);
        retentionReversal.Lines.Sum(l => l.Debit).Should().Be(retentionReversal.Lines.Sum(l => l.Credit));
        retentionReversal.Lines.Sum(l => l.Debit).Should().Be(10.5m);

        // Asiento del gasto también reversado (EXPENSES-CANCEL-01, comportamiento preexistente).
        var expenseEntry = await verifyDb.JournalEntries.FirstAsync(x => x.SourceModule == "Expenses" && x.SourceEventId == expenseId);
        expenseEntry.Status.Should().Be(JournalEntryStatus.Reversed);
    }

    [Fact]
    public async Task Cancelar_gasto_con_retencion_bloquea_si_la_CxP_ya_tiene_pagos_aplicados()
    {
        var (db, _) = BuildWiredContext();
        await SeedAccountingChartAsync(db);
        var expenseId = await CreateDraftExpenseAsync(db, _supplierNonExemptId, "RETQA-006");
        var confirmResult = await BuildConfirmHandler(db)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId, BuildVatRetentionIntent(15m)), CancellationToken.None);
        confirmResult.IsSuccess.Should().BeTrue(because: confirmResult.Error);

        // Aplica un pago mínimo directamente sobre la cuota (sin pasar por RegisterSupplierPaymentCommand
        // completo — no forma parte del alcance de esta fase modificar/explorar SupplierPayment más
        // allá de lo estrictamente necesario para forzar el bloqueo por pagos aplicados).
        await using (var paymentDb = CreateContext())
        {
            var payableToPay = await paymentDb.AccountsPayables.Include(x => x.Installments)
                .FirstAsync(x => x.OriginId == expenseId);
            payableToPay.RegisterPaymentToInstallment(payableToPay.Installments.Single().Id, 10m, _createdBy);
            await paymentDb.SaveChangesAsync();
        }

        var (dbCancel, _) = BuildWiredContext();
        var cancelResult = await BuildCancelHandler(dbCancel)
            .Handle(new CancelExpenseDocumentCommand(expenseId, "RETQA cancelacion bloqueada"), CancellationToken.None);

        cancelResult.IsSuccess.Should().BeFalse("la CxP ya tiene un pago aplicado");

        await using var verifyDb = CreateContext();
        var document = await verifyDb.ExpenseDocuments.FirstAsync(x => x.Id == expenseId);
        document.Status.Should().Be(ExpenseStatus.Confirmed, "el bloqueo no debe dejar el gasto a medias");
        var retention = await verifyDb.RetentionDocuments.FirstAsync(x => x.SourceDocumentId == expenseId);
        retention.Status.Should().Be(RetentionStatus.Issued, "la retencion no debe quedar cancelada si el bloqueo ocurrio");
        var payable = await verifyDb.AccountsPayables.Include(x => x.Installments).FirstAsync(x => x.OriginId == expenseId);
        payable.RetainedAmount.Should().Be(10.5m, "el bloqueo no debe alterar el estado de la CxP");
        payable.PaidAmount.Should().Be(10m);
    }

    // ══════════════════════════════════════════════════════════════════════
    // REGRESIÓN — gasto SIN retención sigue funcionando igual
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Confirmar_gasto_sin_retencion_sigue_funcionando_igual_que_antes()
    {
        var (db, _) = BuildWiredContext();
        await SeedAccountingChartAsync(db);
        var expenseId = await CreateDraftExpenseAsync(db, _supplierNonExemptId, "RETQA-007");

        var confirmResult = await BuildConfirmHandler(db)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId, Retention: null), CancellationToken.None);

        confirmResult.IsSuccess.Should().BeTrue(because: confirmResult.Error);

        await using var verifyDb = CreateContext();
        var document = await verifyDb.ExpenseDocuments.FirstAsync(x => x.Id == expenseId);
        document.Status.Should().Be(ExpenseStatus.Confirmed);

        (await verifyDb.RetentionDocuments.AnyAsync(x => x.SourceDocumentId == expenseId)).Should().BeFalse("sin intencion de retencion, no debe crearse ninguna");

        var payable = await verifyDb.AccountsPayables.Include(x => x.Installments).FirstAsync(x => x.OriginId == expenseId);
        payable.OutstandingAmount.Should().Be(115m, "bruto completo, sin neteo de retencion");
        payable.RetainedAmount.Should().Be(0m);

        var expenseEntry = await verifyDb.JournalEntries.Include(x => x.Lines)
            .FirstAsync(x => x.SourceModule == "Expenses" && x.SourceEventId == expenseId);
        expenseEntry.Lines.Sum(l => l.Debit).Should().Be(expenseEntry.Lines.Sum(l => l.Credit));
        expenseEntry.Lines.Sum(l => l.Debit).Should().Be(115m);

        // GetRetention debe devolver Success(null), nunca error, cuando no hay retencion.
        var getRetention = await BuildGetRetentionHandler(db)
            .Handle(new GetRetentionBySourceQuery(RetentionSourceDocumentType.ExpenseDocument, expenseId), CancellationToken.None);
        getRetention.IsSuccess.Should().BeTrue();
        getRetention.Value.Should().BeNull();

        // Cancelar ese mismo gasto (sin retencion) también sigue funcionando igual.
        var (dbCancel, _) = BuildWiredContext();
        var cancelResult = await BuildCancelHandler(dbCancel)
            .Handle(new CancelExpenseDocumentCommand(expenseId, "RETQA regresion cancelacion"), CancellationToken.None);
        cancelResult.IsSuccess.Should().BeTrue(because: cancelResult.Error);

        await using var verifyDb2 = CreateContext();
        (await verifyDb2.ExpenseDocuments.FirstAsync(x => x.Id == expenseId)).Status.Should().Be(ExpenseStatus.Cancelled);
    }

    // ══════════════════════════════════════════════════════════════════════
    // RETENTIONS-DOCUMENT-SEQUENCE-02E — numeración generada por CaptureNextAsync
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Numero_inicial_configurado_en_850_hace_que_la_retencion_arranque_ahi_y_la_siguiente_incremente()
    {
        var (db, _) = BuildWiredContext();
        await SeedAccountingChartAsync(db);

        // DOCUMENT-SEQUENCES-CONFIG-03: configurar el número inicial ANTES de la primera captura
        // real — mismo mecanismo que expone PUT /api/v1/settings/document-sequences/configure,
        // aplicado aquí directamente sobre el agregado (esa fase ya tiene su propia suite de tests
        // dedicada; no se repite el endpoint HTTP acá).
        var sequenceRepo = new DocumentSequenceRepository(db);
        var sequence = DocumentSequence.Create(_tenantId, _companyId, _emissionPointId, SriDocumentTypeCodes.Withholding);
        sequence.ConfigureNextNumber(850);
        await sequenceRepo.AddAsync(sequence);
        await sequenceRepo.SaveChangesAsync();

        var expenseId1 = await CreateDraftExpenseAsync(db, _supplierNonExemptId, "RETQA-SEQ-001");
        var confirm1 = await BuildConfirmHandler(db)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId1, BuildVatRetentionIntent(15m)), CancellationToken.None);
        confirm1.IsSuccess.Should().BeTrue(because: confirm1.Error);

        // Contexto NUEVO para la segunda confirmación — mismo criterio que el resto de la suite
        // (p. ej. Cancelar_gasto_confirmado_con_retencion_revierte_todo_sin_pagos_aplicados usa un
        // db distinto para confirmar y para cancelar) y el mismo patrón de producción real: cada
        // request HTTP resuelve su propio DbContext. Reutilizar el mismo db context para dos
        // capturas sucesivas es un artefacto de test, no el comportamiento real — el change
        // tracker de EF mantendría en memoria el DocumentSequence ya trackeado por el seed de
        // arriba, con su CurrentSeq desactualizado (CaptureNextAsync escribe con SQL raw, que
        // nunca actualiza una entidad ya trackeada), produciendo un número duplicado que jamás
        // ocurre contra un DbContext fresco por request.
        var (dbSecond, _) = BuildWiredContext();
        var expenseId2 = await CreateDraftExpenseAsync(dbSecond, _supplierNonExemptId, "RETQA-SEQ-002");
        var confirm2 = await BuildConfirmHandler(dbSecond)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId2, BuildVatRetentionIntent(15m)), CancellationToken.None);
        confirm2.IsSuccess.Should().BeTrue(because: confirm2.Error);

        await using var verifyDb = CreateContext();
        var retention1 = await verifyDb.RetentionDocuments.FirstAsync(x => x.SourceDocumentId == expenseId1);
        var retention2 = await verifyDb.RetentionDocuments.FirstAsync(x => x.SourceDocumentId == expenseId2);

        retention1.RetentionNumber.Should().Be("001-001-000000850");
        retention2.RetentionNumber.Should().Be("001-001-000000851");
    }

    [Fact]
    public async Task Concurrencia_no_duplica_numero_de_retencion_para_el_mismo_punto_de_emision()
    {
        // La garantía de exclusión mutua bajo concurrencia (advisory lock + transacción explícita,
        // hasta 500 req concurrentes sin duplicados) ya está probada exhaustivamente contra
        // Postgres real en ERP.API.Tests.Integration.DocumentSequenceConcurrencyTests (ADR-019) —
        // no se repite esa prueba de carga aquí. Este test confirma únicamente que el flujo real de
        // Retentions (ConfirmExpenseDocumentHandler → RetentionIssuer → CaptureNextAsync) preserva
        // esa garantía cuando varias retenciones se emiten en paralelo sobre el mismo punto de
        // emisión — con una carga moderada (10) para no duplicar el costo de la suite de 500 ya
        // existente.
        const int n = 10;
        var (seedDb, _) = BuildWiredContext();
        await SeedAccountingChartAsync(seedDb);

        var expenseIds = new List<Guid>();
        for (var i = 0; i < n; i++)
            expenseIds.Add(await CreateDraftExpenseAsync(seedDb, _supplierNonExemptId, $"RETQA-CONC-{i:D3}"));

        var results = await Task.WhenAll(
            expenseIds.Select(async expenseId =>
            {
                var (db, _) = BuildWiredContext();
                return await BuildConfirmHandler(db)
                    .Handle(new ConfirmExpenseDocumentCommand(expenseId, BuildVatRetentionIntent(15m)), CancellationToken.None);
            })
        );

        results.Should().OnlyContain(r => r.IsSuccess);

        await using var verifyDb = CreateContext();
        var numbers = await verifyDb.RetentionDocuments
            .Where(x => expenseIds.Contains(x.SourceDocumentId))
            .Select(x => x.RetentionNumber)
            .ToListAsync();

        numbers.Should().HaveCount(n);
        numbers.Should().OnlyHaveUniqueItems("CaptureNextAsync debe seguir garantizando unicidad incluso bajo confirmaciones concurrentes de gastos con retención");
    }

    // ══════════════════════════════════════════════════════════════════════
    // RETENTIONS-SOURCE-DOCUMENT-TAX-SUPPORT-02G — codSustento en el snapshot
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Retencion_emitida_copia_TaxSupportCode_del_ExpenseDocument_real_contra_Postgres()
    {
        var (db, _) = BuildWiredContext();
        await SeedAccountingChartAsync(db);
        var expenseId = await CreateDraftExpenseAsync(
            db, _supplierNonExemptId, "RETQA-TAXSUP-001", taxSupportCode: "02"
        );

        var confirmResult = await BuildConfirmHandler(db)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId, BuildVatRetentionIntent(15m)), CancellationToken.None);
        confirmResult.IsSuccess.Should().BeTrue(because: confirmResult.Error);

        await using var verifyDb = CreateContext();
        var retention = await verifyDb.RetentionDocuments
            .FirstAsync(x => x.SourceDocumentId == expenseId);

        retention.SourceDocumentTaxSupportCode.Should().Be("02");
    }

    [Fact]
    public async Task TaxSupportCode_del_snapshot_no_cambia_si_el_ExpenseDocument_origen_se_edita_despues_de_emitir()
    {
        var (db, _) = BuildWiredContext();
        await SeedAccountingChartAsync(db);
        var expenseId = await CreateDraftExpenseAsync(
            db, _supplierNonExemptId, "RETQA-TAXSUP-002", taxSupportCode: "02"
        );

        var confirmResult = await BuildConfirmHandler(db)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId, BuildVatRetentionIntent(15m)), CancellationToken.None);
        confirmResult.IsSuccess.Should().BeTrue(because: confirmResult.Error);

        // El gasto ya está Confirmed (EnsureDraft bloquea UpdateDraft) — no hay forma real de
        // "editarlo" después. Simulamos el escenario que la regla 8 prohíbe (recalcular el
        // snapshot histórico desde datos vivos) igual que
        // Snapshot_de_TaxSupportCode_queda_congelado_y_no_sigue_al_documento_origen_tras_emitir en
        // IssueRetentionHandlerTests: una corrección manual directa en BD sobre la misma fila
        // (fuera del dominio, el único camino físicamente posible una vez Confirmed) no debe
        // alterar la retención ya emitida, porque RetentionDocument nunca lee en vivo — solo
        // guardó una copia primitiva al emitir.
        await using (var mutateDb = CreateContext())
        {
            var liveDocument = await mutateDb.ExpenseDocuments.FirstAsync(x => x.Id == expenseId);
            liveDocument.GetType()
                .GetProperty(nameof(ExpenseDocument.TaxSupportCode))!
                .SetValue(liveDocument, "04");
            await mutateDb.SaveChangesAsync();
        }

        await using var verifyDb = CreateContext();
        var retention = await verifyDb.RetentionDocuments.FirstAsync(x => x.SourceDocumentId == expenseId);
        var mutatedDocument = await verifyDb.ExpenseDocuments.FirstAsync(x => x.Id == expenseId);

        mutatedDocument.TaxSupportCode.Should().Be("04", "confirma que la mutación directa sí ocurrió");
        retention.SourceDocumentTaxSupportCode.Should()
            .Be("02", "el snapshot ya emitido nunca se recalcula desde el documento origen");
    }

    [Fact]
    public async Task Retencion_sigue_emitiendose_si_el_ExpenseDocument_no_tiene_TaxSupportCode()
    {
        var (db, _) = BuildWiredContext();
        await SeedAccountingChartAsync(db);
        var expenseId = await CreateDraftExpenseAsync(
            db, _supplierNonExemptId, "RETQA-TAXSUP-003", taxSupportCode: null
        );

        var confirmResult = await BuildConfirmHandler(db)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId, BuildVatRetentionIntent(15m)), CancellationToken.None);

        confirmResult.IsSuccess.Should().BeTrue(because: confirmResult.Error);
        await using var verifyDb = CreateContext();
        var retention = await verifyDb.RetentionDocuments.FirstAsync(x => x.SourceDocumentId == expenseId);
        retention.SourceDocumentTaxSupportCode.Should().BeNull();
    }

    // ── Infraestructura de test (mismos stubs que SupplierPaymentEndToEndTests) ─────

    private sealed class DeferredPublisher : IPublisher
    {
        public IPublisher? Inner { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Inner!.Publish(notification, cancellationToken);

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Inner!.Publish(notification, cancellationToken);
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

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
