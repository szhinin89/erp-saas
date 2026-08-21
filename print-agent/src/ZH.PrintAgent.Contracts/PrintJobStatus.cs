namespace ZH.PrintAgent.Contracts;

public enum PrintJobStatus
{
    Pending = 0,
    Processing = 1,
    Printed = 2,
    Failed = 3,
    Cancelled = 4,
    NeedsReview = 5
}
