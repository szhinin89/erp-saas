namespace ERP.Domain.Platform.Provisioning;

/// <summary>
/// Immutable audit record for every step in the platform first-run provisioning flow.
/// Written sequentially: Started → OwnerCreated → OperatorCreated → Completed (or Failed).
/// </summary>
public sealed class PlatformProvisioningAuditEvent
{
    public const int InstanceIdMaxLen  = 200;
    public const int MetadataMaxLen    = 8000;
    public const int ErrorMessageMaxLen = 2000;

    public Guid     Id              { get; private set; }
    public ProvisioningEventType EventType { get; private set; }
    public DateTime TimestampUtc    { get; private set; }
    public Guid?    SubscriberId    { get; private set; }
    public Guid?    CompanyId       { get; private set; }
    public Guid?    OperatorUserId  { get; private set; }
    /// <summary>Machine name + PID that ran the provisioning command.</summary>
    public string?  InstanceId      { get; private set; }
    /// <summary>JSONB extra context (taxId, slug, etc.).</summary>
    public string?  Metadata        { get; private set; }
    public bool     IsSuccess       { get; private set; }
    public string?  ErrorMessage    { get; private set; }
    public DateTime CreatedAt       { get; private set; }

    private PlatformProvisioningAuditEvent() { }

    public static PlatformProvisioningAuditEvent Record(
        ProvisioningEventType eventType,
        bool isSuccess,
        string? instanceId      = null,
        Guid? subscriberId      = null,
        Guid? companyId         = null,
        Guid? operatorUserId    = null,
        string? metadata        = null,
        string? errorMessage    = null)
    {
        var now = DateTime.UtcNow;
        return new PlatformProvisioningAuditEvent
        {
            Id             = Guid.NewGuid(),
            EventType      = eventType,
            TimestampUtc   = now,
            SubscriberId   = subscriberId,
            CompanyId      = companyId,
            OperatorUserId = operatorUserId,
            InstanceId     = instanceId?.Length > InstanceIdMaxLen
                                ? instanceId[..InstanceIdMaxLen]
                                : instanceId,
            Metadata       = metadata,
            IsSuccess      = isSuccess,
            ErrorMessage   = errorMessage?.Length > ErrorMessageMaxLen
                                ? errorMessage[..ErrorMessageMaxLen]
                                : errorMessage,
            CreatedAt      = now,
        };
    }
}

public enum ProvisioningEventType
{
    ProvisioningStarted  = 1,
    LockAcquired         = 2,
    OwnerCreated         = 3,
    OperatorCreated      = 4,
    ProvisioningCompleted = 5,
    ProvisioningFailed   = 6,
    ProvisioningReset    = 7,
    LockExpiredRecovered = 8,
}
