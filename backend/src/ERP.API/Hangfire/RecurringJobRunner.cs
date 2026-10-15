using System.Diagnostics;
using ERP.Infrastructure.BackgroundProcessing;
using ERP.Infrastructure.Services;
using Hangfire;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ERP.API.Hangfire;

public sealed class RecurringJobRunner(
    IServiceScopeFactory scopes, IOptions<HangfireOptions> options, ILogger<RecurringJobRunner> logger)
{
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(string id, CancellationToken cancellationToken)
    {
        if (!HangfireRecurringJobs.All.TryGetValue(id, out var definition)) return;
        var settings = options.Value.Jobs[id];
        if (settings.Enabled != true) return;
        await using var connection = new NpgsqlConnection(options.Value.StorageConnectionString);
        await connection.OpenAsync(cancellationToken);
        // Keep the physical session until completion; a crash releases the lock.
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(hashtextextended(@id, 734108))", connection);
        command.Parameters.AddWithValue("id", "ERP.Jobs/" + id);
        if (!(bool)(await command.ExecuteScalarAsync(cancellationToken))!)
        {
            JobMetrics.LockSkipped.Add(1, new KeyValuePair<string, object?>("job", id));
            logger.LogInformation("Job {JobId} skipped: lock held", id);
            return;
        }
        var started = Stopwatch.GetTimestamp();
        using var measurement = JobMetrics.Begin(id);
        using var context = new SystemJobContext();
        JobMetrics.RecordProcessed(0);
        JobMetrics.RecordFailed(0);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await ((IRecurringJob)scope.ServiceProvider.GetRequiredService(definition.JobType))
                .ExecuteAsync(settings.BatchSize, cancellationToken);
            logger.LogInformation("Job {JobId} completed", id);
        }
        catch (Exception ex)
        {
            JobMetrics.RecordFailed();
            logger.LogError(ex, "Job {JobId} failed", id);
            throw;
        }
        finally
        {
            JobMetrics.Duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds,
                new KeyValuePair<string, object?>("job", id));
            command.CommandText = "SELECT pg_advisory_unlock(hashtextextended(@id, 734108))";
            await command.ExecuteScalarAsync(CancellationToken.None);
        }
    }

    private sealed class SystemJobContext : IDisposable
    {
        private readonly Guid _tenant = JobTenantContext.Current;
        private readonly Guid _company = JobCompanyContext.Current;
        private readonly Guid _branch = JobBranchContext.Current;
        public SystemJobContext()
        {
            JobTenantContext.Current = Guid.Empty;
            JobCompanyContext.Current = Guid.Empty;
            JobBranchContext.Current = Guid.Empty;
        }
        public void Dispose()
        {
            JobTenantContext.Current = _tenant;
            JobCompanyContext.Current = _company;
            JobBranchContext.Current = _branch;
        }
    }
}
