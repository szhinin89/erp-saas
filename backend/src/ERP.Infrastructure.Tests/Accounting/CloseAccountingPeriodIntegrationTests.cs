using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;
using ERP.Application.Modules.Accounting.UseCases.Reports;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Tests.Common;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para el cierre real de período
/// (Fase 5.5, ADR-026 §6.1/§9; ACCOUNTING-CONTINUITY-06). Cubre lo que un mock no puede: que el
/// readiness único (<see cref="AccountingCompletenessService"/>) resuelva sus precondiciones contra
/// datos reales (asientos, obligaciones, apertura, orígenes). Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CloseAccountingPeriodIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_close_period_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _createdBy;
    private Guid _debitAccountId;
    private Guid _creditAccountId;

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
        var debitAccount = Account.Create(
            tenant.Id,
            company.Id,
            ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("1.1.01"),
            "Caja",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var creditAccount = Account.Create(
            tenant.Id,
            company.Id,
            ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("4.1.01"),
            "Ventas",
            null,
            AccountType.Income,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Accounts.AddRange(debitAccount, creditAccount);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _debitAccountId = debitAccount.Id;
        _creditAccountId = creditAccount.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private async Task<AccountingPeriod> SeedPeriodAsync()
    {
        await using var db = CreateContext();
        var period = AccountingPeriod.Create(
            _tenantId,
            _companyId,
            2026,
            7,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            _createdBy
        );
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
        return period;
    }

    private async Task<JournalEntry> SeedPostedEntryAsync(AccountingPeriod period)
    {
        await using var db = CreateContext();
        var entryNumber = await new JournalEntrySequenceRepository(db).ReserveNextNumberAsync(
            _tenantId,
            _companyId,
            period.FiscalYear,
            CancellationToken.None
        );

        var entry = JournalEntry.Create(
            _tenantId,
            _companyId,
            new DateOnly(2026, 7, 15),
            period.Id,
            period.FiscalYear,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            "Asiento test",
            _createdBy
        );
        entry.AddLine(_debitAccountId, null, 100m, 0m);
        entry.AddLine(_creditAccountId, null, 0m, 100m);
        entry.Post(_createdBy, entryNumber);

        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private async Task<JournalEntry> SeedDraftEntryAsync(AccountingPeriod period)
    {
        await using var db = CreateContext();
        var entry = JournalEntry.Create(
            _tenantId,
            _companyId,
            new DateOnly(2026, 7, 15),
            period.Id,
            period.FiscalYear,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            "Asiento sin publicar",
            _createdBy
        );

        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static AccountingCompletenessService Completeness(ErpDbContext db, Guid companyId) =>
        ERP.Infrastructure.Tests.TestData.AccountingCompletenessTestSupport.Create(db, companyId);

    private static CloseAccountingPeriodHandler BuildHandler(
        ErpDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid userId
    ) =>
        new(
            new AccountingPeriodRepository(db),
            Completeness(db, companyId),
            new AccountingOperationalPolicyRepository(db),
            new ERP.Infrastructure.Persistence.UnitOfWork(db),
            new ERP.Infrastructure.Tests.Seeding.AlwaysTodayCompanyClock(),
            new FixedCurrentTenant(tenantId),
            new FixedCurrentCompany(companyId),
            new FixedCurrentUser(userId)
        );

    // ACCOUNTING-07: la exclusión ex_accounting_periods_company_no_overlap es la defensa final — dos
    // inserciones concurrentes con rangos solapados y distinto (ejercicio, número): solo una persiste.
    [Fact]
    public async Task A07_PostgreSQL_impide_dos_periodos_solapados_concurrentes()
    {
        async Task<Exception?> InsertAsync(int number, DateOnly start, DateOnly end)
        {
            await using var db = CreateContext();
            db.AccountingPeriods.Add(
                AccountingPeriod.Create(_tenantId, _companyId, 2026, number, start, end, _createdBy)
            );
            try
            {
                await db.SaveChangesAsync();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var results = await Task.WhenAll(
            InsertAsync(8, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            InsertAsync(9, new DateOnly(2026, 8, 20), new DateOnly(2026, 9, 30))
        );

        results.Count(r => r is null).Should().Be(1);
        var failure = results.Single(r => r is not null)!;
        (failure.InnerException as Npgsql.PostgresException ?? failure as Npgsql.PostgresException)!
            .SqlState.Should().Be(Npgsql.PostgresErrorCodes.ExclusionViolation);
        await using var verify = CreateContext();
        (await verify.AccountingPeriods.CountAsync(p => p.CompanyId == _companyId)).Should().Be(1);
    }

    [Fact]
    public async Task Cierre_correcto_cuando_todos_los_asientos_estan_Posted()
    {
        var period = await SeedPeriodAsync();
        await SeedPostedEntryAsync(period);
        await SeedPostedEntryAsync(period);

        await using var db = CreateContext();
        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var loaded = await verifyDb.AccountingPeriods.FirstAsync(x => x.Id == period.Id);
        loaded.Status.Should().Be(PeriodStatus.Closed);
        loaded.ClosedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Cierre_falla_si_existe_un_asiento_Draft_y_el_periodo_permanece_Open()
    {
        var period = await SeedPeriodAsync();
        await SeedPostedEntryAsync(period);
        await SeedDraftEntryAsync(period);

        await using var db = CreateContext();
        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .HandleWithDomainRules(
                new CloseAccountingPeriodCommand(period.Id),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("sin publicar");

        await using var verifyDb = CreateContext();
        var loaded = await verifyDb.AccountingPeriods.FirstAsync(x => x.Id == period.Id);
        loaded.Status.Should().Be(PeriodStatus.Open);
    }

    /// <summary>
    /// ACCOUNTING-CONTINUITY-01 — gate mínimo: una obligación Pending o Failed con EntryDate dentro
    /// del período bloquea el cierre aunque todos los asientos estén Posted; fuera del período no.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cierre_falla_si_existe_una_contabilizacion_Pending_o_Failed_del_periodo(bool failed)
    {
        var period = await SeedPeriodAsync();
        await SeedPostedEntryAsync(period);
        await using (var seed = CreateContext())
        {
            AccountingPosting Obligation(DateOnly date) =>
                AccountingPosting.CreatePending(_tenantId, _companyId, "Purchases", "InvoiceReceived",
                    Guid.NewGuid(), null, date, date, 1, "{}", _createdBy, null);
            var inside = Obligation(new DateOnly(2026, 7, 31));
            if (failed)
                inside.RecordFailed("RULE_NOT_FOUND", "Sin regla", AccountingPostingAttemptTrigger.Initial,
                    _createdBy, null, null, DateTime.UtcNow, DateTime.UtcNow);
            seed.AccountingPostings.AddRange(inside, Obligation(new DateOnly(2026, 8, 1)));
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .HandleWithDomainRules(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("contabilizaciones pendientes o fallidas");
        await using var verifyDb = CreateContext();
        (await verifyDb.AccountingPeriods.FirstAsync(x => x.Id == period.Id))
            .Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public async Task Cierre_correcto_si_las_obligaciones_del_periodo_estan_Posted_o_Canceled_y_las_pendientes_son_de_otro_periodo()
    {
        var period = await SeedPeriodAsync();
        await SeedPostedEntryAsync(period);
        await using (var seed = CreateContext())
        {
            var canceled = AccountingPosting.CreatePending(_tenantId, _companyId, "Purchases", "InvoiceReceived",
                Guid.NewGuid(), null, new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 10), 1, "{}", _createdBy, null);
            canceled.Cancel(_createdBy);
            var nextPeriod = AccountingPosting.CreatePending(_tenantId, _companyId, "Purchases", "InvoiceReceived",
                Guid.NewGuid(), null, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 1), 1, "{}", _createdBy, null);
            seed.AccountingPostings.AddRange(canceled, nextPeriod);
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task Cierre_correcto_con_un_original_y_su_reverso_completo()
    {
        var period = await SeedPeriodAsync();
        var original = await SeedPostedEntryAsync(period);

        await using (var db = CreateContext())
        {
            var entryNumber = await new JournalEntrySequenceRepository(db).ReserveNextNumberAsync(
                _tenantId,
                _companyId,
                period.FiscalYear,
                CancellationToken.None
            );
            var tracked = await db
                .JournalEntries.Include(x => x.Lines)
                .FirstAsync(x => x.Id == original.Id);
            var reversal = tracked.Reverse(_createdBy, entryNumber, "Ajuste de prueba", tracked.EntryDate, tracked.AccountingPeriodId, tracked.FiscalYear);
            db.JournalEntries.Add(reversal);
            await db.SaveChangesAsync();
        }

        await using var closeDb = CreateContext();
        var result = await BuildHandler(closeDb, _tenantId, _companyId, _createdBy)
            .Handle(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result
            .IsSuccess.Should()
            .BeTrue(
                because: "el original sigue Posted (inmutable) y su reverso quedó Posted y numerado: el cierre está completo"
            );

        await using var verifyDb = CreateContext();
        var loaded = await verifyDb.AccountingPeriods.FirstAsync(x => x.Id == period.Id);
        loaded.Status.Should().Be(PeriodStatus.Closed);
    }

    // ── ACCOUNTING-CONTINUITY-06 ──────────────────────────────────────────

    private static ImportBatch CompletedStockBatch(Guid tenantId, Guid companyId, Guid user)
    {
        var batch = ImportBatch.Create(tenantId, companyId, ImportType.InitialStock, user);
        batch.AttachFile("x.xlsx", "x.xlsx", 1, user);
        batch.MarkUploaded(user);
        batch.BeginValidating(user);
        batch.CompleteValidation(1, 1, 0, 0, user);
        batch.BeginConfirming(user);
        batch.CompleteConfirmation(1, anyRowsFailed: false, user);
        return batch;
    }

    /// <summary>Política Enabled adoptada antes del período de prueba (julio 2026).</summary>
    private async Task SeedEnabledAdoptedBeforeAsync(ErpDbContext db)
    {
        var policy = AccountingOperationalPolicy.Create(_tenantId, _companyId, AccountingOperationalMode.Enabled,
            new DateOnly(1900, 1, 1), "test", _createdBy);
        db.AccountingOperationalPolicies.Add(policy);
        db.Entry(policy).Property(x => x.CreatedAt).CurrentValue = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task C06_apertura_sujeta_con_ASI_ausente_y_lote_sin_asiento_bloquea_el_cierre_del_periodo_de_apertura()
    {
        var period = await SeedPeriodAsync();
        await using (var seed = CreateContext())
        {
            await SeedEnabledAdoptedBeforeAsync(seed);
            var company = await seed.Companies.FirstAsync(c => c.Id == _companyId);
            company.SetOpeningBalanceDate(new DateOnly(2026, 7, 1), new OpeningBalanceDateConstraints(false, []), _createdBy);
            await seed.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var asiOnly = await Completeness(db, _companyId).EvaluateClosureAsync(_tenantId, _companyId, period, default);
            asiOnly.Reasons.Should().BeEquivalentTo([new AccountingCompletenessReason(AccountingCompletenessCodes.OpeningAsiMissing, 1)]);
        }

        await using (var seed = CreateContext())
        {
            seed.ImportBatches.Add(CompletedStockBatch(_tenantId, _companyId, _createdBy));
            await seed.SaveChangesAsync();
        }

        await using var closeDb = CreateContext();
        var readiness = await Completeness(closeDb, _companyId).EvaluateClosureAsync(_tenantId, _companyId, period, default);
        readiness.Reasons.Should().Contain(new AccountingCompletenessReason(AccountingCompletenessCodes.OpeningBatchesUnposted, 1));
        var result = await BuildHandler(closeDb, _tenantId, _companyId, _createdBy)
            .HandleWithDomainRules(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("apertura contable");
        await using var verifyDb = CreateContext();
        (await verifyDb.AccountingPeriods.FirstAsync(x => x.Id == period.Id)).Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public async Task C06_origen_sujeto_sin_obligacion_ni_asiento_bloquea_el_cierre_hasta_tener_identidad_contable()
    {
        var period = await SeedPeriodAsync();
        Guid paymentId;
        await using (var seed = CreateContext())
        {
            await SeedEnabledAdoptedBeforeAsync(seed);
            var partner = ERP.Domain.MasterData.Entities.BusinessPartner.Create(
                _tenantId, "04", "1790016919001", null, "Cliente C06", _createdBy);
            seed.BusinessPartners.Add(partner);
            await seed.SaveChangesAsync();
            var payment = ERP.Domain.Modules.Finance.Entities.Payment.Create(_tenantId, _companyId,
                ERP.Domain.Modules.Finance.Enums.PaymentDirection.Collection, partner.Id, 10m,
                new DateOnly(2026, 7, 10), null, "C06", _createdBy);
            seed.Payments.Add(payment);
            seed.Entry(payment).Property(x => x.Status).CurrentValue = ERP.Domain.Modules.Finance.Enums.PaymentStatus.Applied;
            await seed.SaveChangesAsync();
            paymentId = payment.Id;
        }

        await using (var db = CreateContext())
        {
            var blocked = await BuildHandler(db, _tenantId, _companyId, _createdBy)
                .HandleWithDomainRules(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);
            blocked.IsSuccess.Should().BeFalse();
            blocked.Error.Should().Contain("sin contabilización registrada");
        }

        await using (var seed = CreateContext())
        {
            var canceled = AccountingPosting.CreatePending(_tenantId, _companyId, "Finance", "CollectionApplied",
                paymentId, null, new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 10), 2, "{}", _createdBy, null);
            canceled.Cancel(_createdBy);
            seed.AccountingPostings.Add(canceled);
            await seed.SaveChangesAsync();
        }

        await using var closeDb = CreateContext();
        var closed = await BuildHandler(closeDb, _tenantId, _companyId, _createdBy)
            .Handle(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);
        closed.IsSuccess.Should().BeTrue(closed.Error);
    }

    [Fact]
    public async Task C06_reporte_con_obligacion_Failed_es_incompleto_y_completo_al_resolverla()
    {
        await SeedPeriodAsync();
        var posting = AccountingPosting.CreatePending(_tenantId, _companyId, "Purchases", "InvoiceReceived",
            Guid.NewGuid(), null, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 20), 2, "{}", _createdBy, null);
        posting.RecordFailed("RULE_NOT_FOUND", "Sin regla", AccountingPostingAttemptTrigger.Initial,
            _createdBy, null, null, DateTime.UtcNow, DateTime.UtcNow);
        await using (var seed = CreateContext())
        {
            seed.AccountingPostings.Add(posting);
            await seed.SaveChangesAsync();
        }
        var query = new GetTrialBalanceReportQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        async Task<AccountingCompleteness> TrialBalanceCompletenessAsync()
        {
            await using var db = CreateContext();
            var result = await new GetTrialBalanceReportHandler(
                new JournalEntryRepository(db), new AccountRepository(db), new FixedCurrentTenant(_tenantId),
                new FixedCurrentCompany(_companyId), Completeness(db, _companyId)).Handle(query, default);
            return result.Value!.Completeness;
        }

        var incomplete = await TrialBalanceCompletenessAsync();
        incomplete.IsComplete.Should().BeFalse();
        incomplete.AsOf.Should().Be(new DateOnly(2026, 7, 31));
        incomplete.Reasons.Should().BeEquivalentTo([new AccountingCompletenessReason(AccountingCompletenessCodes.PostingsUnresolved, 1)]);

        await using (var resolve = CreateContext())
        {
            var tracked = await resolve.AccountingPostings.FirstAsync(x => x.Id == posting.Id);
            tracked.Cancel(_createdBy);
            await resolve.SaveChangesAsync();
        }

        (await TrialBalanceCompletenessAsync()).IsComplete.Should().BeTrue();
    }

    // ── ACCOUNTING-CONTINUITY-06: cobertura completa de orígenes ─────────

    public enum OriginKind { PurchaseReturn, Retention, SupplierCreditRefund }

    /// <summary>
    /// Siembra el origen en el estado que genera contabilidad, con fecha en julio 2026, sin obligación ni
    /// asiento. Solo la conciliación se prueba aquí: las FKs a documentos relacionados (factura, sucursal,
    /// proveedor, punto de emisión) se omiten en esta sesión con <c>session_replication_role = replica</c>.
    /// </summary>
    private async Task SeedOriginWithoutAccountingAsync(OriginKind kind)
    {
        await using var db = CreateContext();
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("SET session_replication_role = replica");
        switch (kind)
        {
            case OriginKind.PurchaseReturn:
                var purchaseReturn = ERP.Domain.Modules.Purchases.Entities.PurchaseReturn.CreateDraft(
                    _tenantId, _companyId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "C06",
                    [new(Guid.NewGuid(), Guid.NewGuid(), 1m, Guid.NewGuid())],
                    _createdBy, Guid.NewGuid(), "hash");
                db.PurchaseReturns.Add(purchaseReturn);
                db.Entry(purchaseReturn).Property(x => x.Status).CurrentValue =
                    ERP.Domain.Modules.Purchases.Enums.PurchaseReturnStatus.Authorized;
                db.Entry(purchaseReturn).Property(x => x.AuthorizedAtUtc).CurrentValue =
                    new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc);
                break;
            case OriginKind.Retention:
                var retention = ERP.Domain.Modules.Retentions.Entities.RetentionDocument.Create(
                    _tenantId, _companyId, Guid.NewGuid(),
                    ERP.Domain.Modules.Retentions.Enums.RetentionSourceDocumentType.ExpenseDocument,
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _createdBy);
                db.RetentionDocuments.Add(retention);
                db.Entry(retention).Property(x => x.Status).CurrentValue =
                    ERP.Domain.Modules.Retentions.Enums.RetentionStatus.Issued;
                db.Entry(retention).Property(x => x.IssueDate).CurrentValue = new DateOnly(2026, 7, 12);
                break;
            case OriginKind.SupplierCreditRefund:
                db.SupplierCreditRefundTransactions.Add(
                    ERP.Domain.Modules.Finance.Entities.SupplierCreditRefundTransaction.CreateReceived(
                        _tenantId, _companyId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
                        Guid.NewGuid(), null, null, "CAJA", "Caja", "CashRegister", "01", 10m, "USD",
                        new DateOnly(2026, 7, 14), _createdBy, Guid.NewGuid(), "hash"));
                break;
        }
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(OriginKind.PurchaseReturn)]
    [InlineData(OriginKind.Retention)]
    [InlineData(OriginKind.SupplierCreditRefund)]
    public async Task C06_origen_forward_sin_obligacion_ni_asiento_es_ORIGINS_WITHOUT_ACCOUNTING(OriginKind kind)
    {
        var period = await SeedPeriodAsync();
        await using (var seed = CreateContext())
            await SeedEnabledAdoptedBeforeAsync(seed);
        await SeedOriginWithoutAccountingAsync(kind);

        await using var db = CreateContext();
        var result = await Completeness(db, _companyId).EvaluateAsync(_tenantId, _companyId, period.StartDate, period.EndDate, default);

        result.Reasons.Should().BeEquivalentTo([new AccountingCompletenessReason(AccountingCompletenessCodes.OriginsWithoutAccounting, 1)]);
    }

    [Fact]
    public async Task C06_mismos_origenes_en_Disabled_o_antes_de_la_adopcion_no_bloquean()
    {
        var period = await SeedPeriodAsync();
        await using (var seed = CreateContext())
        {
            // Ventana sujeta = 11/07 (adopción) .. 12/07; desde el 13/07, Disabled.
            var enabled = AccountingOperationalPolicy.Create(_tenantId, _companyId, AccountingOperationalMode.Enabled,
                new DateOnly(1900, 1, 1), "test", _createdBy);
            enabled.EndBefore(new DateOnly(2026, 7, 13), _createdBy);
            var disabled = AccountingOperationalPolicy.Create(_tenantId, _companyId, AccountingOperationalMode.Disabled,
                new DateOnly(2026, 7, 13), "test", _createdBy);
            seed.AccountingOperationalPolicies.AddRange(enabled, disabled);
            seed.Entry(enabled).Property(x => x.CreatedAt).CurrentValue = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);
            await seed.SaveChangesAsync();
        }
        await SeedOriginWithoutAccountingAsync(OriginKind.PurchaseReturn);
        await SeedOriginWithoutAccountingAsync(OriginKind.SupplierCreditRefund);

        await using var db = CreateContext();
        var result = await Completeness(db, _companyId).EvaluateAsync(_tenantId, _companyId, period.StartDate, period.EndDate, default);

        result.IsComplete.Should().BeTrue(
            "la devolución (10/07) es anterior a la adopción y el reembolso (14/07) cae en Disabled");
    }

    [Fact]
    public async Task C06_reader_interpreta_la_fecha_de_negocio_en_la_zona_horaria_de_la_empresa()
    {
        // Empresa en America/Guayaquil (UTC−5): 01/08 02:00 UTC es todavía 31/07 21:00 en la empresa.
        await using (var db = CreateContext())
        {
            (await db.Companies.SingleAsync(c => c.Id == _companyId)).Timezone.Should().Be("America/Guayaquil");
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("SET session_replication_role = replica");
            var purchaseReturn = ERP.Domain.Modules.Purchases.Entities.PurchaseReturn.CreateDraft(
                _tenantId, _companyId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "C06",
                [new(Guid.NewGuid(), Guid.NewGuid(), 1m, Guid.NewGuid())], _createdBy, Guid.NewGuid(), "hash");
            db.PurchaseReturns.Add(purchaseReturn);
            db.Entry(purchaseReturn).Property(x => x.Status).CurrentValue =
                ERP.Domain.Modules.Purchases.Enums.PurchaseReturnStatus.Authorized;
            db.Entry(purchaseReturn).Property(x => x.CreatedAt).CurrentValue =
                new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        }

        await using var readDb = CreateContext();
        var reader = new AccountingRelevantFactsReader(readDb, new ERP.Infrastructure.Persistence.Services.CompanyClock(readDb));

        (await reader.ExistsOnOrAfterAsync(_tenantId, _companyId, new DateOnly(2026, 8, 1), default))
            .Should().BeFalse("el hecho ocurrió el 31/07 en la zona horaria de la empresa");
        (await reader.ExistsOnOrAfterAsync(_tenantId, _companyId, new DateOnly(2026, 7, 31), default))
            .Should().BeTrue();
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
