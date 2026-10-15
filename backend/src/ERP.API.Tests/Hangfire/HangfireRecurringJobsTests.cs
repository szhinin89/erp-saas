using ERP.API.Hangfire;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ERP.API.Tests.Hangfire;

public sealed class HangfireRecurringJobsTests
{
    internal static HangfireOptions Valid(string connection = "Host=localhost;Database=test;Username=test") => new()
    {
        Server = new() { Enabled = false }, StorageConnectionString = connection,
        Dashboard = new() { Enabled = false },
        Workers = new() { ["core"] = 1, ["communications"] = 2, ["sri"] = 2, ["maintenance"] = 1 },
        Jobs = HangfireRecurringJobs.All.Keys.ToDictionary(id => id, id => new HangfireOptions.RecurringJobOptions
        {
            Enabled = true, Cron = "* * * * *", BatchSize = id == "retention-electronic-recovery" ? 3 : 2, MaxConcurrency = 1,
            Queue = id switch { "process-outbox" => "core", "expire-user-sessions" => "maintenance",
                "process-communications" or "reconcile-electronic-document-communications" => "communications", _ => "sri" }
        })
    };

    [Fact] public void Catalog_has_exactly_seven_concrete_jobs()
    {
        HangfireRecurringJobs.All.Should().HaveCount(7);
        HangfireRecurringJobs.All.Values.Should().OnlyContain(t => typeof(IRecurringJob).IsAssignableFrom(t.JobType) && !t.JobType.IsAbstract);
        HangfireRecurringJobs.All.Keys.Should().NotContain("masterdata-reconciliation");
        new HangfireOptionsValidator().Validate(null, Valid()).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("storage")] [InlineData("unknown")] [InlineData("missing")]
    [InlineData("cron")] [InlineData("queue")] [InlineData("batch")]
    [InlineData("concurrency")] [InlineData("workers")] [InlineData("enabled")]
    [InlineData("server")] [InlineData("dashboard")] [InlineData("multiplexing")]
    public void Invalid_configuration_fails_even_when_server_is_off(string field)
    {
        var options = Valid();
        var job = options.Jobs["process-outbox"];
        switch (field)
        {
            case "storage": options.StorageConnectionString = ""; break;
            case "unknown": options.Jobs["legacy"] = job; break;
            case "missing": options.Jobs.Remove("expire-user-sessions"); break;
            case "cron": job.Cron = "61 * * * *"; break;
            case "queue": job.Queue = "default"; break;
            case "batch": job.BatchSize = 0; break;
            case "concurrency": job.MaxConcurrency = 2; break;
            case "workers": options.Workers.Remove("core"); break;
            case "enabled": job.Enabled = null; break;
            case "server": options.Server.Enabled = null; break;
            case "dashboard": options.Dashboard.Enabled = true; break;
            case "multiplexing": options.StorageConnectionString += ";Multiplexing=true"; break;
        }
        new HangfireOptionsValidator().Validate(null, options).Failed.Should().BeTrue();
    }

    [Fact] public void Configured_json_validates_and_API_registers_no_worker_when_off()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "STATUS.md"))) directory = directory.Parent;
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(directory!.FullName, "backend/src/ERP.API/appsettings.json")).Build();
        var services = new ServiceCollection();
        services.AddErpBackgroundProcessing(config);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<HangfireOptions>>().Value.Server.Enabled.Should().BeFalse();
        provider.GetService<global::Hangfire.JobStorage>().Should().BeNull();
    }

    [Fact] public void Legacy_keys_are_rejected()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Hangfire:Enabled"] = "true" }).Build();
        var action = () => new ServiceCollection().AddErpBackgroundProcessing(config);
        action.Should().Throw<InvalidOperationException>();
    }
}
