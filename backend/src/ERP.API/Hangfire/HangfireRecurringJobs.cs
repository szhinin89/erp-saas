using Hangfire;
using Hangfire.Storage;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Options;

namespace ERP.API.Hangfire;

public interface IRecurringJob
{
    Task ExecuteAsync(int batchSize, CancellationToken cancellationToken);
}

/// <summary>
/// Catálogo inmutable ID → tipo concreto (ADR-043). Solo identidad: cola, cron, lote y Enabled
/// son valores operativos de <see cref="HangfireOptions"/>.
/// </summary>
public static class HangfireRecurringJobs
{
    public static readonly IReadOnlyDictionary<string, Type> All = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["process-outbox"] = typeof(ProcessOutboxJob),
        ["process-communications"] = typeof(ProcessCommunicationsJob),
        ["reconcile-electronic-document-communications"] = typeof(ReconcileElectronicDocumentCommunicationsJob),
        ["electronic-document-retry"] = typeof(ElectronicDocumentRetryJob),
        ["sales-electronic-document-recovery"] = typeof(SalesElectronicDocumentRecoveryJob),
        ["retention-electronic-recovery"] = typeof(RetentionElectronicRecoveryJob),
        ["expire-user-sessions"] = typeof(ExpireUserSessionsJob),
    }.AsReadOnly();

    public static IServiceCollection AddErpBackgroundProcessing(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<HangfireOptions>, HangfireOptionsValidator>();
        services.AddOptions<HangfireOptions>().Bind(configuration.GetSection("Hangfire"), b => b.ErrorOnUnknownConfiguration = true)
            .ValidateOnStart();
        var settings = configuration.GetSection("Hangfire").Get<HangfireOptions>(b => b.ErrorOnUnknownConfiguration = true)
            ?? throw new InvalidOperationException("Hangfire configuration is required.");
        var validation = new HangfireOptionsValidator().Validate(null, settings);
        if (validation.Failed) throw new OptionsValidationException("Hangfire", typeof(HangfireOptions), validation.Failures);
        foreach (var jobType in All.Values) services.AddScoped(jobType);
        services.AddScoped<RecurringJobRunner>();
        if (settings.Server.Enabled == true)
        {
            services.AddHangfire(config => config.UsePostgreSqlStorage(o => o.UseNpgsqlConnection(settings.StorageConnectionString!)));
            services.AddHostedService<RecurringJobSynchronization>();
            foreach (var (queue, workers) in settings.Workers)
                services.AddHangfireServer(server =>
                {
                    server.Queues = [queue];
                    server.WorkerCount = workers;
                    server.ServerName = $"{Environment.MachineName}:{Environment.ProcessId}:{queue}";
                });
        }
        return services;
    }

    public static void Synchronize(IRecurringJobManager manager, JobStorage storage, HangfireOptions options)
    {
        using var connection = storage.GetConnection();
        foreach (var existing in connection.GetRecurringJobs())
            if (!All.ContainsKey(existing.Id) || !options.Jobs[existing.Id].Enabled!.Value)
                manager.RemoveIfExists(existing.Id);
        foreach (var (id, job) in options.Jobs)
        {
            if (!job.Enabled!.Value) { manager.RemoveIfExists(id); continue; }
            manager.AddOrUpdate<RecurringJobRunner>(id, job.Queue!,
                runner => runner.RunAsync(id, CancellationToken.None), job.Cron!,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
    }
}

public sealed class RecurringJobSynchronization(
    IRecurringJobManager manager, JobStorage storage, IOptions<HangfireOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HangfireRecurringJobs.Synchronize(manager, storage, options.Value);
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
