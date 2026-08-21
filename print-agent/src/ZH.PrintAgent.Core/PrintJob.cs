using ZH.PrintAgent.Contracts;

namespace ZH.PrintAgent.Core;

public sealed record PrintJob
{
    public required string JobId { get; init; }
    public required string PrinterName { get; init; }
    public int Copies { get; init; } = 1;
    public required ReceiptDocument Receipt { get; init; }
    public PrintJobStatus Status { get; init; } = PrintJobStatus.Pending;
    public int Attempts { get; init; }
    public string? LastError { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? ProcessingStartedAt { get; init; }
    public DateTimeOffset? NextAttemptAt { get; init; }

    public static PrintJob Create(SubmitPrintJobRequest request, DateTimeOffset now)
    {
        return new PrintJob
        {
            JobId = request.JobId.Trim(),
            PrinterName = request.PrinterName.Trim(),
            Copies = Math.Max(1, request.Copies),
            Receipt = request.Receipt ?? throw new ArgumentException("receipt is required.", nameof(request)),
            Status = PrintJobStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public PrintJob MarkProcessing(DateTimeOffset now)
    {
        return this with
        {
            Status = PrintJobStatus.Processing,
            Attempts = Attempts + 1,
            ProcessingStartedAt = now,
            UpdatedAt = now,
            LastError = null
        };
    }

    public PrintJob MarkPrinted(DateTimeOffset now)
    {
        return this with
        {
            Status = PrintJobStatus.Printed,
            UpdatedAt = now,
            ProcessingStartedAt = null,
            NextAttemptAt = null,
            LastError = null
        };
    }

    public PrintJob MarkFailed(string error, DateTimeOffset now, int maxAttempts, TimeSpan baseBackoff)
    {
        var finalFailure = Attempts >= maxAttempts;
        return this with
        {
            Status = finalFailure ? PrintJobStatus.Failed : PrintJobStatus.Pending,
            LastError = error,
            UpdatedAt = now,
            ProcessingStartedAt = null,
            NextAttemptAt = finalFailure ? null : now.Add(BackoffForAttempt(Attempts, baseBackoff))
        };
    }

    public PrintJob MarkCancelled(DateTimeOffset now)
    {
        return this with
        {
            Status = PrintJobStatus.Cancelled,
            UpdatedAt = now,
            ProcessingStartedAt = null,
            NextAttemptAt = null
        };
    }

    public PrintJob MarkManualRetry(DateTimeOffset now)
    {
        return this with
        {
            Status = PrintJobStatus.Pending,
            Attempts = 0,
            LastError = null,
            ProcessingStartedAt = null,
            NextAttemptAt = null,
            UpdatedAt = now
        };
    }

    public PrintJob MarkNeedsReview(DateTimeOffset now, string reason)
    {
        return this with
        {
            Status = PrintJobStatus.NeedsReview,
            ProcessingStartedAt = null,
            NextAttemptAt = null,
            LastError = reason,
            UpdatedAt = now
        };
    }

    private static TimeSpan BackoffForAttempt(int attempts, TimeSpan baseBackoff)
    {
        var exponent = Math.Clamp(attempts - 1, 0, 6);
        return TimeSpan.FromMilliseconds(baseBackoff.TotalMilliseconds * Math.Pow(2, exponent));
    }
}
