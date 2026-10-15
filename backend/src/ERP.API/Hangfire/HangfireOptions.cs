using Microsoft.Extensions.Options;
using Npgsql;

namespace ERP.API.Hangfire;

/// <summary>
/// Único modelo de valores operativos de Background Processing (ADR-043): servidor, storage,
/// workers por cola y, por job, Enabled/Cron/Queue/BatchSize. La identidad (ID → tipo) vive solo
/// en <see cref="HangfireRecurringJobs.All"/>.
/// </summary>
public sealed class HangfireOptions
{
    public ServerOptions Server { get; set; } = new();
    public string? StorageConnectionString { get; set; }
    public Dictionary<string, int> Workers { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, RecurringJobOptions> Jobs { get; set; } = new(StringComparer.Ordinal);
    public sealed class ServerOptions { public bool? Enabled { get; set; } }
    public sealed class RecurringJobOptions
    {
        public bool? Enabled { get; set; }
        public string? Cron { get; set; }
        public string? Queue { get; set; }
        public int BatchSize { get; set; }
    }
}

public sealed class HangfireOptionsValidator : IValidateOptions<HangfireOptions>
{
    // Nombres de cola aceptados por Hangfire; "default" queda reservado: ningún servidor lo consume.
    private static readonly System.Text.RegularExpressions.Regex QueueName = new("^[a-z0-9_-]+$");
    public ValidateOptionsResult Validate(string? name, HangfireOptions options)
    {
        var errors = new List<string>();
        if (options.Server.Enabled is null) errors.Add("Hangfire:Server:Enabled must be explicit.");
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(options.StorageConnectionString ?? "");
            if (string.IsNullOrWhiteSpace(connection.Host) || string.IsNullOrWhiteSpace(connection.Database)
                || string.IsNullOrWhiteSpace(connection.Username)) errors.Add("Hangfire:StorageConnectionString requires Host, Database and Username.");
            if (connection.Multiplexing) errors.Add("Hangfire storage cannot use multiplexing with session locks.");
        }
        catch (ArgumentException) { errors.Add("Hangfire:StorageConnectionString is invalid."); }
        // Las colas salen de los jobs configurados; Workers define solo cuántos workers atiende cada una.
        var jobQueues = options.Jobs.Values.Select(j => j.Queue).OfType<string>().ToHashSet(StringComparer.Ordinal);
        if (!options.Workers.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(jobQueues)
            || options.Workers.Values.Any(v => v < 1 || v > 256))
            errors.Add("Hangfire:Workers requires exactly the queues used by Hangfire:Jobs, each 1..256.");
        if (!options.Jobs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(HangfireRecurringJobs.All.Keys))
            errors.Add("Hangfire:Jobs must match the seven catalog IDs exactly.");
        foreach (var (id, job) in options.Jobs)
        {
            if (job.Enabled is null || job.BatchSize is < 1 or > 10000)
                errors.Add($"Hangfire:Jobs:{id} requires Enabled and BatchSize 1..10000.");
            if (id == "retention-electronic-recovery" && job.BatchSize < 3) errors.Add("Retention recovery requires BatchSize >= 3 for its three streams.");
            if (job.Queue is null || job.Queue == "default" || !QueueName.IsMatch(job.Queue))
                errors.Add($"Hangfire:Jobs:{id}:Queue requires an explicit lowercase queue other than default.");
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
