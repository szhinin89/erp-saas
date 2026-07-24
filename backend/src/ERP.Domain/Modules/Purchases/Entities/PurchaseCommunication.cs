using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

public sealed class PurchaseCommunication : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int SubjectMaxLen = 200;
    public const int NotesMaxLen   = 500;

    public Guid    CompanyId    { get; private set; }
    public Guid    PurchaseId   { get; private set; }
    public string  Subject      { get; private set; } = null!;
    public string? Notes        { get; private set; }
    public string  Status       { get; private set; } = "pending";
    public DateOnly? ScheduledDate { get; private set; }

    private PurchaseCommunication() { }

    public static PurchaseCommunication Create(
        Guid tenantId, Guid companyId, Guid purchaseId,
        string subject, Guid createdBy, string? notes = null, DateOnly? scheduledDate = null)
    {
        var c = new PurchaseCommunication
        {
            Id            = Guid.NewGuid(),
            TenantId      = tenantId,
            CompanyId     = companyId,
            PurchaseId    = purchaseId,
            Subject       = subject.Trim(),
            Notes         = notes?.Trim(),
            Status        = "pending",
            ScheduledDate = scheduledDate,
        };
        c.SetCreated(createdBy);
        return c;
    }
}
