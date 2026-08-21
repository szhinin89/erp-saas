using System.Text.Json;
using ZH.PrintAgent.Contracts;
using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.Infrastructure;

public sealed class JsonPrintJobStore : IPrintJobStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly string backupPath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonPrintJobStore(string filePath)
    {
        this.filePath = filePath;
        backupPath = filePath + ".bak";
    }

    public async Task<StoreAddResult> TryAddAsync(PrintJob job, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await ReadUnlockedAsync(cancellationToken);
            var existing = snapshot.Jobs.FirstOrDefault(existingJob =>
                string.Equals(existingJob.JobId, job.JobId, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                return new StoreAddResult(false, existing);
            }

            snapshot.Jobs.Add(job);
            await WriteUnlockedAsync(snapshot, cancellationToken);
            return new StoreAddResult(true, job);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<PrintJob?> GetAsync(string jobId, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await ReadUnlockedAsync(cancellationToken);
            return snapshot.Jobs.FirstOrDefault(job =>
                string.Equals(job.JobId, jobId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<PrintJob>> ListAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await ReadUnlockedAsync(cancellationToken);
            return snapshot.Jobs
                .OrderBy(job => job.CreatedAt)
                .ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<PrintJob?> TryLeaseNextDueJobAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await ReadUnlockedAsync(cancellationToken);
            var index = snapshot.Jobs.FindIndex(job =>
                job.Status == PrintJobStatus.Pending &&
                (job.NextAttemptAt is null || job.NextAttemptAt <= now));

            if (index < 0)
            {
                return null;
            }

            var leased = snapshot.Jobs[index].MarkProcessing(now);
            snapshot.Jobs[index] = leased;
            await WriteUnlockedAsync(snapshot, cancellationToken);
            return leased;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task UpdateAsync(PrintJob job, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await ReadUnlockedAsync(cancellationToken);
            var index = snapshot.Jobs.FindIndex(existing =>
                string.Equals(existing.JobId, job.JobId, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                snapshot.Jobs.Add(job);
            }
            else
            {
                snapshot.Jobs[index] = job;
            }

            await WriteUnlockedAsync(snapshot, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<JobStoreSnapshot> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new JobStoreSnapshot();
        }

        try
        {
            return await ReadSnapshotFromPathAsync(filePath, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            var corruptPath = filePath + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
            File.Copy(filePath, corruptPath, overwrite: true);

            if (!File.Exists(backupPath))
            {
                throw new InvalidOperationException(
                    $"Print job queue file '{filePath}' is unreadable and no backup is available. Corrupt copy: {corruptPath}",
                    ex);
            }

            try
            {
                var recovered = await ReadSnapshotFromPathAsync(backupPath, cancellationToken);
                File.Copy(backupPath, filePath, overwrite: true);
                return recovered;
            }
            catch (Exception backupEx) when (backupEx is JsonException or IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    $"Print job queue file '{filePath}' is unreadable and backup recovery failed. Corrupt copy: {corruptPath}",
                    backupEx);
            }
        }
    }

    private async Task WriteUnlockedAsync(JobStoreSnapshot snapshot, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(filePath))
        {
            File.Copy(filePath, backupPath, overwrite: true);
        }

        var temporaryPath = filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken);
        }

        File.Move(temporaryPath, filePath, overwrite: true);
    }

    private static async Task<JobStoreSnapshot> ReadSnapshotFromPathAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<JobStoreSnapshot>(stream, JsonOptions, cancellationToken)
               ?? new JobStoreSnapshot();
    }

    private sealed record JobStoreSnapshot
    {
        public List<PrintJob> Jobs { get; init; } = new();
    }
}
