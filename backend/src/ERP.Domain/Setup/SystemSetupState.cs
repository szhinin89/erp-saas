namespace ERP.Domain.Setup;

/// <summary>
/// Singleton row (Id = 1) that guards the first-run initialization gate.
/// Once IsInitialized = true, POST /api/setup/admin returns 409.
/// </summary>
public sealed class SystemSetupState
{
    public int Id { get; private set; } = 1;
    public bool IsInitialized { get; private set; }
    public DateTime? InitializedAtUtc { get; private set; }
    public string? AdminEmail { get; private set; }

    public bool IsFirstRun { get; private set; } = true;
    public string? SetupTokenHash { get; private set; }
    public DateTime? SetupTokenExpiryUtc { get; private set; }

    private SystemSetupState() { }

    public static SystemSetupState CreateNew() => new();

    /// <summary>Issues a new single-use setup token, replacing any previous one.</summary>
    public void IssueSetupToken(string tokenHash, DateTime expiryUtc)
    {
        if (IsInitialized)
            throw new InvalidOperationException("El sistema ya ha sido inicializado.");

        SetupTokenHash = tokenHash;
        SetupTokenExpiryUtc = expiryUtc;
        IsFirstRun = true;
    }

    public bool HasActiveSetupToken(DateTime utcNow)
        => !IsInitialized
        && SetupTokenHash is not null
        && SetupTokenExpiryUtc is not null
        && SetupTokenExpiryUtc.Value > utcNow;

    public void MarkInitialized(string adminEmail)
    {
        IsInitialized = true;
        InitializedAtUtc = DateTime.UtcNow;
        AdminEmail = adminEmail;

        // Setup token is single-use — invalidate permanently.
        IsFirstRun = false;
        SetupTokenHash = null;
        SetupTokenExpiryUtc = null;
    }
}
