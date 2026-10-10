using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.InitialLoad.OpeningPosting;
using ERP.Domain.Modules.Accounting.ValueObjects;
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
/// IL-7A — validaciones previas del asiento de apertura por lote, sin generarlo: monto tomado de
/// los datos confirmados del dominio (nunca staging) y redondeado a 2 decimales, fecha =
/// Company.OpeningBalanceDate, y el mismo pipeline del Posting Engine en seco (regla, período
/// abierto, cuentas postables, partida doble). Nada se persiste: ni asiento ni número.
/// </summary>
public sealed class OpeningBalancePostingPreflightTests
{
    private static readonly DateOnly Opening = new(2026, 8, 31);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _branch = Guid.NewGuid();
    private readonly string _database = Guid.NewGuid().ToString();
    private Guid _companyId = Guid.NewGuid();

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

    private OpeningBalancePostingPreflight Preflight(ErpDbContext db)
    {
        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(x => x.TenantId).Returns(_tenant);
        ctx.SetupGet(x => x.CompanyId).Returns(_companyId);
        ctx.SetupGet(x => x.UserId).Returns(_actor);
        return new OpeningBalancePostingPreflight(
            ctx.Object,
            new ImportBatchRepository(db),
            new OpeningBalancePostingRepository(db),
            new OpeningBalanceConstraintsReader(db, new FixedCurrentCompany(_companyId)),
            new OpeningBalanceSourceReader(db),
            new PostingPreflight(
                new PostingRuleRepository(db),
                new AccountingPeriodRepository(db),
                new AccountRepository(db),
                NullLogger<PostingEngine>.Instance
            )
        );
    }

    private async Task<OpeningBalancePostingPreflightResult> CheckAsync(Guid batchId)
    {
        await using var db = Db();
        return await Preflight(db).CheckAsync(batchId, CancellationToken.None);
    }

    /// <summary>Empresa con plan/reglas/período sembrados por el bootstrap real y fecha de apertura.</summary>
    private async Task SeedCompanyAsync(DateOnly? openingBalanceDate = null, bool withOpeningDate = true)
    {
        await using var db = Db();
        var company = Company.CreateManaged(_tenant, "1790012345001", "Apertura", createdBy: _actor);
        if (withOpeningDate)
            company.SetOpeningBalanceDate(
                openingBalanceDate ?? Opening,
                new OpeningBalanceDateConstraints(false, []),
                _actor
            );
        db.Companies.Add(company);
        _companyId = company.Id;
        await db.SaveChangesAsync();

        await using var seed = Db();
        await new AccountingBootstrapStep(seed, new AlwaysTodayCompanyClock(), NullLogger<AccountingBootstrapStep>.Instance)
            .ExecuteAsync(new CompanyBootstrapContext(_tenant, _companyId, _actor));
    }

    private ImportBatch CompletedBatch(ImportType type, int rows, bool complete = true)
    {
        var batch = ImportBatch.Create(_tenant, _companyId, type, _actor);
        batch.AttachFile("x/file.xlsx", "file.xlsx", 10, _actor);
        batch.MarkUploaded(_actor);
        batch.BeginValidating(_actor);
        batch.CompleteValidation(rows, rows, 0, 0, _actor);
        if (complete)
        {
            batch.BeginConfirming(_actor);
            batch.CompleteConfirmation(rows, anyRowsFailed: false, _actor);
        }
        return batch;
    }

    private async Task<Guid> SeedReceivablesBatchAsync(params decimal[] balances)
    {
        await using var db = Db();
        var batch = CompletedBatch(ImportType.InitialReceivables, balances.Length);
        db.ImportBatches.Add(batch);
        for (var i = 0; i < balances.Length; i++)
            db.SalesReceivables.Add(
                SalesReceivable.CreateInitialBalance(_tenant, _companyId, _branch, Guid.NewGuid(),
                    $"FAC-{i + 1}", Opening.AddDays(-10), Opening.AddDays(20), balances[i], batch.Id, _actor));
        await db.SaveChangesAsync();
        return batch.Id;
    }

    [Fact]
    public async Task CxC_confirmada_lista_con_monto_del_dominio_fecha_de_apertura_y_sin_persistir_nada()
    {
        await SeedCompanyAsync();
        var batchId = await SeedReceivablesBatchAsync(100.10m, 49.90m);

        var result = await CheckAsync(batchId);

        result.Issues.Should().BeEmpty();
        result.IsReady.Should().BeTrue();
        result.CurrentStatus.Should().BeNull();
        result.Fact!.SourceModule.Should().Be("InitialLoad");
        result.Fact.FactType.Should().Be("OpeningReceivables");
        result.Fact.SourceEventId.Should().Be(batchId);
        result.Fact.EntryDate.Should().Be(Opening);
        result.Fact.GrandTotal.Should().Be(150.00m);

        await using var db = Db();
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.OpeningBalancePostings.CountAsync()).Should().Be(0);
        (await db.Set<ERP.Domain.Modules.Accounting.Entities.JournalEntrySequence>().CountAsync())
            .Should().Be(0, "el preflight nunca reserva numeración");
    }

    [Fact]
    public async Task Inventario_suma_costo_del_kardex_inicial_del_lote_y_redondea_away_from_zero()
    {
        await SeedCompanyAsync();
        Guid batchId;
        await using (var db = Db())
        {
            var batch = CompletedBatch(ImportType.InitialStock, 2);
            db.ImportBatches.Add(batch);
            var document = Guid.NewGuid();
            for (var i = 1; i <= 2; i++)
            {
                var row = ImportBatchRow.Create(_tenant, _companyId, batch.Id, i, "{}", _actor);
                row.SetParsedData("{}", hasBlockingIssue: false, _actor);
                row.MarkImported(document, _actor);
                db.ImportBatchRows.Add(row);
            }
            // 3 × 1.3335 = 4.0005 ; 2 × 0.0025 = 0.005 → 4.0055 → 4.01 (nunca truncado).
            db.StockMovements.Add(Movement(document, 3m, 1.3335m, Opening));
            db.StockMovements.Add(Movement(document, 2m, 0.0025m, Opening));
            // Otro documento (fuera del lote) no cuenta.
            db.StockMovements.Add(Movement(Guid.NewGuid(), 5m, 10m, Opening));
            await db.SaveChangesAsync();
            batchId = batch.Id;
        }

        var result = await CheckAsync(batchId);

        result.Issues.Should().BeEmpty();
        result.Fact!.FactType.Should().Be("OpeningInventory");
        result.Fact.GrandTotal.Should().Be(4.01m);
    }

    private StockMovement Movement(Guid document, decimal qty, decimal unitCost, DateOnly date) =>
        StockMovement.Create(_tenant, _branch, Guid.NewGuid(), Guid.NewGuid(), StockMovementType.InitialBalance,
            qty, "UND", 0m, 1, unitCost, qty * unitCost, date, "AJ-1", document, "StockAdjustment", _actor,
            _companyId, unitCost: unitCost);

    [Fact]
    public async Task CxP_con_corte_distinto_a_la_fecha_de_apertura_se_bloquea()
    {
        await SeedCompanyAsync();
        Guid batchId;
        await using (var db = Db())
        {
            var batch = CompletedBatch(ImportType.InitialPayables, 1);
            db.ImportBatches.Add(batch);
            db.AccountsPayables.Add(AccountsPayable.CreateInitialBalance(_tenant, _companyId, _branch, Guid.NewGuid(),
                "01", "001-001-000000001", Opening.AddDays(-40), Opening.AddDays(10), Opening.AddDays(-1),
                80m, batch.Id, Guid.NewGuid(), _actor));
            await db.SaveChangesAsync();
            batchId = batch.Id;
        }

        var result = await CheckAsync(batchId);

        result.Issues.Select(i => i.Code).Should().Equal("OPENING_DATE_MISMATCH");
        result.Fact.Should().BeNull();
    }

    [Fact]
    public async Task CxP_confirmada_al_corte_queda_lista_por_sus_cuotas_originales()
    {
        await SeedCompanyAsync();
        Guid batchId;
        await using (var db = Db())
        {
            var batch = CompletedBatch(ImportType.InitialPayables, 2);
            db.ImportBatches.Add(batch);
            foreach (var (number, amount) in new[] { ("001-001-000000001", 80m), ("001-001-000000002", 20.55m) })
                db.AccountsPayables.Add(AccountsPayable.CreateInitialBalance(_tenant, _companyId, _branch,
                    Guid.NewGuid(), "01", number, Opening.AddDays(-40), Opening.AddDays(10), Opening, amount,
                    batch.Id, Guid.NewGuid(), _actor));
            await db.SaveChangesAsync();
            batchId = batch.Id;
        }

        var result = await CheckAsync(batchId);

        result.Issues.Should().BeEmpty();
        result.Fact!.FactType.Should().Be("OpeningPayables");
        result.Fact.GrandTotal.Should().Be(100.55m);
    }

    [Fact]
    public async Task Sin_periodo_abierto_que_contenga_la_fecha_bloquea_con_PERIOD_NOT_OPEN()
    {
        await SeedCompanyAsync(openingBalanceDate: new DateOnly(2025, 12, 31));
        var batchId = await SeedReceivablesBatchAsync(10m);

        var result = await CheckAsync(batchId);

        result.IsReady.Should().BeFalse();
        result.Issues.Select(i => i.Code).Should().Equal("PERIOD_NOT_OPEN");
    }

    [Fact]
    public async Task Periodo_cerrado_bloquea_con_PERIOD_NOT_OPEN()
    {
        await SeedCompanyAsync();
        var batchId = await SeedReceivablesBatchAsync(10m);
        await using (var db = Db())
        {
            var period = await db.AccountingPeriods.SingleAsync();
            period.Close(_actor, new JournalEntryClosureReadiness(false, false, false));
            await db.SaveChangesAsync();
        }

        (await CheckAsync(batchId)).Issues.Select(i => i.Code).Should().Equal("PERIOD_NOT_OPEN");
    }

    [Fact]
    public async Task Regla_inactiva_bloquea_con_RULE_NOT_FOUND()
    {
        await SeedCompanyAsync();
        var batchId = await SeedReceivablesBatchAsync(10m);
        await using (var db = Db())
        {
            (await db.PostingRules.SingleAsync(r => r.SourceModule == "InitialLoad" && r.FactType == "OpeningReceivables"))
                .Disable(_actor);
            await db.SaveChangesAsync();
        }

        (await CheckAsync(batchId)).Issues.Select(i => i.Code).Should().Equal("RULE_NOT_FOUND");
    }

    [Fact]
    public async Task Cuenta_puente_inactiva_bloquea_con_POSTING_ACCOUNT_INVALID()
    {
        await SeedCompanyAsync();
        var batchId = await SeedReceivablesBatchAsync(10m);
        await using (var db = Db())
        {
            (await db.Accounts.SingleAsync(a => a.Code.Value == "3.1.04.001")).Disable(_actor);
            await db.SaveChangesAsync();
        }

        (await CheckAsync(batchId)).Issues.Select(i => i.Code).Should().Equal("POSTING_ACCOUNT_INVALID");
    }

    [Fact]
    public async Task Sin_fecha_de_apertura_bloquea()
    {
        await SeedCompanyAsync(withOpeningDate: false);
        var batchId = await SeedReceivablesBatchAsync(10m);

        (await CheckAsync(batchId)).Issues.Select(i => i.Code).Should().Equal("OPENING_DATE_MISSING");
    }

    [Fact]
    public async Task Lote_no_completado_maestro_o_inexistente_no_es_contabilizable()
    {
        await SeedCompanyAsync();
        Guid pending, customers;
        await using (var db = Db())
        {
            var p = CompletedBatch(ImportType.InitialReceivables, 1, complete: false);
            var c = CompletedBatch(ImportType.Customers, 1);
            db.ImportBatches.AddRange(p, c);
            await db.SaveChangesAsync();
            (pending, customers) = (p.Id, c.Id);
        }

        (await CheckAsync(pending)).Issues.Select(i => i.Code).Should().Equal("BATCH_NOT_COMPLETED");
        (await CheckAsync(customers)).Issues.Select(i => i.Code).Should().Equal("BATCH_NOT_ACCOUNTABLE");
        (await CheckAsync(Guid.NewGuid())).Issues.Select(i => i.Code).Should().Equal("BATCH_NOT_FOUND");
    }

    [Fact]
    public async Task Lote_sin_saldos_confirmados_bloquea()
    {
        await SeedCompanyAsync();
        Guid batchId;
        await using (var db = Db())
        {
            var batch = CompletedBatch(ImportType.InitialReceivables, 1);
            db.ImportBatches.Add(batch);
            await db.SaveChangesAsync();
            batchId = batch.Id;
        }

        (await CheckAsync(batchId)).Issues.Select(i => i.Code)
            .Should().BeEquivalentTo("SOURCE_EMPTY", "AMOUNT_NOT_POSITIVE");
    }

    [Fact]
    public async Task Apertura_ya_contabilizada_no_se_vuelve_a_preparar_y_Failed_es_reintentable()
    {
        await SeedCompanyAsync();
        var posted = await SeedReceivablesBatchAsync(10m);
        var failed = await SeedReceivablesBatchAsync(20m);
        await using (var db = Db())
        {
            var p = OpeningBalancePosting.CreatePending(_tenant, _companyId, posted, ImportType.InitialReceivables, Opening, 10m, _actor);
            p.MarkPosted(Guid.NewGuid(), _actor);
            var f = OpeningBalancePosting.CreatePending(_tenant, _companyId, failed, ImportType.InitialReceivables, Opening, 20m, _actor);
            f.MarkFailed("PERIOD_NOT_OPEN", "cerrado", _actor);
            db.OpeningBalancePostings.AddRange(p, f);
            await db.SaveChangesAsync();
        }

        var postedResult = await CheckAsync(posted);
        postedResult.Issues.Select(i => i.Code).Should().Equal("ALREADY_POSTED");
        postedResult.CurrentStatus.Should().Be(OpeningBalancePostingStatus.Posted);

        var failedResult = await CheckAsync(failed);
        failedResult.IsReady.Should().BeTrue();
        failedResult.CurrentStatus.Should().Be(OpeningBalancePostingStatus.Failed);
    }

    [Fact]
    public async Task Lote_de_otra_empresa_no_es_visible()
    {
        await SeedCompanyAsync();
        var batchId = await SeedReceivablesBatchAsync(10m);
        _companyId = Guid.NewGuid();

        (await CheckAsync(batchId)).Issues.Select(i => i.Code).Should().Equal("BATCH_NOT_FOUND");
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

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
