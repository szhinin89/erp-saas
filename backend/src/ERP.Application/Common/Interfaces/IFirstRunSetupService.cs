namespace ERP.Application.Common.Interfaces;

public sealed record FirstRunTokenIssueResult(
    bool IsFirstRun,
    bool TokenGenerated,
    string? PlainToken,
    DateTime? ExpiresAtUtc);

public sealed record FirstRunResetResult(
    string Message,
    int RemovedSuperAdmins,
    string SetupToken,
    DateTime ExpiresAtUtc);

public interface IFirstRunSetupService
{
    Task<FirstRunTokenIssueResult> EnsureTokenIssuedAsync(CancellationToken ct = default);
    Task<bool> ValidateSetupTokenAsync(string? submittedToken, CancellationToken ct = default);
    /// <summary>
    /// Marca first-run como completado: <c>is_first_run = false</c>, <c>completed_at = UtcNow</c>,
    /// y limpia <c>setup_token_hash</c> / <c>setup_token_expiry_utc</c>. Llamar tras persistir el SuperAdmin.
    /// </summary>
    Task MarkFirstRunCompletedAsync(CancellationToken ct = default);
    Task<FirstRunResetResult> ResetForDevelopmentAsync(CancellationToken ct = default);
}
