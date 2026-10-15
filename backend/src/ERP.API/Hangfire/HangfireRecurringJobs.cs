using Hangfire;
using Hangfire.Storage;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Options;

namespace ERP.API.Hangfire;

public interface IRecurringJob
{
    Task ExecuteAsync(int batchSize, CancellationToken cancellationToken);
}

public sealed record RecurringJobDefinition(Type JobType, string Queue);

public static class HangfireRecurringJobs
{
    public static readonly IReadOnlyDictionary<string, RecurringJobDefinition> All = new Dictionary<string, RecurringJobDefinition>(StringComparer.Ordinal)
    {
        ["process-outbox"] = new(typeof(ProcessOutboxJob), "core"),
        ["process-communications"] = new(typeof(ProcessCommunicationsJob), "communications"),
        ["reconcile-electronic-document-communications"] = new(typeof(ReconcileElectronicDocumentCommunicationsJob), "communications"),
        ["electronic-document-retry"] = new(typeof(ElectronicDocumentRetryJob), "sri"),
        ["sales-electronic-document-recovery"] = new(typeof(SalesElectronicDocumentRecoveryJob), "sri"),
        ["retention-electronic-recovery"] = new(typeof(RetentionElectronicRecoveryJob), "sri"),
        ["expire-user-sessions"] = new(typeof(ExpireUserSessionsJob), "maintenance"),
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
        foreach (var definition in All.Values) services.AddScoped(definition.JobType);
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
