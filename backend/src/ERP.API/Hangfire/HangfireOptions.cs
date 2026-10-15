using Microsoft.Extensions.Options;
using Npgsql;

namespace ERP.API.Hangfire;

public sealed class HangfireOptions
{
    public ServerOptions Server { get; set; } = new();
    public string? StorageConnectionString { get; set; }
    public Dictionary<string, int> Workers { get; set; } = new(StringComparer.Ordinal);
    public DashboardOptions Dashboard { get; set; } = new();
    public Dictionary<string, RecurringJobOptions> Jobs { get; set; } = new(StringComparer.Ordinal);
    public sealed class ServerOptions { public bool? Enabled { get; set; } }
    public sealed class DashboardOptions { public bool? Enabled { get; set; } }
    public sealed class RecurringJobOptions
    {
        public bool? Enabled { get; set; }
        public string? Cron { get; set; }
        public string? Queue { get; set; }
        public int BatchSize { get; set; }
        public int MaxConcurrency { get; set; }
    }
}

public sealed class HangfireOptionsValidator : IValidateOptions<HangfireOptions>
{
    public static readonly string[] Queues = ["core", "communications", "sri", "maintenance"];
    public ValidateOptionsResult Validate(string? name, HangfireOptions options)
    {
        var errors = new List<string>();
        if (options.Server.Enabled is null) errors.Add("Hangfire:Server:Enabled must be explicit.");
        if (options.Dashboard.Enabled != false) errors.Add("Hangfire:Dashboard:Enabled must be false.");
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(options.StorageConnectionString ?? "");
            if (string.IsNullOrWhiteSpace(connection.Host) || string.IsNullOrWhiteSpace(connection.Database)
                || string.IsNullOrWhiteSpace(connection.Username)) errors.Add("Hangfire:StorageConnectionString requires Host, Database and Username.");
            if (connection.Multiplexing) errors.Add("Hangfire storage cannot use multiplexing with session locks.");
        }
        catch (ArgumentException) { errors.Add("Hangfire:StorageConnectionString is invalid."); }
        if (!options.Workers.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(Queues)
            || options.Workers.Values.Any(v => v < 1 || v > 256))
            errors.Add("Hangfire:Workers requires exactly core/communications/sri/maintenance, each 1..256.");
        if (!options.Jobs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(HangfireRecurringJobs.All.Keys))
            errors.Add("Hangfire:Jobs must match the seven catalog IDs exactly.");
        foreach (var (id, job) in options.Jobs)
        {
            if (job.Enabled is null || job.BatchSize is < 1 or > 10000 || job.MaxConcurrency != 1)
                errors.Add($"Hangfire:Jobs:{id} requires Enabled, BatchSize 1..10000 and MaxConcurrency=1.");
            if (id == "retention-electronic-recovery" && job.BatchSize < 3) errors.Add("Retention recovery requires BatchSize >= 3 for its three streams.");
            var expectedQueue = HangfireRecurringJobs.All.TryGetValue(id, out var definition) ? definition.Queue : null;
            if (job.Queue != expectedQueue) errors.Add($"Hangfire:Jobs:{id}:Queue must be {expectedQueue}.");
            if (!IsValidCron(job.Cron)) errors.Add($"Hangfire:Jobs:{id}:Cron requires five numeric UTC fields.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    // Deliberately accept the numeric five-field subset used by this catalog.
    private static bool IsValidCron(string? cron)
    {
        var fields = cron?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields?.Length != 5) return false;
        int[] minimum = [0, 0, 1, 1, 0];
        int[] maximum = [59, 23, 31, 12, 6];
        for (var i = 0; i < 5; i++)
            foreach (var item in fields[i].Split(','))
            {
                var step = item.Split('/');
                if (step.Length > 2 || (step.Length == 2 && (!int.TryParse(step[1], out var value) || value < 1 || value > maximum[i]))) return false;
                if (step[0] == "*") continue;
                var range = step[0].Split('-');
                if (range.Length > 2 || !int.TryParse(range[0], out var start) || start < minimum[i] || start > maximum[i]) return false;
                if (range.Length == 2 && (!int.TryParse(range[1], out var end) || end < start || end > maximum[i])) return false;
            }
        return true;
    }
}
