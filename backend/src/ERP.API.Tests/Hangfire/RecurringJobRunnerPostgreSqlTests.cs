using Microsoft.AspNetCore.DataProtection;
using System.Diagnostics.Metrics;
using ERP.API.Hangfire;
using ERP.Infrastructure.BackgroundProcessing;
using ERP.Infrastructure.Persistence.Outbox;
using FluentAssertions;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace ERP.API.Tests.Hangfire;

public sealed class RecurringJobRunnerPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Two_independent_instances_skip_overlap_release_lock_and_emit_metrics()
    {
        var settings = HangfireRecurringJobsTests.Valid(_postgres.GetConnectionString());
        var processor = new BlockingProcessor();
        var measurements = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        { if (instrument.Meter.Name == "ERP.Jobs") meterListener.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) => { lock (measurements) measurements.Add(instrument.Name); });
        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) => { lock (measurements) measurements.Add(instrument.Name); });
        listener.Start();
        var services = new ServiceCollection().AddScoped<ProcessOutboxJob>().AddSingleton<IOutboxProcessor>(processor);
        using var firstProvider = services.BuildServiceProvider();
        using var secondProvider = services.BuildServiceProvider();
        RecurringJobRunner Runner(IServiceProvider provider) => new(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(settings), NullLogger<RecurringJobRunner>.Instance);
        var first = Runner(firstProvider).RunAsync("process-outbox", default);
        await processor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await Runner(secondProvider).RunAsync("process-outbox", default).WaitAsync(TimeSpan.FromSeconds(5));
        processor.Calls.Should().Be(1);
        processor.BatchSize.Should().Be(2);
        processor.Release.TrySetResult();
        await first;
        await Runner(secondProvider).RunAsync("process-outbox", default);
        processor.Calls.Should().Be(2);
        settings.Jobs["process-outbox"].Enabled = false;
        await Runner(firstProvider).RunAsync("process-outbox", default);
        processor.Calls.Should().Be(2);
        measurements.Should().Contain("erp.jobs.lock_skipped").And.Contain("erp.jobs.duration").And.Contain("erp.jobs.processed");
    }

    [Fact]
    public void Synchronization_removes_orphans_and_disabled_jobs_and_sets_queues()
    {
        var storage = new PostgreSqlStorage(new NpgsqlConnectionFactory(_postgres.GetConnectionString(), new PostgreSqlStorageOptions(), null), new PostgreSqlStorageOptions());
        var manager = new RecurringJobManager(storage);
        manager.AddOrUpdate<RecurringJobRunner>("legacy", "core", r => r.RunAsync("legacy", CancellationToken.None), "* * * * *");
        var settings = HangfireRecurringJobsTests.Valid(_postgres.GetConnectionString());
        HangfireRecurringJobs.Synchronize(manager, storage, settings);
        using var connection = storage.GetConnection();
        var jobs = connection.GetRecurringJobs();
        jobs.Select(j => j.Id).Should().BeEquivalentTo(HangfireRecurringJobs.All.Keys);
        jobs.Should().OnlyContain(j => j.Job.Queue == settings.Jobs[j.Id].Queue && j.Job.Type == typeof(RecurringJobRunner));
        manager.Trigger("process-outbox");
        storage.GetMonitoringApi().EnqueuedJobs("core", 0, 10).Should().ContainSingle();
        storage.GetMonitoringApi().EnqueuedJobs("default", 0, 10).Should().BeEmpty();
        settings.Jobs["process-outbox"].Enabled = false;
        HangfireRecurringJobs.Synchronize(manager, storage, settings);
        connection.GetRecurringJobs().Select(j => j.Id).Should().NotContain("process-outbox");
    }

    [Fact]
    public async Task Worker_starts_separately_without_HTTP_server()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "STATUS.md"))) directory = directory.Parent;
        var arguments = new List<string> { "--contentRoot", Path.Combine(directory!.FullName, "backend/src/ERP.API"),
            "--environment", "Testing", "--Hangfire:Server:Enabled", "true",
            "--Hangfire:StorageConnectionString", _postgres.GetConnectionString(),
            "--ConnectionStrings:DefaultConnection", _postgres.GetConnectionString() };
        foreach (var id in HangfireRecurringJobs.All.Keys)
            arguments.AddRange(["--Hangfire:Jobs:" + id + ":Enabled", "false"]);
        using var host = BackgroundWorkerHost.Build(arguments.ToArray());
        host.Services.GetService<Microsoft.AspNetCore.Hosting.Server.IServer>().Should().BeNull();
        var apiBuilder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(new Microsoft.AspNetCore.Builder.WebApplicationOptions
        {
            EnvironmentName = "Testing", ContentRootPath = Path.Combine(directory.FullName, "backend/src/ERP.API")
        });
        apiBuilder.Services.AddDataProtection();
        await using var api = apiBuilder.Build();
        host.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.DataProtection.DataProtectionOptions>>().Value.ApplicationDiscriminator
            .Should().Be(api.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.DataProtection.DataProtectionOptions>>().Value.ApplicationDiscriminator);
        using (var scope = host.Services.CreateScope())
            foreach (var definition in HangfireRecurringJobs.All.Values)
                scope.ServiceProvider.GetRequiredService(definition.JobType).Should().BeAssignableTo<IRecurringJob>();
        await host.StartAsync();
        var storage = host.Services.GetRequiredService<JobStorage>();
        var expected = host.Services.GetRequiredService<IOptions<HangfireOptions>>().Value.Workers;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (storage.GetMonitoringApi().Servers().Count < 4 && DateTime.UtcNow < deadline)
            await Task.Delay(100);
        var servers = storage.GetMonitoringApi().Servers();
        servers.Should().HaveCount(4);
        foreach (var server in servers)
        {
            server.Queues.Should().ContainSingle();
            server.WorkersCount.Should().Be(expected[server.Queues.Single()]);
        }
        await host.StopAsync();
        global::Hangfire.Logging.LogProvider.SetCurrentLogProvider(null);
    }

    [Fact]
    public void Runner_disables_automatic_retries()
    {
        var attribute = typeof(RecurringJobRunner).GetMethod(nameof(RecurringJobRunner.RunAsync))!
            .CustomAttributes.Single(a => a.AttributeType == typeof(AutomaticRetryAttribute));
        attribute.NamedArguments.Single(a => a.MemberName == "Attempts").TypedValue.Value.Should().Be(0);
    }

    private sealed class BlockingProcessor : IOutboxProcessor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public int BatchSize;
        public async Task ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            BatchSize = batchSize;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            JobMetrics.RecordProcessed();
        }
    }
}
