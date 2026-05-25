using ERP.Domain.Common;

namespace ERP.Domain.Modules.Integration.Entities;

/// <summary>
/// Relación documental desacoplada entre bounded contexts (sin FK rígidas cross-BC).
/// </summary>
public sealed class DocumentRelation : SnowflakeAuditableEntity
{
    public const int ModuleMaxLen = 40;
    public const int RelationTypeMaxLen = 40;

    public Guid BranchId { get; private set; }
    public string SourceModule { get; private set; } = null!;
    public long SourceId { get; private set; }
    public string TargetModule { get; private set; } = null!;
    public long? TargetId { get; private set; }
    public string RelationType { get; private set; } = null!;

    private DocumentRelation() { }

    public static DocumentRelation Create(
        long id,
        Guid publicId,
        Guid subscriberId,
        Guid branchId,
        string sourceModule,
        long sourceId,
        string targetModule,
        long? targetId,
        string relationType,
        Guid createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(relationType);

        var relation = new DocumentRelation
        {
            Id = id,
            PublicId = publicId,
            SubscriberId = subscriberId,
            CompanyId = branchId,
            BranchId = branchId,
            SourceModule = sourceModule.Trim(),
            SourceId = sourceId,
            TargetModule = targetModule.Trim(),
            TargetId = targetId,
            RelationType = relationType.Trim(),
        };
        relation.SetCreated(createdBy);
        return relation;
    }

    public void LinkTarget(long targetId, Guid userId)
    {
        TargetId = targetId;
        SetUpdated(userId);
    }
}
