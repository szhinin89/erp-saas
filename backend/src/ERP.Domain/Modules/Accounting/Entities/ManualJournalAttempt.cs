using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>Append-only result of one explicit manual publication request.</summary>
public sealed class ManualJournalAttempt : ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid JournalEntryId { get; private set; }
    public DateTime AttemptedAtUtc { get; private set; }
    public Guid AttemptedBy { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public bool Success { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int ExpectedVersion { get; private set; }
    public int ResultVersion { get; private set; }
    public string ResultStatus { get; private set; } = null!;
    public int? EntryNumber { get; private set; }

    private ManualJournalAttempt() { }

    public static ManualJournalAttempt Create(JournalEntry entry, Guid actor, Guid key,
        int expectedVersion, bool success, string? code, string? error) => new()
    {
        Id = Guid.NewGuid(), TenantId = entry.TenantId, CompanyId = entry.CompanyId,
        JournalEntryId = entry.Id, AttemptedAtUtc = DateTime.UtcNow, AttemptedBy = actor,
        IdempotencyKey = key, ExpectedVersion = expectedVersion, Success = success,
        ErrorCode = code, ErrorMessage = error, ResultVersion = entry.Version,
        ResultStatus = entry.Status.ToString(), EntryNumber = entry.EntryNumber,
    };
}
