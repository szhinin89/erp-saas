using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Finance;

/// <summary>
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para el flujo completo de
/// liquidación de cobros (Fase 5.6.4): RegisterCollectionCommand → Payment.Apply() →
/// CollectionAppliedEvent → CollectionAppliedPostingTranslator → IPostingEngine → JournalEntry.
/// Mismo patrón de DI real (AddMediatR con escaneo de ensamblado) que
/// SalesInvoiceAuthorizedPostingIntegrationTests — sin mocks de EF Core. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CollectionPostingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_collection_posting_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _customerId;
    private Guid _cashSessionId;
    private Guid _createdBy;
    private Guid _receivableId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _createdBy
        );
        var branch = Branch.Create(
            tenant.Id,
            "Matriz",
            "Av. Principal 123",
            "001",
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
            _createdBy,
            companyId: company.Id
        );
        var customer = BusinessPartner.Create(
            tenant.Id,
            "05",
            "1710034065",
            1,
            "Cliente Test",
            _createdBy
        );
        var establishment = Establishment.Create(
            tenant.Id,
            branchId: branch.Id,
            company.Id,
            code: "001",
            name: "Matriz Test",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: _createdBy
        );
        var cashRegister = CashRegister.Create(
            tenant.Id,
            company.Id,
            branch.Id,
            "CAJA-01",
            "Caja Principal",
            _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(customer);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            tenant.Id,
            company.Id,
            establishment.Id,
            code: "001",
            name: "PE-001",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy: _createdBy
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();

        var cashSession = CashSession.Open(
            tenant.Id,
            company.Id,
            branch.Id,
            _createdBy,
            cashRegister.Id,
            "CAJA-01",
            "Caja Principal",
            emissionPoint.Id,
            "001",
            0m,
            _createdBy
        );
        db.CashSessions.Add(cashSession);
        await db.SaveChangesAsync();

        // Factura solo como ancla de la FK real de SalesReceivable.InvoiceId — no se autoriza,
        // el flujo bajo prueba parte directamente de la CxC ya existente.
        var customerSnapshot = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(
            Guid.NewGuid(),
            "Crédito",
            installments: 1,
            daysBetween: 30
        );
        var invoice = SalesInvoice.CreateDraft(
            tenant.Id,
            company.Id,
            branch.Id,
            customer.Id,
            customerSnapshot,
            invoiceNumber: "001-001-000000001",
            issueDate: new DateOnly(2026, 7, 1),
            createdBy: _createdBy,
            paymentTerm: paymentTerm,
            cashSessionId: cashSession.Id
        );
        var line = SalesInvoiceDetail.Create(
            invoice.Id,
            tenant.Id,
            "Producto Test",
            quantity: 1,
            unitPrice: 300m,
            vatCode: "10",
            uomCode: "UNIT"
        );
        invoice.ReplaceLines(new[] { line }, _createdBy);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();

        var receivable = SalesReceivable.Create(
            tenant.Id,
            company.Id,
            invoice.Id,
            customer.Id,
            300m,
            _createdBy
        );
        db.SalesReceivables.Add(receivable);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _customerId = customer.Id;
        _cashSessionId = cashSession.Id;
        _receivableId = receivable.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(IPublisher? publisher = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            publisher ?? new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    /// <summary>Mismo mecanismo de producción (AddMediatR con escaneo de ensamblado) que
    /// SalesInvoiceAuthorizedPostingIntegrationTests — confirma que CollectionAppliedPostingTranslator
    /// se registra automáticamente como INotificationHandler&lt;CollectionAppliedEvent&gt;.</summary>
    private static (ErpDbContext db, IPublisher publisher) BuildWiredContext(
        Guid tenantId,
        Guid companyId,
        PostgreSqlContainer postgres
    )
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(postgres.GetConnectionString() + ";Include Error Detail=true")
            .EnableSensitiveDataLogging()
            .Options;
        var db = new ErpDbContext(
            options,
            new FixedCurrentTenant(tenantId),
            deferred,
            new FixedCurrentCompany(companyId)
        );

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(companyId));
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(CollectionAppliedPostingTranslator).Assembly)
        );

        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<IPublisher>();

        return (db, deferred);
    }

    private async Task SeedRuleAndPeriodAsync(ErpDbContext db, DateOnly entryDate)
    {
        var cashAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"1.1.{Guid.NewGuid():N}"[..8]),
            "Caja",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var receivableAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"1.2.{Guid.NewGuid():N}"[..8]),
            "Cuentas por Cobrar",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        db.Accounts.AddRange(cashAccount, receivableAccount);

        var rule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Finance",
            "CollectionApplied",
            null,
            null,
            null,
            _createdBy
        );
        rule.AddLine(cashAccount.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(receivableAccount.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);

        var period = AccountingPeriod.Create(
            _tenantId,
            _companyId,
            entryDate.Year,
            entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1),
            new DateOnly(
                entryDate.Year,
                entryDate.Month,
                DateTime.DaysInMonth(entryDate.Year, entryDate.Month)
            ),
            _createdBy
        );

        db.PostingRules.Add(rule);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    private static RegisterCollectionCommandHandler BuildHandler(
        ErpDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid userId
    ) =>
        new(
            new PaymentRepository(db),
            new SalesReceivableRepository(db, new FixedCurrentCompany(companyId)),
            new FixedCurrentTenant(tenantId),
            new FixedCurrentCompany(companyId),
            new FixedCurrentUser(userId)
        );

    private static ReverseCollectionCommandHandler BuildReverseHandler(
        ErpDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid userId
    ) =>
        new(
            new PaymentRepository(db),
            new SalesReceivableRepository(db, new FixedCurrentCompany(companyId)),
            new FixedCurrentTenant(tenantId),
            new FixedCurrentCompany(companyId),
            new FixedCurrentUser(userId)
        );

    private static RegisterCollectionCommand SingleLineCommand(
        Guid receivableId,
        decimal amount,
        DateOnly paymentDate,
        Guid customerId
    ) =>
        new(
            customerId,
            amount,
            paymentDate,
            PaymentMethodId: null,
            Reference: null,
            new[] { new PaymentApplicationLineInput(receivableId, null, amount) }
        );

    /// <summary>Fase 5.6.6 — siembra tanto la regla de aplicación como la de reverso, más un único
    /// período compartido (dos períodos para el mismo mes/empresa no aportan nada y complicarían
    /// la resolución del PostingPeriodResolver sin necesidad).</summary>
    private async Task SeedAppliedAndReversedRulesAndPeriodAsync(
        ErpDbContext db,
        DateOnly entryDate
    )
    {
        var cashAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"1.1.{Guid.NewGuid():N}"[..8]),
            "Caja",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var receivableAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"1.2.{Guid.NewGuid():N}"[..8]),
            "Cuentas por Cobrar",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        db.Accounts.AddRange(cashAccount, receivableAccount);

        var appliedRule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Finance",
            "CollectionApplied",
            null,
            null,
            null,
            _createdBy
        );
        appliedRule.AddLine(cashAccount.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        appliedRule.AddLine(
            receivableAccount.Id,
            AccountNature.Credit,
            PostingAmountKind.GrandTotal
        );

        var reversedRule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Finance",
            "CollectionReversed",
            null,
            null,
            null,
            _createdBy
        );
        reversedRule.AddLine(
            receivableAccount.Id,
            AccountNature.Debit,
            PostingAmountKind.GrandTotal
        );
        reversedRule.AddLine(cashAccount.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);

        var period = AccountingPeriod.Create(
            _tenantId,
            _companyId,
            entryDate.Year,
            entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1),
            new DateOnly(
                entryDate.Year,
                entryDate.Month,
                DateTime.DaysInMonth(entryDate.Year, entryDate.Month)
            ),
            _createdBy
        );

        db.PostingRules.AddRange(appliedRule, reversedRule);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task RegisterCollectionCommand_persiste_Payment_lineas_y_actualiza_SalesReceivable()
    {
        var paymentDate = new DateOnly(2026, 7, 15);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);

        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(
                SingleLineCommand(_receivableId, 300m, paymentDate, _customerId),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var payment = await verifyDb
            .Payments.Include(x => x.Lines)
            .FirstAsync(x => x.Id == result.Value!.Id);
        var receivable = await verifyDb.SalesReceivables.FirstAsync(x => x.Id == _receivableId);

        payment.Direction.Should().Be(PaymentDirection.Collection);
        payment.Status.Should().Be(PaymentStatus.Applied);
        payment.PartnerId.Should().Be(_customerId);
        payment.Amount.Should().Be(300m);
        payment.Lines.Should().ContainSingle();
        payment.Lines.Single().ReceivableId.Should().Be(_receivableId);
        payment.Lines.Single().AppliedAmount.Should().Be(300m);

        receivable.PaidAmount.Should().Be(300m);
        receivable.BalanceDue.Should().Be(0m);
    }

    [Fact]
    public async Task RegisterCollectionCommand_cobro_parcial_seguido_de_complemento_llega_a_saldo_cero()
    {
        var paymentDate = new DateOnly(2026, 7, 15);

        var (dbA, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        var firstResult = await BuildHandler(dbA, _tenantId, _companyId, _createdBy)
            .Handle(
                SingleLineCommand(_receivableId, 120m, paymentDate, _customerId),
                CancellationToken.None
            );
        firstResult.IsSuccess.Should().BeTrue();

        await using (var midDb = CreateContext())
        {
            var midReceivable = await midDb.SalesReceivables.FirstAsync(x => x.Id == _receivableId);
            midReceivable.PaidAmount.Should().Be(120m);
            midReceivable.BalanceDue.Should().Be(180m);
        }

        var (dbB, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        var secondResult = await BuildHandler(dbB, _tenantId, _companyId, _createdBy)
            .Handle(
                SingleLineCommand(_receivableId, 180m, paymentDate, _customerId),
                CancellationToken.None
            );
        secondResult.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var receivable = await verifyDb.SalesReceivables.FirstAsync(x => x.Id == _receivableId);
        var paymentCount = await verifyDb.Payments.CountAsync(x => x.PartnerId == _customerId);

        receivable.PaidAmount.Should().Be(300m);
        receivable.BalanceDue.Should().Be(0m);
        paymentCount
            .Should()
            .Be(2, because: "dos cobros independientes, cada uno con su propio Payment");
    }

    [Fact]
    public async Task RegisterCollectionCommand_con_PostingRule_genera_JournalEntry_balanceado()
    {
        var paymentDate = new DateOnly(2026, 7, 15);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(db, paymentDate);

        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(
                SingleLineCommand(_receivableId, 300m, paymentDate, _customerId),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.SourceEventId == result.Value!.Id);

        entry.Should().NotBeNull();
        entry!.SourceModule.Should().Be("Finance");
        entry.SourceEventType.Should().Be("CollectionApplied");
        entry.Status.Should().Be(JournalEntryStatus.Posted);
        entry.Lines.Should().HaveCount(2);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Sum(l => l.Debit).Should().Be(300m);
    }

    [Fact]
    public async Task RegisterCollectionCommand_sin_PostingRule_persiste_Payment_sin_generar_JournalEntry()
    {
        var paymentDate = new DateOnly(2026, 7, 15);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        // Sin PostingRule sembrada — fuerza RULE_NOT_FOUND dentro del pipeline.

        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(
                SingleLineCommand(_receivableId, 300m, paymentDate, _customerId),
                CancellationToken.None
            );

        result
            .IsSuccess.Should()
            .BeTrue(because: "el fallo del Posting Engine no debe revertir el registro del cobro");

        await using var verifyDb = CreateContext();
        var receivable = await verifyDb.SalesReceivables.FirstAsync(x => x.Id == _receivableId);
        receivable.PaidAmount.Should().Be(300m);

        var entry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x =>
            x.SourceEventId == result.Value!.Id
        );
        entry.Should().BeNull();
    }

    [Fact]
    public async Task Republicar_CollectionAppliedEvent_no_duplica_JournalEntry_Payment_ni_lineas()
    {
        var paymentDate = new DateOnly(2026, 7, 15);
        var (db, publisher) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(db, paymentDate);

        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(
                SingleLineCommand(_receivableId, 300m, paymentDate, _customerId),
                CancellationToken.None
            );
        result.IsSuccess.Should().BeTrue();
        var paymentId = result.Value!.Id;

        // Republicación manual del mismo evento — simula un reintento de entrega del mismo Domain
        // Event (nunca se vuelve a ejecutar RegisterCollectionCommand).
        var repeated = new CollectionAppliedEvent(
            _tenantId,
            paymentId,
            _companyId,
            _customerId,
            300m,
            paymentDate
        );
        await publisher.Publish(repeated, CancellationToken.None);

        await using var verifyDb = CreateContext();
        var journalEntryCount = await verifyDb.JournalEntries.CountAsync(x =>
            x.SourceEventId == paymentId
        );
        var paymentCount = await verifyDb.Payments.CountAsync(x => x.Id == paymentId);
        var lineCount = await verifyDb.PaymentApplicationLines.CountAsync(x =>
            x.PaymentId == paymentId
        );

        journalEntryCount
            .Should()
            .Be(
                1,
                because: "el Posting Engine ya garantiza idempotencia por SourceEventId (Fase 3.1)"
            );
        paymentCount
            .Should()
            .Be(
                1,
                because: "republicar el evento no crea un segundo Payment — solo el comando lo hace"
            );
        lineCount.Should().Be(1, because: "republicar el evento no duplica PaymentApplicationLine");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Fase 5.6.6 — Reverso de cobros (CollectionReversedEvent → CollectionReversedPostingTranslator)
    // ══════════════════════════════════════════════════════════════════════
    //
    // CollectionReversedEvent no transporta la fecha del cobro original — el traductor fecha el
    // hecho contable con BaseDomainEvent.OccurredOn (fecha real de ejecución). Por eso estos tests
    // usan "hoy" (DateOnly.FromDateTime(DateTime.UtcNow)) tanto para el cobro inicial como para el
    // período sembrado, en vez de una fecha ficticia fija — así ambos asientos (el de aplicación y
    // el de reverso) caen dentro del mismo período sin depender de una fecha hardcodeada.

    [Fact]
    public async Task ReverseCollectionCommand_restaura_PaidAmount_y_BalanceDue_de_SalesReceivable()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedAppliedAndReversedRulesAndPeriodAsync(db, today);

        var registerResult = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(
                SingleLineCommand(_receivableId, 300m, today, _customerId),
                CancellationToken.None
            );
        registerResult.IsSuccess.Should().BeTrue();
        var paymentId = registerResult.Value!.Id;

        var (dbReverse, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        var reverseResult = await BuildReverseHandler(dbReverse, _tenantId, _companyId, _createdBy)
            .Handle(
                new ReverseCollectionCommand(paymentId, "Error de digitación"),
                CancellationToken.None
            );

        reverseResult.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var receivable = await verifyDb.SalesReceivables.FirstAsync(x => x.Id == _receivableId);
        var payment = await verifyDb.Payments.FirstAsync(x => x.Id == paymentId);

        receivable.PaidAmount.Should().Be(0m);
        receivable.BalanceDue.Should().Be(300m);
        payment.Status.Should().Be(PaymentStatus.Reversed);
        payment.ReverseReason.Should().Be("Error de digitación");
    }

    [Fact]
    public async Task ReverseCollectionCommand_con_PostingRule_genera_JournalEntry_de_reverso()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedAppliedAndReversedRulesAndPeriodAsync(db, today);

        var registerResult = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(
                SingleLineCommand(_receivableId, 300m, today, _customerId),
                CancellationToken.None
            );
        registerResult.IsSuccess.Should().BeTrue();
        var paymentId = registerResult.Value!.Id;

        var (dbReverse, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        var reverseResult = await BuildReverseHandler(dbReverse, _tenantId, _companyId, _createdBy)
            .Handle(
                new ReverseCollectionCommand(paymentId, "Error de digitación"),
                CancellationToken.None
            );
        reverseResult.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var appliedEntry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x =>
            x.SourceEventId == paymentId && x.SourceEventType == "CollectionApplied"
        );
        var reversedEntry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstOrDefaultAsync(x =>
                x.SourceEventId == paymentId && x.SourceEventType == "CollectionReversed"
            );

        appliedEntry
            .Should()
            .NotBeNull(because: "el asiento original de aplicación no debe desaparecer");
        reversedEntry.Should().NotBeNull();
        reversedEntry!.SourceModule.Should().Be("Finance");
        reversedEntry.Status.Should().Be(JournalEntryStatus.Posted);
        reversedEntry.Lines.Should().HaveCount(2);
        reversedEntry.Lines.Sum(l => l.Debit).Should().Be(reversedEntry.Lines.Sum(l => l.Credit));
        reversedEntry.Lines.Sum(l => l.Debit).Should().Be(300m);
    }

    [Fact]
    public async Task Republicar_CollectionReversedEvent_no_duplica_el_JournalEntry_de_reverso()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedAppliedAndReversedRulesAndPeriodAsync(db, today);

        var registerResult = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(
                SingleLineCommand(_receivableId, 300m, today, _customerId),
                CancellationToken.None
            );
        registerResult.IsSuccess.Should().BeTrue();
        var paymentId = registerResult.Value!.Id;

        var (dbReverse, publisher) = BuildWiredContext(_tenantId, _companyId, _postgres);
        var reverseResult = await BuildReverseHandler(dbReverse, _tenantId, _companyId, _createdBy)
            .Handle(
                new ReverseCollectionCommand(paymentId, "Error de digitación"),
                CancellationToken.None
            );
        reverseResult.IsSuccess.Should().BeTrue();

        // Republicación manual del mismo evento de reverso — simula un reintento de entrega;
        // nunca se vuelve a ejecutar ReverseCollectionCommand (Payment.Reverse() es de un solo uso).
        var repeated = new CollectionReversedEvent(
            _tenantId,
            paymentId,
            _companyId,
            _customerId,
            300m,
            "Error de digitación"
        );
        await publisher.Publish(repeated, CancellationToken.None);

        await using var verifyDb = CreateContext();
        var reversedEntryCount = await verifyDb.JournalEntries.CountAsync(x =>
            x.SourceEventId == paymentId && x.SourceEventType == "CollectionReversed"
        );
        var payment = await verifyDb.Payments.FirstAsync(x => x.Id == paymentId);

        reversedEntryCount
            .Should()
            .Be(
                1,
                because: "el Posting Engine ya garantiza idempotencia por (CompanyId, SourceModule, SourceEventId, SourceEventType)"
            );
        payment
            .Status.Should()
            .Be(
                PaymentStatus.Reversed,
                because: "republicar el evento no vuelve a ejecutar Payment.Reverse()"
            );
    }

    private sealed class DeferredPublisher : IPublisher
    {
        public IPublisher? Inner { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Inner!.Publish(notification, cancellationToken);

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
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
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
