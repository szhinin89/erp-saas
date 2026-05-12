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
    Task MarkFirstRunCompletedAsync(CancellationToken ct = default);
    Task<FirstRunResetResult> ResetForDevelopmentAsync(CancellationToken ct = default);
}
