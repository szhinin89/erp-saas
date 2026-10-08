using System.Data.Common;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Inventory.Costing;
using ERP.Application.Modules.Sales.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Items;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Inventory.Events;
using ERP.Domain.Modules.Caja.Enums;

namespace ERP.Infrastructure.Tests.Accounting;

public sealed partial class SalesInvoiceAuthorizedPostingIntegrationTests
{
    private AuthorizeSalesInvoiceHandler A8Authorize(ErpDbContext db, ServiceProvider provider, Guid? tenantId = null, Guid? companyId = null, Guid? branchId = null)
    {
        var company = new FixedCurrentCompany(companyId ?? _companyId);
        var clock = new Mock<ICompanyClock>();
        clock.Setup(c => c.TodayAsync(_companyId, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(new DateOnly(2026, 7, 25));
        var tax = new Mock<ISriTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(0m, "VAT 0"));
        return new AuthorizeSalesInvoiceHandler(
            new SalesInvoiceRepository(db, company), new SalesReceivableRepository(db, company),
            provider.GetRequiredService<IStockRepository>(), new PaymentTermRepository(db), tax.Object,
            new DocumentSequenceRepository(db), new EmissionPointRepository(db), new EstablishmentRepository(db),
            new ElectronicDocumentRepository(db, clock.Object), Mock.Of<ISalesInvoiceEmissionStrategyResolver>(),
            clock.Object, new BusinessPartnerRepository(db), Mock.Of<ISalesFiscalPolicyResolver>(),
            new PaymentMethodRepository(db), new PaymentMethodAccountRepository(db),
            new CompanyBankAccountRepository(db, company), new CashSessionRepository(db, company),
            new CashRegisterRepository(db, company), new AccountRepository(db),
            provider.GetRequiredService<IPostingEngine>(), NullLogger<AuthorizeSalesInvoiceHandler>.Instance,
            new FixedCurrentTenant(tenantId ?? _tenantId), company, Mock.Of<ICurrentBranch>(b => b.BranchId == (branchId ?? _branchId)),
            Mock.Of<ICurrentUser>(u => u.UserId == _createdBy), new A7Preferences(),
            StandardPrecisionPolicyProvider.Instance, new ItemRepository(db), new WarehouseRepository(db, company), new SalesAuthorizationRetryReader(new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options, new FixedCurrentTenant(tenantId ?? _tenantId), company, new NoOpPublisher(), new PostgresDatabaseExceptionTranslator()));
    }

    private async Task<Result<ERP.Application.Modules.Sales.DTOs.SalesInvoiceDto>> A8AttemptAsync(
        Guid id, DbCommandInterceptor? observer = null, string? name = null,
        Guid? tenantId = null, Guid? companyId = null, Guid? branchId = null)
    {
        var (db, provider) = BuildA7Context(observer ?? new A8NoOpInterceptor(), name ?? "A8");
        using var disposeProvider = provider;
        await using var disposeDb = db;
        return await A8Authorize(db, provider, tenantId, companyId, branchId).Handle(new(id), CancellationToken.None);
    }

    [Fact]
    public async Task A8_lost_response_and_multiple_retries_return_same_committed_document_without_effects()
    {
        var fixture = await SeedCoverageAsync(3m, 1);
        var id = fixture.Invoices.Single();
        // The first response is discarded; only committed state is used by the retries.
        (await A8AttemptAsync(id)).IsSuccess.Should().BeTrue();
        await using var before = CreateContext();
        var outboxIds = await before.OutboxMessages.Select(m => m.Id).ToArrayAsync();
        var first = await A8AttemptAsync(id);
        first.IsSuccess.Should().BeTrue(first.Error);
        for (var i = 0; i < 5; i++)
        {
            var retry = await A8AttemptAsync(id);
            retry.IsSuccess.Should().BeTrue(retry.Error);
            retry.Value.Should().BeEquivalentTo(first.Value);
        }
        first.Value!.Id.Should().Be(id);
        first.Value.Status.Should().Be("Authorized");
        await AssertCoverageAsync(fixture, id);
        await using var after = CreateContext();
        (await after.OutboxMessages.Select(m => m.Id).ToArrayAsync()).Should().BeEquivalentTo(outboxIds);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    public async Task A8_double_request_converges_to_same_authorized_document_in_clean_context(int initialStock)
    {
        var fixture = await SeedCoverageAsync(initialStock, 1);
        var id = fixture.Invoices.Single();
        var name = $"A8-double-{Guid.NewGuid():N}";
        var overlap = new InventoryLockOverlap(_postgres.GetConnectionString(), name);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            A8AttemptAsync(id, new FirstInventoryLockObserver(overlap), name)));
        results.Should().OnlyContain(r => r.IsSuccess);
        results[0].Value.Should().BeEquivalentTo(results[1].Value, options => options.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromMicroseconds(1))).WhenTypeIs<DateTime>());
        results[0].Value!.Status.Should().Be("Authorized");
        overlap.Completed.Should().Be(2);
        await AssertCoverageAsync(fixture, id);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("company")]
    [InlineData("branch")]
    [InlineData("missing-company")]
    [InlineData("missing-branch")]
    public async Task A8_retry_foreign_or_missing_scope_is_rejected(string scope)
    {
        var fixture = await SeedCoverageAsync(3m, 1);
        var id = fixture.Invoices.Single();
        (await A8AttemptAsync(id)).IsSuccess.Should().BeTrue();
        var result = await A8AttemptAsync(id,
            tenantId: scope == "tenant" ? Guid.NewGuid() : null,
            companyId: scope == "company" ? Guid.NewGuid() : scope == "missing-company" ? Guid.Empty : null,
            branchId: scope == "branch" ? Guid.NewGuid() : scope == "missing-branch" ? Guid.Empty : null);
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        await AssertCoverageAsync(fixture, id);
    }

    [Fact]
    public async Task A8_stale_precheck_returns_committed_authorization_instead_of_stock_error()
    {
        var fixture = await SeedCoverageAsync(1m, 1);
        var id = fixture.Invoices.Single();
        var pause = new A8PauseStockPrecheck();
        var staleRequest = A8AttemptAsync(id, pause);
        try
        {
            await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            (await A8AttemptAsync(id)).IsSuccess.Should().BeTrue();
        }
        finally { pause.Release.TrySetResult(true); }
        var retry = await staleRequest;
        retry.IsSuccess.Should().BeTrue(retry.Error);
        retry.Value!.Id.Should().Be(id);
        await AssertCoverageAsync(fixture, id);
    }

    [Fact]
    public async Task A8_last_unit_for_different_invoices_preserves_stock_rejection()
    {
        var fixture = await SeedCoverageAsync(1m, 2);
        var name = $"A8-distinct-{Guid.NewGuid():N}";
        var overlap = new InventoryLockOverlap(_postgres.GetConnectionString(), name);
        async Task<bool> AttemptAsync(Guid id)
        {
            try
            {
                return (await A8AttemptAsync(id, new FirstInventoryLockObserver(overlap), name)).IsSuccess;
            }
            catch (DomainRuleViolationException) { return false; }
        }
        var success = await Task.WhenAll(fixture.Invoices.Select(AttemptAsync));
        success.Count(s => s).Should().Be(1);
        await AssertCoverageAsync(fixture, fixture.Invoices[Array.FindIndex(success, s => s)]);
    }

    [Fact]
    public async Task A8_rollback_before_commit_leaves_draft_and_next_request_authorizes_normally()
    {
        var fixture = await SeedCoverageAsync(3m, 1);
        var id = fixture.Invoices.Single();
        Func<Task> attempt = async () => await A8AttemptAsync(id, new A8FailAfterCashLock());
        await attempt.Should().ThrowAsync<InvalidOperationException>().WithMessage("A8 injected failure before commit");
        await AssertCoverageAsync(fixture, null);
        var retry = await A8AttemptAsync(id);
        retry.IsSuccess.Should().BeTrue(retry.Error);
        retry.Value!.Id.Should().Be(id);
        await AssertCoverageAsync(fixture, id);
    }

    [Fact]
    public async Task A8_conflict_with_uncommitted_authorized_tracker_does_not_return_false_success()
    {
        var fixture = await SeedCoverageAsync(3m, 1);
        var id = fixture.Invoices.Single();
        Func<Task> attempt = async () => await A8AttemptAsync(id, new A8FailAfterCashLock(conflict: true));
        await attempt.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await AssertCoverageAsync(fixture, null);
        (await A8AttemptAsync(id)).IsSuccess.Should().BeTrue();
        await AssertCoverageAsync(fixture, id);
    }

    [Fact]
    public async Task A8_authorized_electronic_retry_reads_existing_document_without_dispatch()
    {
        var fixture = await SeedCoverageAsync(3m, 1);
        var id = fixture.Invoices.Single();
        (await A8AttemptAsync(id)).IsSuccess.Should().BeTrue();
        await using (var seed = CreateContext())
        {
            // Persisted electronic origin with no issuer or resolver available to the retry.
            await seed.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales_invoices SET emission_type = 1 WHERE id = {id}");
            seed.ElectronicDocuments.Add(ERP.Domain.Modules.ElectronicDocuments.Entities.ElectronicDocument.Create(
                _tenantId, _companyId, ERP.Domain.Modules.ElectronicDocuments.Enums.ElectronicDocumentType.Invoice,
                "Sales", id, _createdBy));
            await seed.SaveChangesAsync();
        }
        var retry = await A8AttemptAsync(id);
        retry.IsSuccess.Should().BeTrue(retry.Error);
        retry.Value!.EmissionType.Should().Be("Electronic");
        retry.Value.ElectronicStatus.Should().Be("Draft");
        await using var verify = CreateContext();
        (await verify.ElectronicDocuments.CountAsync(d => d.SourceEntityId == id)).Should().Be(1);
        await AssertCoverageAsync(fixture, id);
    }

    private sealed class A8NoOpInterceptor : DbCommandInterceptor;

    private sealed class A8PauseStockPrecheck : DbCommandInterceptor
    {
        public TaskCompletionSource<bool> Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken ct = default)
        {
            if (command.CommandText.Contains("current_stocks") && Reached.TrySetResult(true))
                await Release.Task.WaitAsync(ct);
            return result;
        }
    }

    private sealed class A8FailAfterCashLock(bool conflict = false) : DbCommandInterceptor
    {
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken ct = default)
        {
            // Initial sale state has already been written in the still-open transaction.
            if (command.CommandText.Contains("cash_sessions") && command.CommandText.Contains("FOR UPDATE"))
                throw conflict ? new DbUpdateConcurrencyException("A8 injected conflict before commit")
                    : new InvalidOperationException("A8 injected failure before commit");
            return ValueTask.FromResult(result);
        }
    }
}



