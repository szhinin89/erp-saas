using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.InitialLoad.OpeningPosting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.InitialLoad;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories.InitialLoad;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Tests.Seeding;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>
/// IL-7C — <see cref="GetOpeningBalanceReconciliationHandler"/> con repositorios reales (InMemory).
/// Los asientos los publica <see cref="PostOpeningBalanceCommandHandler"/> real con un engine
/// sustituto que escribe un <see cref="JournalEntry"/> Posted según la regla sembrada
/// <c>InitialLoad/*</c> (el engine real con sus locks SQL se prueba en IL-7B PostgreSQL). Cubre:
/// inventario/CxC/CxP conciliados, cuenta puente sin reclasificar (OPENING_BRIDGE_NOT_CLEARED), diferencia detectada
/// (lote y mayor), posting pendiente y fallido, aislamiento por empresa, fecha de corte y 0 escrituras.
/// </summary>
public sealed class OpeningBalanceReconciliationTests
{
    private static readonly DateOnly Opening = new(2026, 8, 31);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _branch = Guid.NewGuid();
    private readonly string _database = Guid.NewGuid().ToString();
    private readonly Mock<IPostingEngine> _engine = new();
    private Guid _companyId = Guid.NewGuid();
    private int _entryNumber;

    /// <summary>Lo que el engine sustituto suma al monto del hecho (simula un asiento descuadrado con el submayor).</summary>
    private decimal _engineSkew;

    public OpeningBalanceReconciliationTests()
    {
        _engine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Returns(async (PostingFact f, CancellationToken _) =>
            {
                var id = await WriteRuleEntryAsync(f.FactType, f.SourceEventId, f.EntryDate, f.GrandTotal + _engineSkew);
                return Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(id, PostingOutcomeStatus.Created));
            });
    }

    private ErpDbContext Db(params IInterceptor[] extra) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .UseInMemoryDatabase(_database)
                .AddInterceptors([new NewChildEntityTrackingInterceptor(), .. extra])
                .Options,
            new FixedCurrentTenant(_tenant),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );

    private Mock<IOperationalContext> Context()
    {
        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(x => x.TenantId).Returns(_tenant);
        ctx.SetupGet(x => x.CompanyId).Returns(_companyId);
        ctx.SetupGet(x => x.UserId).Returns(_actor);
        return ctx;
    }

    private async Task<OpeningBalanceReconciliationDto> ReconcileAsync(ErpDbContext? context = null)
    {
        var db = context ?? Db();
        try
        {
            var handler = new GetOpeningBalanceReconciliationHandler(
                Context().Object,
                new ImportBatchRepository(db),
                new OpeningBalancePostingRepository(db),
                new OpeningBalanceConstraintsReader(db, new FixedCurrentCompany(_companyId)),
                new OpeningBalanceSourceReader(db),
                new PostingRuleRepository(db),
                new AccountRepository(db),
                new JournalEntryRepository(db));
            var result = await handler.Handle(new GetOpeningBalanceReconciliationQuery(), CancellationToken.None);
            result.IsSuccess.Should().BeTrue(result.Error);
            return result.Value!;
        }
        finally
        {
            if (context is null)
                await db.DisposeAsync();
        }
    }

    private async Task<Result<OpeningBalancePostingDto>> PostAsync(Guid batchId)
    {
        await using var db = Db();
        var ctx = Context();
        var preflight = new OpeningBalancePostingPreflight(
            ctx.Object,
            new ImportBatchRepository(db),
            new OpeningBalancePostingRepository(db),
            new OpeningBalanceConstraintsReader(db, new FixedCurrentCompany(_companyId)),
            new OpeningBalanceSourceReader(db),
            new PostingPreflight(new PostingRuleRepository(db), new AccountingPeriodRepository(db),
                new AccountRepository(db), NullLogger<PostingEngine>.Instance));
        var handler = new PostOpeningBalanceCommandHandler(ctx.Object, new UnitOfWork(db),
            new OpeningBalancePostingRepository(db), preflight, _engine.Object,
            NullLogger<PostOpeningBalanceCommandHandler>.Instance);
        return await handler.Handle(new PostOpeningBalanceCommand(batchId), CancellationToken.None);
    }

    /// <summary>Asiento Posted con las líneas de la regla InitialLoad/<paramref name="factType"/> de la empresa.</summary>
    private async Task<Guid> WriteRuleEntryAsync(string factType, Guid sourceEventId, DateOnly date, decimal amount)
    {
        await using var db = Db();
        var rule = await db.PostingRules.Include(r => r.Lines)
            .SingleAsync(r => r.SourceModule == "InitialLoad" && r.FactType == factType);
        var lines = rule.Lines.Select(l => l.Nature == AccountNature.Debit
            ? (l.AccountId, amount, 0m)
            : (l.AccountId, 0m, amount));
        return await WriteEntryAsync(db, "InitialLoad", factType, sourceEventId, date, lines);
    }

    private async Task<Guid> WriteManualEntryAsync(DateOnly date, params (string Code, decimal Debit, decimal Credit)[] lines)
    {
        await using var db = Db();
        var accounts = (await db.Accounts.ToListAsync()).ToDictionary(a => a.Code.Value, a => a.Id);
        return await WriteEntryAsync(db, "Manual", "ManualEntry", Guid.NewGuid(), date,
            lines.Select(l => (accounts[l.Code], l.Debit, l.Credit)));
    }

    private async Task<Guid> WriteEntryAsync(ErpDbContext db, string module, string eventType, Guid sourceEventId,
        DateOnly date, IEnumerable<(Guid AccountId, decimal Debit, decimal Credit)> lines)
    {
        var period = await db.AccountingPeriods.FirstAsync();
        var entry = JournalEntry.Create(_tenant, _companyId, date, period.Id, period.FiscalYear, module, eventType,
            sourceEventId, "Asiento de prueba", _actor);
        foreach (var (accountId, debit, credit) in lines)
            entry.AddLine(accountId, null, debit, credit);
        entry.Post(_actor, ++_entryNumber);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private async Task SeedCompanyAsync()
    {
        await using var db = Db();
        var company = Company.CreateManaged(_tenant, $"17900{Random.Shared.Next(10000000, 99999999)}", "Apertura",
            createdBy: _actor);
        company.SetOpeningBalanceDate(Opening, new OpeningBalanceDateConstraints(false, []), _actor);
        db.Companies.Add(company);
        _companyId = company.Id;
        await db.SaveChangesAsync();
        await using var seed = Db();
        await new AccountingBootstrapStep(seed, new AlwaysTodayCompanyClock(), NullLogger<AccountingBootstrapStep>.Instance)
            .ExecuteAsync(new CompanyBootstrapContext(_tenant, _companyId, _actor));
    }

    private ImportBatch CompletedBatch(ImportType type, int rows)
    {
        var batch = ImportBatch.Create(_tenant, _companyId, type, _actor);
        batch.AttachFile("x/file.xlsx", "file.xlsx", 10, _actor);
        batch.MarkUploaded(_actor);
        batch.BeginValidating(_actor);
        batch.CompleteValidation(rows, rows, 0, 0, _actor);
        batch.BeginConfirming(_actor);
        batch.CompleteConfirmation(rows, anyRowsFailed: false, _actor);
        return batch;
    }

    private async Task<Guid> ReceivablesBatchAsync(params decimal[] balances)
    {
        await using var db = Db();
        var batch = CompletedBatch(ImportType.InitialReceivables, balances.Length);
        db.ImportBatches.Add(batch);
        for (var i = 0; i < balances.Length; i++)
            db.SalesReceivables.Add(SalesReceivable.CreateInitialBalance(_tenant, _companyId, _branch, Guid.NewGuid(),
                $"FAC-{i + 1}", Opening.AddDays(-10), Opening.AddDays(20), balances[i], batch.Id, _actor));
        await db.SaveChangesAsync();
        return batch.Id;
    }

    private async Task<Guid> PayablesBatchAsync(decimal amount)
    {
        await using var db = Db();
        var batch = CompletedBatch(ImportType.InitialPayables, 1);
        db.ImportBatches.Add(batch);
        db.AccountsPayables.Add(AccountsPayable.CreateInitialBalance(_tenant, _companyId, _branch, Guid.NewGuid(), "01",
            "001-001-000000001", Opening.AddDays(-40), Opening.AddDays(10), Opening, amount, batch.Id, Guid.NewGuid(), _actor));
        await db.SaveChangesAsync();
        return batch.Id;
    }

    /// <summary>3 × 1.3335 = 4.0005 → 4.00 con el redondeo monetario.</summary>
    private async Task<Guid> StockBatchAsync()
    {
        await using var db = Db();
        var batch = CompletedBatch(ImportType.InitialStock, 1);
        db.ImportBatches.Add(batch);
        var document = Guid.NewGuid();
        var row = ImportBatchRow.Create(_tenant, _companyId, batch.Id, 1, "{}", _actor);
        row.SetParsedData("{}", hasBlockingIssue: false, _actor);
        row.MarkImported(document, _actor);
        db.ImportBatchRows.Add(row);
        db.StockMovements.Add(StockMovement.Create(_tenant, _branch, Guid.NewGuid(), Guid.NewGuid(),
            StockMovementType.InitialBalance, 3m, "UND", 0m, 1, 1.3335m, 4.0005m, Opening, "AJ-1", document,
            "StockAdjustment", _actor, _companyId, unitCost: 1.3335m));
        await db.SaveChangesAsync();
        return batch.Id;
    }

    private static OpeningReconciliationTypeDto Type(OpeningBalanceReconciliationDto r, ImportType type) =>
        r.Types.Single(t => t.ImportType == type);

    [Fact]
    public async Task Inventario_CxC_y_CxP_contabilizados_quedan_conciliados_por_lote_y_contra_el_mayor()
    {
        await SeedCompanyAsync();
        var stock = await StockBatchAsync();
        var receivables = await ReceivablesBatchAsync(100.10m, 49.90m);
        var payables = await PayablesBatchAsync(80.55m);
        foreach (var batch in new[] { stock, receivables, payables })
            (await PostAsync(batch)).IsSuccess.Should().BeTrue();

        var r = await ReconcileAsync();

        r.CutoffDate.Should().Be(Opening);
        r.Batches.Should().HaveCount(3).And.OnlyContain(b =>
            b.Status == OpeningReconciliationStatus.Reconciled && b.Difference == 0m
            && b.PostingStatus == OpeningBalancePostingStatus.Posted && b.JournalEntryId != null
            && b.JournalEntryNumber != null && !b.CanPost && b.ErrorCode == null);
        r.Batches.Single(b => b.ImportBatchId == stock).OperationalAmount.Should().Be(4.00m);
        foreach (var (type, amount, code) in new[]
                 {
                     (ImportType.InitialStock, 4.00m, "1.1.04.001"),
                     (ImportType.InitialReceivables, 150.00m, "1.1.03.001"),
                     (ImportType.InitialPayables, 80.55m, (string?)null),
                 })
        {
            var t = Type(r, type);
            t.Status.Should().Be(OpeningReconciliationStatus.Reconciled);
            t.OperationalAmount.Should().Be(amount);
            t.LedgerBalance.Should().Be(amount);
            t.Difference.Should().Be(0m);
            t.AccountId.Should().NotBeNull();
            if (code is not null)
                t.AccountCode.Should().Be(code);
        }
        r.Status.Should().Be(OpeningReconciliationStatus.Reconciled);
        r.BridgeAccount!.AccountCode.Should().Be("3.1.04.001");
        r.BridgeAccount.FromOpeningPostings.Should().Be(4.00m + 150.00m - 80.55m);
        r.BridgeAccount.BalanceAtCutoff.Should().Be(73.45m);
        r.BridgeAccount.CurrentBalance.Should().Be(73.45m);
        r.BridgeAccount.PendingReclassification.Should().Be(73.45m);
        r.CanCloseImplementation.Should().BeFalse();
        var blocker = r.Blockers.Should().ContainSingle().Which;
        blocker.Code.Should().Be(GetOpeningBalanceReconciliationHandler.OpeningBridgeNotClearedCode);
        blocker.Message.Should().Be("La cuenta de Saldos de apertura mantiene un saldo pendiente de reclasificación.");
    }

    [Fact]
    public async Task Cuenta_puente_reclasificada_a_cero_libera_el_cierre()
    {
        await SeedCompanyAsync();
        (await PostAsync(await ReceivablesBatchAsync(50m))).IsSuccess.Should().BeTrue();
        string equityCode;
        await using (var db = Db())
            equityCode = (await db.Accounts.ToListAsync())
                .Where(a => a.AccountType == AccountType.Equity && a.AllowsPosting && a.Code.Value != "3.1.04.001")
                .Select(a => a.Code.Value).First();

        await WriteManualEntryAsync(Opening, ("3.1.04.001", 50m, 0m), (equityCode, 0m, 50m));
        var r = await ReconcileAsync();

        r.BridgeAccount!.PendingReclassification.Should().Be(0m);
        r.BridgeAccount.FromOpeningPostings.Should().Be(50m);
        r.Blockers.Should().BeEmpty();
        r.CanCloseImplementation.Should().BeTrue();
    }

    [Fact]
    public async Task Diferencia_del_mayor_al_corte_se_detecta_aunque_el_lote_este_conciliado()
    {
        await SeedCompanyAsync();
        (await PostAsync(await ReceivablesBatchAsync(100m))).IsSuccess.Should().BeTrue();
        await WriteManualEntryAsync(Opening, ("1.1.03.001", 7.25m, 0m), ("3.1.04.001", 0m, 7.25m));

        var r = await ReconcileAsync();

        r.Batches.Single().Status.Should().Be(OpeningReconciliationStatus.Reconciled);
        var t = Type(r, ImportType.InitialReceivables);
        t.LedgerBalance.Should().Be(107.25m);
        t.Difference.Should().Be(-7.25m, "diferencia = submayor − mayor");
        t.Status.Should().Be(OpeningReconciliationStatus.Difference);
        r.Status.Should().Be(OpeningReconciliationStatus.Difference);
        r.CanCloseImplementation.Should().BeFalse();
        r.Blockers.Should().Contain(b => b.Code == "RECONCILIATION_DIFFERENCE" && b.ImportBatchId == null
            && b.Message.Contains("-7.25"));
    }

    [Fact]
    public async Task Asiento_del_lote_distinto_del_saldo_operativo_marca_diferencia_por_lote()
    {
        await SeedCompanyAsync();
        var batch = await PayablesBatchAsync(80m);
        _engineSkew = 0.01m;
        (await PostAsync(batch)).IsSuccess.Should().BeTrue();

        var r = await ReconcileAsync();

        var row = r.Batches.Single();
        row.OperationalAmount.Should().Be(80m);
        row.AccountingAmount.Should().Be(80.01m);
        row.Difference.Should().Be(-0.01m);
        row.Status.Should().Be(OpeningReconciliationStatus.Difference);
        Type(r, ImportType.InitialPayables).Status.Should().Be(OpeningReconciliationStatus.Difference);
        r.Blockers.Should().Contain(b => b.Code == "RECONCILIATION_DIFFERENCE" && b.ImportBatchId == batch);
    }

    [Fact]
    public async Task Lote_sin_asiento_queda_pendiente_de_posting_y_contabilizable()
    {
        await SeedCompanyAsync();
        var batch = await StockBatchAsync();

        var r = await ReconcileAsync();

        var row = r.Batches.Single();
        row.Status.Should().Be(OpeningReconciliationStatus.PendingPosting);
        row.PostingStatus.Should().BeNull();
        row.OperationalAmount.Should().Be(4.00m);
        row.AccountingAmount.Should().Be(0m);
        row.Difference.Should().Be(4.00m);
        row.CanPost.Should().BeTrue();
        row.JournalEntryId.Should().BeNull();
        Type(r, ImportType.InitialStock).Status.Should().Be(OpeningReconciliationStatus.PendingPosting);
        r.Status.Should().Be(OpeningReconciliationStatus.PendingPosting);
        r.Blockers.Should().Contain(b => b.Code == "OPENING_POSTING_PENDING" && b.ImportBatchId == batch);
        r.CanCloseImplementation.Should().BeFalse();
    }

    [Fact]
    public async Task Posting_fallido_muestra_el_error_y_permite_reintentar()
    {
        await SeedCompanyAsync();
        var batch = await PayablesBatchAsync(25m);
        await using (var db = Db())
        {
            (await db.PostingRules.SingleAsync(r => r.SourceModule == "InitialLoad" && r.FactType == "OpeningPayables"))
                .Disable(_actor);
            await db.SaveChangesAsync();
        }
        (await PostAsync(batch)).Code.Should().Be("RULE_NOT_FOUND");

        var r = await ReconcileAsync();

        var row = r.Batches.Single();
        row.Status.Should().Be(OpeningReconciliationStatus.PendingPosting);
        row.PostingStatus.Should().Be(OpeningBalancePostingStatus.Failed);
        row.ErrorCode.Should().Be("RULE_NOT_FOUND");
        row.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        row.CanPost.Should().BeTrue();
        Type(r, ImportType.InitialPayables).AccountId.Should().NotBeNull("la cuenta sale de la regla aunque esté inactiva");
        r.Blockers.Should().Contain(b => b.Code == "OPENING_POSTING_FAILED" && b.ImportBatchId == batch);
    }

    [Fact]
    public async Task Solo_concilia_lotes_y_asientos_de_la_empresa_activa()
    {
        await SeedCompanyAsync();
        var companyA = _companyId;
        var batchA = await ReceivablesBatchAsync(10m);
        (await PostAsync(batchA)).IsSuccess.Should().BeTrue();

        await SeedCompanyAsync();
        var batchB = await ReceivablesBatchAsync(999m);
        (await PostAsync(batchB)).IsSuccess.Should().BeTrue();
        await StockBatchAsync();

        _companyId = companyA;
        var r = await ReconcileAsync();

        r.Batches.Should().ContainSingle().Which.ImportBatchId.Should().Be(batchA);
        Type(r, ImportType.InitialReceivables).LedgerBalance.Should().Be(10m);
        Type(r, ImportType.InitialStock).BatchCount.Should().Be(0);
        r.BridgeAccount!.CurrentBalance.Should().Be(10m);
    }

    [Fact]
    public async Task Mayor_se_toma_al_corte_y_la_cuenta_puente_muestra_ademas_su_saldo_actual()
    {
        await SeedCompanyAsync();
        (await PostAsync(await ReceivablesBatchAsync(100m))).IsSuccess.Should().BeTrue();
        await WriteManualEntryAsync(Opening.AddDays(1), ("1.1.03.001", 30m, 0m), ("3.1.04.001", 0m, 30m));

        var r = await ReconcileAsync();

        var t = Type(r, ImportType.InitialReceivables);
        t.LedgerBalance.Should().Be(100m, "los movimientos posteriores al corte no entran en la apertura");
        t.Status.Should().Be(OpeningReconciliationStatus.Reconciled);
        r.BridgeAccount!.BalanceAtCutoff.Should().Be(100m);
        r.BridgeAccount.CurrentBalance.Should().Be(130m);
        r.BridgeAccount.PendingReclassification.Should().Be(130m);
    }

    [Fact]
    public async Task La_consulta_de_conciliacion_no_escribe_nada()
    {
        await SeedCompanyAsync();
        (await PostAsync(await ReceivablesBatchAsync(10m))).IsSuccess.Should().BeTrue();
        var failing = await PayablesBatchAsync(5m);
        await StockBatchAsync();
        await using (var db = Db())
        {
            (await db.PostingRules.SingleAsync(r => r.FactType == "OpeningPayables")).Disable(_actor);
            await db.SaveChangesAsync();
        }
        (await PostAsync(failing)).IsSuccess.Should().BeFalse();

        async Task<string> SnapshotAsync()
        {
            await using var db = Db();
            var postings = await db.OpeningBalancePostings.AsNoTracking()
                .Select(p => $"{p.Id}:{p.Status}:{p.Attempts}:{p.UpdatedAt:O}").OrderBy(x => x).ToListAsync();
            return string.Join("|", postings)
                + $"#{await db.JournalEntries.CountAsync()}#{await db.ImportBatches.CountAsync()}";
        }

        var before = await SnapshotAsync();
        var saves = new SaveChangesCounter();
        await using (var db = Db(saves))
        {
            await ReconcileAsync(db);
            db.ChangeTracker.Entries().Should().NotContain(e =>
                e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
        }

        saves.Count.Should().Be(0);
        (await SnapshotAsync()).Should().Be(before);
        _engine.Verify(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class SaveChangesCounter : SaveChangesInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Count++;
            return base.SavingChanges(eventData, result);
        }
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

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
