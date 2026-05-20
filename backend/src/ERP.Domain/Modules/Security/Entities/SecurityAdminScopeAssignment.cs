using ERP.Domain.Common;

namespace ERP.Domain.Security.Entities;

public class SecurityAdminScopeAssignment : AuditableEntity
{
    public string SubjectType { get; private set; } = null!; // "Role" | "User"
    public string SubjectKey { get; private set; } = null!;  // RoleName or UserId (Guid string)
    public int Scope { get; private set; }                   // SecurityAdminScope enum int
    public bool IsAllowed { get; private set; }

    private SecurityAdminScopeAssignment() { }

    public static SecurityAdminScopeAssignment Create(
        Guid subscriberId,
        string subjectType,
        string subjectKey,
        int scope,
        bool isAllowed,
        Guid createdBy)
    {
        var entity = new SecurityAdminScopeAssignment
        {
            Id = Guid.NewGuid(),
            SubscriberId = subscriberId,
            SubjectType = subjectType,
            SubjectKey = subjectKey,
            Scope = scope,
            IsAllowed = isAllowed
        };
        entity.SetCreated(createdBy);
        return entity;
    }

    public void SetAllowed(bool isAllowed, Guid updatedBy)
    {
        IsAllowed = isAllowed;
        SetUpdated(updatedBy);
    }
}

public enum SecurityAdminScope
{
    ManageRoles = 1,
    ManageModules = 2,
    ManageScreens = 3,
    ManageProcesses = 4,
}

