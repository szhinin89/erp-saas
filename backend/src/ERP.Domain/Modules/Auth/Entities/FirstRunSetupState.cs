using ERP.Domain.Common;

namespace ERP.Domain.Auth.Entities;

/// <summary>
/// Estado global (no multi-tenant) para bootstrap seguro de la primera cuenta SuperAdmin.
/// </summary>
public sealed class FirstRunSetupState : SystemAuditableEntity
{
    public bool IsFirstRun { get; private set; }
    public string? SetupTokenHash { get; private set; }
    public DateTime? SetupTokenExpiryUtc { get; private set; }

    private FirstRunSetupState() { }

    public static FirstRunSetupState Create(Guid actorId)
    {
        var row = new FirstRunSetupState
        {
            Id = Guid.NewGuid(),
            IsFirstRun = true
        };
        row.SetCreated(actorId);
        return row;
    }

    public void IssueToken(string setupTokenHash, DateTime expiryUtc, Guid actorId)
    {
        SetupTokenHash = setupTokenHash;
        SetupTokenExpiryUtc = expiryUtc;
        SetUpdated(actorId);
    }

    public void CompleteFirstRun(Guid actorId)
    {
        IsFirstRun = false;
        SetupTokenHash = null;
        SetupTokenExpiryUtc = null;
        SetUpdated(actorId);
    }

    public void ReopenFirstRun(Guid actorId)
    {
        IsFirstRun = true;
        SetupTokenHash = null;
        SetupTokenExpiryUtc = null;
        SetUpdated(actorId);
    }
}
