using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Finance;

/// <summary>
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para el flujo completo de
/// liquidación de pagos a proveedor (Fase 5.6.4): RegisterPaymentCommand → Payment.Apply() →
/// SupplierPaymentAppliedEvent → SupplierPaymentAppliedPostingTranslator → IPostingEngine →
/// JournalEntry. Sin mocks de EF Core. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SupplierPaymentPostingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_supplier_payment_posting_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _supplierId;
    private Guid _createdBy;
    private Guid _payableId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        var supplier = BusinessPartner.Create(tenant.Id, "05", "1710034065", PersonType.Natural, "Proveedor Test", _createdBy);

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.BusinessPartners.Add(supplier);
        await db.SaveChangesAsync();

        // PurchasePayable no tiene FK real a PurchaseInvoice/BusinessPartner (a diferencia de
        // SalesReceivable) — PurchaseId es un Guid suelto, solo la empresa/tenant son reales.
        var payable = PurchasePayable.Create(tenant.Id, company.Id, Guid.NewGuid(), supplier.Id, 300m, _createdBy);
        db.PurchasePayables.Add(payable);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _supplierId = supplier.Id;
        _payableId = payable.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(IPublisher? publisher = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options, new FixedCurrentTenant(_tenantId), publisher ?? new NoOpPublisher(), new FixedCurrentCompany(_companyId));
    }

    private static (ErpDbContext db, IPublisher publisher) BuildWiredContext(
        Guid tenantId, Guid companyId, PostgreSqlContainer postgres)
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(postgres.GetConnectionString() + ";Include Error Detail=true")
            .EnableSensitiveDataLogging()
            .Options;
        var db = new ErpDbContext(options, new FixedCurrentTenant(tenantId), deferred, new FixedCurrentCompany(companyId));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(companyId));
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SupplierPaymentAppliedPostingTranslator).Assembly));

        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<IPublisher>();

        return (db, deferred);
    }

    private async Task SeedRuleAndPeriodAsync(ErpDbContext db, DateOnly entryDate)
    {
        var payableAccount = Account.Create(
            _tenantId, _companyId, AccountCode.Create($"2.1.{Guid.NewGuid():N}"[..8]), "Cuentas por Pagar", null,
            AccountType.Liability, AccountNature.Credit, allowsPosting: true, createdBy: _createdBy);
        var cashAccount = Account.Create(
            _tenantId, _companyId, AccountCode.Create($"1.1.{Guid.NewGuid():N}"[..8]), "Caja", null,
            AccountType.Asset, AccountNature.Debit, allowsPosting: true, createdBy: _createdBy);
        db.Accounts.AddRange(payableAccount, cashAccount);

        var rule = PostingRule.Create(_tenantId, _companyId, "Finance", "SupplierPaymentApplied", null, null, null, _createdBy);
        rule.AddLine(payableAccount.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(cashAccount.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);

        var period = AccountingPeriod.Create(
            _tenantId, _companyId, entryDate.Year, entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1),
            new DateOnly(entryDate.Year, entryDate.Month, DateTime.DaysInMonth(entryDate.Year, entryDate.Month)),
            _createdBy);

        db.PostingRules.Add(rule);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    private static RegisterPaymentCommandHandler BuildHandler(ErpDbContext db, Guid tenantId, Guid companyId, Guid userId) => new(
        new PaymentRepository(db),
        new PurchasePayableRepository(db, new FixedCurrentCompany(companyId)),
        new FixedCurrentTenant(tenantId),
        new FixedCurrentCompany(companyId),
        new FixedCurrentUser(userId));

    private static ReversePaymentCommandHandler BuildReverseHandler(ErpDbContext db, Guid tenantId, Guid companyId, Guid userId) => new(
        new PaymentRepository(db),
        new PurchasePayableRepository(db, new FixedCurrentCompany(companyId)),
        new FixedCurrentTenant(tenantId),
        new FixedCurrentCompany(companyId),
        new FixedCurrentUser(userId));

    private static RegisterPaymentCommand SingleLineCommand(Guid payableId, decimal amount, DateOnly paymentDate, Guid supplierId) => new(
        supplierId, amount, paymentDate, PaymentMethodId: null, Reference: null,
        new[] { new PaymentApplicationLineInput(payableId, null, amount) });

    /// <summary>Fase 5.6.6 — igual que en Collection: SupplierPaymentReversedEvent fecha el hecho
    /// contable con OccurredOn (fecha real de ejecución), así que se siembra un único período
    /// cubriendo "hoy" para ambos asientos (aplicación + reverso).</summary>
    private async Task SeedAppliedAndReversedRulesAndPeriodAsync(ErpDbContext db, DateOnly entryDate)
    {
        var payableAccount = Account.Create(
            _tenantId, _companyId, AccountCode.Create($"2.1.{Guid.NewGuid():N}"[..8]), "Cuentas por Pagar", null,
            AccountType.Liability, AccountNature.Credit, allowsPosting: true, createdBy: _createdBy);
        var cashAccount = Account.Create(
            _tenantId, _companyId, AccountCode.Create($"1.1.{Guid.NewGuid():N}"[..8]), "Caja", null,
            AccountType.Asset, AccountNature.Debit, allowsPosting: true, createdBy: _createdBy);
        db.Accounts.AddRange(payableAccount, cashAccount);

        var appliedRule = PostingRule.Create(_tenantId, _companyId, "Finance", "SupplierPaymentApplied", null, null, null, _createdBy);
        appliedRule.AddLine(payableAccount.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        appliedRule.AddLine(cashAccount.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);

        var reversedRule = PostingRule.Create(_tenantId, _companyId, "Finance", "SupplierPaymentReversed", null, null, null, _createdBy);
        reversedRule.AddLine(cashAccount.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        reversedRule.AddLine(payableAccount.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);

        var period = AccountingPeriod.Create(
            _tenantId, _companyId, entryDate.Year, entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1),
            new DateOnly(entryDate.Year, entryDate.Month, DateTime.DaysInMonth(entryDate.Year, entryDate.Month)),
            _createdBy);

        db.PostingRules.AddRange(appliedRule, reversedRule);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task RegisterPaymentCommand_persiste_Payment_lineas_y_actualiza_PurchasePayable()
    {
        var paymentDate = new DateOnly(2026, 7, 15);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);

        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(SingleLineCommand(_payableId, 300m, paymentDate, _supplierId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var payment = await verifyDb.Payments.Include(x => x.Lines).FirstAsync(x => x.Id == result.Value!.Id);
        var payable = await verifyDb.PurchasePayables.FirstAsync(x => x.Id == _payableId);

        payment.Direction.Should().Be(PaymentDirection.Payment);
        payment.Status.Should().Be(PaymentStatus.Applied);
        payment.PartnerId.Should().Be(_supplierId);
        payment.Amount.Should().Be(300m);
        payment.Lines.Should().ContainSingle();
        payment.Lines.Single().PayableId.Should().Be(_payableId);
        payment.Lines.Single().AppliedAmount.Should().Be(300m);

        payable.PaidAmount.Should().Be(300m);
        payable.BalanceDue.Should().Be(0m);
    }

    [Fact]
    public async Task RegisterPaymentCommand_pago_parcial_seguido_de_complemento_llega_a_saldo_cero()
    {
        var paymentDate = new DateOnly(2026, 7, 15);

        var (dbA, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        var firstResult = await BuildHandler(dbA, _tenantId, _companyId, _createdBy)
            .Handle(SingleLineCommand(_payableId, 120m, paymentDate, _supplierId), CancellationToken.None);
        firstResult.IsSuccess.Should().BeTrue();

        await using (var midDb = CreateContext())
        {
            var midPayable = await midDb.PurchasePayables.FirstAsync(x => x.Id == _payableId);
            midPayable.PaidAmount.Should().Be(120m);
            midPayable.BalanceDue.Should().Be(180m);
        }

        var (dbB, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        var secondResult = await BuildHandler(dbB, _tenantId, _companyId, _createdBy)
            .Handle(SingleLineCommand(_payableId, 180m, paymentDate, _supplierId), CancellationToken.None);
        secondResult.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var payable = await verifyDb.PurchasePayables.FirstAsync(x => x.Id == _payableId);
        var paymentCount = await verifyDb.Payments.CountAsync(x => x.PartnerId == _supplierId);

        payable.PaidAmount.Should().Be(300m);
        payable.BalanceDue.Should().Be(0m);
        paymentCount.Should().Be(2, because: "dos pagos independientes, cada uno con su propio Payment");
    }

    [Fact]
    public async Task RegisterPaymentCommand_con_PostingRule_genera_JournalEntry_balanceado()
    {
        var paymentDate = new DateOnly(2026, 7, 15);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(db, paymentDate);

        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(SingleLineCommand(_payableId, 300m, paymentDate, _supplierId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb.JournalEntries.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.SourceEventId == result.Value!.Id);

        entry.Should().NotBeNull();
        entry!.SourceModule.Should().Be("Finance");
        entry.SourceEventType.Should().Be("SupplierPaymentApplied");
        entry.Status.Should().Be(JournalEntryStatus.Posted);
        entry.Lines.Should().HaveCount(2);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Sum(l => l.Debit).Should().Be(300m);
    }

    [Fact]
    public async Task RegisterPaymentCommand_sin_PostingRule_persiste_Payment_sin_generar_JournalEntry()
    {
        var paymentDate = new DateOnly(2026, 7, 15);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        // Sin PostingRule sembrada — fuerza RULE_NOT_FOUND dentro del pipeline.

        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(SingleLineCommand(_payableId, 300m, paymentDate, _supplierId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: "el fallo del Posting Engine no debe revertir el registro del pago");

        await using var verifyDb = CreateContext();
        var payable = await verifyDb.PurchasePayables.FirstAsync(x => x.Id == _payableId);
        payable.PaidAmount.Should().Be(300m);

        var entry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x => x.SourceEventId == result.Value!.Id);
        entry.Should().BeNull();
    }

    [Fact]
    public async Task Republicar_SupplierPaymentAppliedEvent_no_duplica_JournalEntry_Payment_ni_lineas()
    {
        var paymentDate = new DateOnly(2026, 7, 15);
        var (db, publisher) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(db, paymentDate);

        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(SingleLineCommand(_payableId, 300m, paymentDate, _supplierId), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        var paymentId = result.Value!.Id;

        var repeated = new SupplierPaymentAppliedEvent(_tenantId, paymentId, _companyId, _supplierId, 300m, paymentDate);
        await publisher.Publish(repeated, CancellationToken.None);

        await using var verifyDb = CreateContext();
        var journalEntryCount = await verifyDb.JournalEntries.CountAsync(x => x.SourceEventId == paymentId);
        var paymentCount = await verifyDb.Payments.CountAsync(x => x.Id == paymentId);
        var lineCount = await verifyDb.PaymentApplicationLines.CountAsync(x => x.PaymentId == paymentId);

        journalEntryCount.Should().Be(1, because: "el Posting Engine ya garantiza idempotencia por SourceEventId (Fase 3.1)");
        paymentCount.Should().Be(1, because: "republicar el evento no crea un segundo Payment — solo el comando lo hace");
        lineCount.Should().Be(1, because: "republicar el evento no duplica PaymentApplicationLine");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Fase 5.6.6 — Reverso de pagos (SupplierPaymentReversedEvent → SupplierPaymentReversedPostingTranslator)
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ReversePaymentCommand_restaura_PaidAmount_y_BalanceDue_de_PurchasePayable()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedAppliedAndReversedRulesAndPeriodAsync(db, today);

        var registerResult = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(SingleLineCommand(_payableId, 300m, today, _supplierId), CancellationToken.None);
        registerResult.IsSuccess.Should().BeTrue();
        var paymentId = registerResult.Value!.Id;

        var (dbReverse, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        var reverseResult = await BuildReverseHandler(dbReverse, _tenantId, _companyId, _createdBy)
            .Handle(new ReversePaymentCommand(paymentId, "Error de digitación"), CancellationToken.None);

        reverseResult.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var payable = await verifyDb.PurchasePayables.FirstAsync(x => x.Id == _payableId);
        var payment = await verifyDb.Payments.FirstAsync(x => x.Id == paymentId);

        payable.PaidAmount.Should().Be(0m);
        payable.BalanceDue.Should().Be(300m);
        payment.Status.Should().Be(PaymentStatus.Reversed);
        payment.ReverseReason.Should().Be("Error de digitación");
    }

    [Fact]
    public async Task ReversePaymentCommand_con_PostingRule_genera_JournalEntry_de_reverso()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedAppliedAndReversedRulesAndPeriodAsync(db, today);

        var registerResult = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(SingleLineCommand(_payableId, 300m, today, _supplierId), CancellationToken.None);
        registerResult.IsSuccess.Should().BeTrue();
        var paymentId = registerResult.Value!.Id;

        var (dbReverse, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        var reverseResult = await BuildReverseHandler(dbReverse, _tenantId, _companyId, _createdBy)
            .Handle(new ReversePaymentCommand(paymentId, "Error de digitación"), CancellationToken.None);
        reverseResult.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var appliedEntry = await verifyDb.JournalEntries
            .FirstOrDefaultAsync(x => x.SourceEventId == paymentId && x.SourceEventType == "SupplierPaymentApplied");
        var reversedEntry = await verifyDb.JournalEntries.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.SourceEventId == paymentId && x.SourceEventType == "SupplierPaymentReversed");

        appliedEntry.Should().NotBeNull(because: "el asiento original de aplicación no debe desaparecer");
        reversedEntry.Should().NotBeNull();
        reversedEntry!.SourceModule.Should().Be("Finance");
        reversedEntry.Status.Should().Be(JournalEntryStatus.Posted);
        reversedEntry.Lines.Should().HaveCount(2);
        reversedEntry.Lines.Sum(l => l.Debit).Should().Be(reversedEntry.Lines.Sum(l => l.Credit));
        reversedEntry.Lines.Sum(l => l.Debit).Should().Be(300m);
    }

    [Fact]
    public async Task Republicar_SupplierPaymentReversedEvent_no_duplica_el_JournalEntry_de_reverso()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedAppliedAndReversedRulesAndPeriodAsync(db, today);

        var registerResult = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(SingleLineCommand(_payableId, 300m, today, _supplierId), CancellationToken.None);
        registerResult.IsSuccess.Should().BeTrue();
        var paymentId = registerResult.Value!.Id;

        var (dbReverse, publisher) = BuildWiredContext(_tenantId, _companyId, _postgres);
        var reverseResult = await BuildReverseHandler(dbReverse, _tenantId, _companyId, _createdBy)
            .Handle(new ReversePaymentCommand(paymentId, "Error de digitación"), CancellationToken.None);
        reverseResult.IsSuccess.Should().BeTrue();

        var repeated = new SupplierPaymentReversedEvent(_tenantId, paymentId, _companyId, _supplierId, 300m, "Error de digitación");
        await publisher.Publish(repeated, CancellationToken.None);

        await using var verifyDb = CreateContext();
        var reversedEntryCount = await verifyDb.JournalEntries
            .CountAsync(x => x.SourceEventId == paymentId && x.SourceEventType == "SupplierPaymentReversed");
        var payment = await verifyDb.Payments.FirstAsync(x => x.Id == paymentId);

        reversedEntryCount.Should().Be(1, because: "el Posting Engine ya garantiza idempotencia por (CompanyId, SourceModule, SourceEventId, SourceEventType)");
        payment.Status.Should().Be(PaymentStatus.Reversed, because: "republicar el evento no vuelve a ejecutar Payment.Reverse()");
    }

    private sealed class DeferredPublisher : IPublisher
    {
        public IPublisher? Inner { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Inner!.Publish(notification, cancellationToken);

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Inner!.Publish(notification, cancellationToken);
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
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
