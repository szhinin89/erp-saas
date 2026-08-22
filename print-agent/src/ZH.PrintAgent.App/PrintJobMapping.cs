using ZH.PrintAgent.Contracts;
using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.App;

public static class PrintJobMapping
{
    public static PrintJobResponse ToResponse(this PrintJob job, bool duplicate)
    {
        return new PrintJobResponse
        {
            JobId = job.JobId,
            PrinterName = job.PrinterName,
            Status = job.Status,
            Duplicate = duplicate,
            Attempts = job.Attempts,
            LastError = job.LastError,
            Reviewed = job.Reviewed,
            ReviewedAt = job.ReviewedAt,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
            NextAttemptAt = job.NextAttemptAt
        };
    }
}
