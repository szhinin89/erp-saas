using ERP.Domain.Common;

namespace ERP.Domain.Modules.Fiscal.Entities;

public sealed class InvoiceElectronic
{
    public const int StatusMaxLen = 20;
    public const int PathMaxLen = 500;
    public const int ErrorMaxLen = 1000;

    public static class SriStatuses
    {
        public const string Pending = "Pending";
        public const string Signed = "Signed";
        public const string Sent = "Sent";
        public const string Authorized = "Authorized";
        public const string Rejected = "Rejected";
        public const string RetryPending = "RetryPending";
        public const string DeadLetter = "DeadLetter";
    }

    public long Id { get; private set; }
    public long InvoiceId { get; private set; }
    public Guid SubscriberId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Status { get; private set; } = SriStatuses.Pending;
    public string? AuthorizationNumber { get; private set; }
    public DateTime? AuthorizationDate { get; private set; }
    public string? XmlSignedPath { get; private set; }
    public string? XmlAuthorizedPath { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateOnly IssueDate { get; private set; }

    private InvoiceElectronic() { }

    public static InvoiceElectronic CreatePending(
        long id,
        long invoiceId,
        Guid subscriberId,
        Guid branchId,
        DateOnly issueDate)
    {
        return new InvoiceElectronic
        {
            Id = id,
            InvoiceId = invoiceId,
            SubscriberId = subscriberId,
            BranchId = branchId,
            IssueDate = issueDate,
            Status = SriStatuses.Pending,
        };
    }

    public void MarkSigned(string? xmlSignedPath)
    {
        Status = SriStatuses.Signed;
        XmlSignedPath = xmlSignedPath;
    }

    public void MarkSent() => Status = SriStatuses.Sent;

    public void MarkAuthorized(string authNumber, DateTime authDate, string? xmlAuthPath)
    {
        Status = SriStatuses.Authorized;
        AuthorizationNumber = authNumber;
        AuthorizationDate = authDate;
        XmlAuthorizedPath = xmlAuthPath;
        ErrorMessage = null;
    }

    public void MarkRejected(string errorMessage)
    {
        Status = SriStatuses.Rejected;
        ErrorMessage = errorMessage?.Trim();
    }

    public void MarkSendError(string errorMessage)
    {
        Status = SriStatuses.RetryPending;
        ErrorMessage = errorMessage?.Trim();
    }

    internal void AssignIdIfNeeded(ISnowflakeIdGenerator idGenerator)
    {
        if (Id == 0)
            Id = idGenerator.NextId();
    }
}
