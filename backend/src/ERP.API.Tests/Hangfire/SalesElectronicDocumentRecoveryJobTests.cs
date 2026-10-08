using ERP.API.Hangfire;
using ERP.Application.Modules.Sales.Services;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.API.Tests.Hangfire;

public sealed class SalesElectronicDocumentRecoveryJobTests
{
    [Fact]
    public async Task Every_candidate_uses_new_scope_and_own_context_even_after_failure()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var candidates = new[] {
            new SalesElectronicRecoveryCandidate(tenant, company, Guid.NewGuid(), Guid.NewGuid()),
            new SalesElectronicRecoveryCandidate(tenant, company, Guid.NewGuid(), Guid.NewGuid()),
            new SalesElectronicRecoveryCandidate(tenant, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            new SalesElectronicRecoveryCandidate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        };
        var calls = new List<(Guid ScopeId, Guid TenantId, Guid CompanyId, Guid BranchId)>();
        var services = new ServiceCollection();
        services.AddScoped<ISalesElectronicDocumentRecovery>(_ => new RecordingRecovery(candidates, calls));
        await using var provider = services.BuildServiceProvider();
        // An inherited context must be restored, never contaminate candidates.
        using var inherited = JobExecutionContext.Begin(Guid.NewGuid(), Guid.NewGuid());
        var previous = (JobTenantContext.Current, JobCompanyContext.Current);
        var job = new SalesElectronicDocumentRecoveryJob(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SalesElectronicDocumentRecoveryJob>.Instance);
        await job.ExecuteAsync();
        calls.Should().HaveCount(candidates.Length);
        calls.Select(c => c.ScopeId).Distinct().Should().HaveCount(candidates.Length);
        calls.Select(c => (c.TenantId, c.CompanyId, c.BranchId))
            .Should().Equal(candidates.Select(c => (c.TenantId, c.CompanyId, c.BranchId)));
        (JobTenantContext.Current, JobCompanyContext.Current).Should().Be(previous);
    }

    private sealed class RecordingRecovery(
        IReadOnlyList<SalesElectronicRecoveryCandidate> candidates,
        List<(Guid ScopeId, Guid TenantId, Guid CompanyId, Guid BranchId)> calls)
        : ISalesElectronicDocumentRecovery
    {
        private readonly Guid _scopeId = Guid.NewGuid();

        public Task<IReadOnlyList<SalesElectronicRecoveryCandidate>> GetCandidatesAsync(CancellationToken ct) =>
            Task.FromResult(candidates);

        public Task RecoverAsync(SalesElectronicRecoveryCandidate candidate, CancellationToken ct)
        {
            calls.Add((_scopeId, JobTenantContext.Current, JobCompanyContext.Current, candidate.BranchId));
            if (candidate == candidates[0]) throw new InvalidOperationException("Injected first-candidate failure");
            return Task.CompletedTask;
        }
    }
}
