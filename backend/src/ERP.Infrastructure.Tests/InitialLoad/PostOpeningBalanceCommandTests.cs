using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
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
/// IL-7B — <see cref="PostOpeningBalanceCommandHandler"/> con preflight, repositorios y UoW reales
/// (InMemory) y el Posting Engine sustituido (sus locks son SQL de PostgreSQL; el engine real, la
/// concurrencia y la unicidad del asiento se prueban en <c>PostOpeningBalancePostgreSqlTests</c>).
/// Cubre: los 3 tipos de apertura, Posted idempotente, Failed persistido y reintentable,
/// PERIOD_NOT_OPEN/regla faltante sin llamar al engine, backfill de lote histórico (monto original
/// aunque haya cobros posteriores al corte), cambio de fuente y reverso bloqueado.
/// </summary>
public sealed class PostOpeningBalanceCommandTests
{
    private static readonly DateOnly Opening = new(2026, 8, 31);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _branch = Guid.NewGuid();
    private readonly string _database = Guid.NewGuid().ToString();
    private readonly Mock<IPostingEngine> _engine = new();
    private readonly List<PostingFact> _posted = [];
    private Guid _companyId = Guid.NewGuid();

    public PostOpeningBalanceCommandTests()
    {
        _engine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostingFact f, CancellationToken _) =>
            {
                _posted.Add(f);
                return Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created));
            });
    }

    private ErpDbContext Db() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .UseInMemoryDatabase(_database)
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(_tenant),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );

    private async Task<Result<OpeningBalancePostingDto>> PostAsync(Guid batchId)
    {
        await using var db = Db();
        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(x => x.TenantId).Returns(_tenant);
        ctx.SetupGet(x => x.CompanyId).Returns(_companyId);
        ctx.SetupGet(x => x.UserId).Returns(_actor);
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

    private async Task<OpeningBalancePosting?> StateAsync(Guid batchId)
    {
        await using var db = Db();
        return await db.OpeningBalancePostings.AsNoTracking().SingleOrDefaultAsync(p => p.ImportBatchId == batchId);
    }

    private async Task SeedCompanyAsync(DateOnly? openingBalanceDate = null)
    {
        await using var db = Db();
        var company = Company.CreateManaged(_tenant, "1790012345001", "Apertura", createdBy: _actor);
        company.SetOpeningBalanceDate(openingBalanceDate ?? Opening, new OpeningBalanceDateConstraints(false, []), _actor);
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

    [Fact]
    public async Task Las_tres_aperturas_se_contabilizan_un_asiento_por_lote_con_el_hecho_del_preflight()
    {
        await SeedCompanyAsync();
        var stock = await StockBatchAsync();
        var receivables = await ReceivablesBatchAsync(100.10m, 49.90m);
        var payables = await PayablesBatchAsync(80.55m);

        foreach (var (batch, factType, amount) in new[]
                 {
                     (stock, "OpeningInventory", 4.00m),
                     (receivables, "OpeningReceivables", 150.00m),
                     (payables, "OpeningPayables", 80.55m),
                 })
        {
            var result = await PostAsync(batch);

            result.IsSuccess.Should().BeTrue(result.Error);
            result.Value!.Status.Should().Be(OpeningBalancePostingStatus.Posted);
            result.Value.AlreadyPosted.Should().BeFalse();
            result.Value.JournalEntryId.Should().NotBeNull();
            result.Value.Amount.Should().Be(amount);
            var fact = _posted.Last();
            fact.SourceModule.Should().Be("InitialLoad");
            fact.FactType.Should().Be(factType);
            fact.SourceEventId.Should().Be(batch);
            fact.EntryDate.Should().Be(Opening);
            fact.GrandTotal.Should().Be(amount);
            var state = await StateAsync(batch);
            state!.Status.Should().Be(OpeningBalancePostingStatus.Posted);
            state.JournalEntryId.Should().Be(result.Value.JournalEntryId);
            state.PostedAt.Should().NotBeNull();
            state.Attempts.Should().Be(1);
        }
        _posted.Should().HaveCount(3);
    }

    [Fact]
    public async Task Posted_es_idempotente_devuelve_el_resultado_existente_sin_volver_al_engine()
    {
        await SeedCompanyAsync();
        var batch = await ReceivablesBatchAsync(10m);
        var first = await PostAsync(batch);

        var second = await PostAsync(batch);

        second.IsSuccess.Should().BeTrue();
        second.Value!.AlreadyPosted.Should().BeTrue();
        second.Value.JournalEntryId.Should().Be(first.Value!.JournalEntryId);
        _posted.Should().HaveCount(1);
        (await StateAsync(batch))!.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task PERIOD_NOT_OPEN_queda_Failed_sin_llamar_al_engine_ni_tocar_la_carga_operativa()
    {
        await SeedCompanyAsync(openingBalanceDate: new DateOnly(2025, 12, 31));
        var batch = await ReceivablesBatchAsync(10m);

        var result = await PostAsync(batch);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("PERIOD_NOT_OPEN");
        _posted.Should().BeEmpty();
        var state = await StateAsync(batch);
        state!.Status.Should().Be(OpeningBalancePostingStatus.Failed);
        state.ErrorCode.Should().Be("PERIOD_NOT_OPEN");
        state.Attempts.Should().Be(1);
        state.JournalEntryId.Should().BeNull();
        await using var db = Db();
        (await db.SalesReceivables.CountAsync(r => r.ImportBatchId == batch)).Should().Be(1);
        (await db.ImportBatches.SingleAsync(b => b.Id == batch)).Status.Should().Be(ImportStatus.Completed);
    }

    [Fact]
    public async Task Regla_faltante_queda_Failed_y_el_reintento_tras_corregirla_contabiliza()
    {
        await SeedCompanyAsync();
        var batch = await PayablesBatchAsync(25m);
        await using (var db = Db())
        {
            (await db.PostingRules.SingleAsync(r => r.SourceModule == "InitialLoad" && r.FactType == "OpeningPayables"))
                .Disable(_actor);
            await db.SaveChangesAsync();
        }

        var failed = await PostAsync(batch);
        failed.Code.Should().Be("RULE_NOT_FOUND");
        (await StateAsync(batch))!.Status.Should().Be(OpeningBalancePostingStatus.Failed);
        _posted.Should().BeEmpty();

        await using (var db = Db())
        {
            (await db.PostingRules.SingleAsync(r => r.SourceModule == "InitialLoad" && r.FactType == "OpeningPayables"))
                .Enable(_actor);
            await db.SaveChangesAsync();
        }
        var retried = await PostAsync(batch);

        retried.IsSuccess.Should().BeTrue(retried.Error);
        var state = await StateAsync(batch);
        state!.Status.Should().Be(OpeningBalancePostingStatus.Posted);
        state.ErrorCode.Should().BeNull();
        state.Attempts.Should().Be(2);
        _posted.Should().ContainSingle();
    }

    [Fact]
    public async Task Fallo_del_engine_queda_Failed_y_es_reintentable()
    {
        await SeedCompanyAsync();
        var batch = await ReceivablesBatchAsync(10m);
        _engine
            .SetupSequence(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PostingOutcomeDto>.ValidationFailure("período cerrado en carrera", "PERIOD_NOT_OPEN"))
            .ReturnsAsync(Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)));

        (await PostAsync(batch)).Code.Should().Be("PERIOD_NOT_OPEN");
        (await StateAsync(batch))!.Status.Should().Be(OpeningBalancePostingStatus.Failed);

        (await PostAsync(batch)).IsSuccess.Should().BeTrue();
        (await StateAsync(batch))!.Status.Should().Be(OpeningBalancePostingStatus.Posted);
    }

    [Fact]
    public async Task Asiento_ya_existente_en_el_engine_se_enlaza_sin_duplicar()
    {
        await SeedCompanyAsync();
        var batch = await ReceivablesBatchAsync(10m);
        var existing = Guid.NewGuid();
        _engine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(existing, PostingOutcomeStatus.AlreadyProcessed)));

        var result = await PostAsync(batch);

        result.Value!.JournalEntryId.Should().Be(existing);
        (await StateAsync(batch))!.Status.Should().Be(OpeningBalancePostingStatus.Posted);
    }

    [Fact]
    public async Task Backfill_de_lote_historico_usa_el_monto_original_aunque_haya_cobros_posteriores_al_corte()
    {
        await SeedCompanyAsync();
        var batch = await ReceivablesBatchAsync(100m);
        await using (var db = Db())
        {
            (await db.SalesReceivables.SingleAsync(r => r.ImportBatchId == batch)).RegisterCollection(60m, _actor);
            await db.SaveChangesAsync();
        }

        var result = await PostAsync(batch);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Amount.Should().Be(100m, "la apertura es el saldo al corte, no el saldo actual");
        _posted.Single().GrandTotal.Should().Be(100m);
    }

    [Fact]
    public async Task Estado_guardado_distinto_de_la_fuente_confirmada_no_se_contabiliza()
    {
        await SeedCompanyAsync();
        var batch = await ReceivablesBatchAsync(10m);
        await using (var db = Db())
        {
            var stale = OpeningBalancePosting.CreatePending(_tenant, _companyId, batch, ImportType.InitialReceivables,
                Opening, 99m, _actor);
            stale.MarkFailed("PERIOD_NOT_OPEN", "x", _actor);
            db.OpeningBalancePostings.Add(stale);
            await db.SaveChangesAsync();
        }

        var result = await PostAsync(batch);

        result.Code.Should().Be(PostOpeningBalanceCommandHandler.SourceChangedCode);
        _posted.Should().BeEmpty();
        (await StateAsync(batch))!.ErrorCode.Should().Be(PostOpeningBalanceCommandHandler.SourceChangedCode);
    }

    [Fact]
    public async Task Lote_inexistente_o_de_otra_empresa_no_deja_estado()
    {
        await SeedCompanyAsync();
        var batch = await ReceivablesBatchAsync(10m);
        _companyId = Guid.NewGuid();

        var result = await PostAsync(batch);

        result.IsSuccess.Should().BeFalse();
        _posted.Should().BeEmpty();
        await using var db = Db();
        (await db.OpeningBalancePostings.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Reverso_del_asiento_de_apertura_se_rechaza_con_ReverseJournalEntryCommand()
    {
        await SeedCompanyAsync();
        Guid entryId;
        await using (var db = Db())
        {
            var period = await db.AccountingPeriods.SingleAsync();
            var accounts = await db.Accounts.ToDictionaryAsync(a => a.Code.Value, a => a.Id);
            var entry = JournalEntry.Create(_tenant, _companyId, Opening, period.Id, period.FiscalYear, "InitialLoad",
                "OpeningReceivables", Guid.NewGuid(), "Apertura", _actor);
            entry.AddLine(accounts["1.1.03.001"], null, 10m, 0m);
            entry.AddLine(accounts["3.1.04.001"], null, 0m, 10m);
            entry.Post(_actor, 1);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();
            entryId = entry.Id;
        }

        await using var context = Db();
        var handler = new ReverseJournalEntryCommandHandler(
            new JournalEntryRepository(context),
            new AccountingPeriodRepository(context),
            new JournalEntrySequenceRepository(context),
            new FixedCurrentTenant(_tenant),
            new FixedCurrentCompany(_companyId),
            Mock.Of<ICurrentUser>(u => u.UserId == _actor));
        var result = await handler.Handle(new ReverseJournalEntryCommand(entryId, "corrección"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ReverseJournalEntryCommandHandler.OpeningReversalNotAllowedCode);
        await using var check = Db();
        (await check.JournalEntries.CountAsync()).Should().Be(1, "no se crea asiento de reverso");
        (await check.JournalEntries.SingleAsync()).Status.Should().Be(JournalEntryStatus.Posted);
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
