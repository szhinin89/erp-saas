using ERP.Domain.Common;

namespace ERP.Domain.Auth.Entities;

/// <summary>
/// Estado global (no multi-tenant) para bootstrap seguro de la primera cuenta operador platform.
/// Columnas: <c>is_first_run</c>, <c>setup_token_hash</c>, <c>setup_token_expiry_utc</c>, <c>completed_at</c> + auditoría.
/// </summary>
public sealed class FirstRunSetupState : SystemAuditableEntity
{
    public bool IsFirstRun { get; private set; }
    public string? SetupTokenHash { get; private set; }
    public DateTime? SetupTokenExpiryUtc { get; private set; }
    /// <summary>UTC en que se completó el first-run (operador platform creado). Null mientras siga activo o sin completar.</summary>
    public DateTime? CompletedAt { get; private set; }

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
        CompletedAt = DateTime.UtcNow;
        SetUpdated(actorId);
    }

    public void ReopenFirstRun(Guid actorId)
    {
        IsFirstRun = true;
        SetupTokenHash = null;
        SetupTokenExpiryUtc = null;
        CompletedAt = null;
        SetUpdated(actorId);
    }
}
