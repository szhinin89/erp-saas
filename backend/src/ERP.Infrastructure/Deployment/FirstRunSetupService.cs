using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Deployment;

public sealed class FirstRunSetupService : IFirstRunSetupService
{
    private static readonly Guid SystemActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly ErpDbContext _db;

    public FirstRunSetupService(ErpDbContext db)
    {
        _db = db;
    }

    public async Task<FirstRunTokenIssueResult> EnsureTokenIssuedAsync(CancellationToken ct = default)
    {
        var state = await GetOrCreateStateAsync(ct);

        // Si ya existe SuperAdmin, cerramos first-run de forma defensiva.
        var hasSuperAdmin = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Role == "SuperAdmin", ct);
        if (hasSuperAdmin && state.IsFirstRun)
        {
            state.CompleteFirstRun(SystemActorId);
            await _db.SaveChangesAsync(ct);
            return new FirstRunTokenIssueResult(false, false, null, null);
        }

        if (!state.IsFirstRun)
            return new FirstRunTokenIssueResult(false, false, null, null);

        var expired = !state.SetupTokenExpiryUtc.HasValue || state.SetupTokenExpiryUtc.Value <= DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(state.SetupTokenHash) && !expired)
            return new FirstRunTokenIssueResult(true, false, null, state.SetupTokenExpiryUtc);

        var plainToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = ComputeSha256(plainToken);
        var expiry = DateTime.UtcNow.AddMinutes(15);

        state.IssueToken(tokenHash, expiry, SystemActorId);
        await _db.SaveChangesAsync(ct);

        return new FirstRunTokenIssueResult(true, true, plainToken, expiry);
    }

    public async Task<bool> ValidateSetupTokenAsync(string? submittedToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(submittedToken))
            return false;

        var state = await GetOrCreateStateAsync(ct);
        if (!state.IsFirstRun)
            return false;
        if (string.IsNullOrWhiteSpace(state.SetupTokenHash))
            return false;
        if (!state.SetupTokenExpiryUtc.HasValue || state.SetupTokenExpiryUtc.Value <= DateTime.UtcNow)
            return false;

        var submittedHash = ComputeSha256(submittedToken.Trim());
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(state.SetupTokenHash),
            Encoding.UTF8.GetBytes(submittedHash));
    }

    public async Task MarkFirstRunCompletedAsync(CancellationToken ct = default)
    {
        var state = await GetOrCreateStateAsync(ct);
        if (!state.IsFirstRun)
            return;

        state.CompleteFirstRun(SystemActorId);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<FirstRunResetResult> ResetForDevelopmentAsync(CancellationToken ct = default)
    {
        var superAdmins = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Role == "SuperAdmin")
            .ToListAsync(ct);
        var superAdminIds = superAdmins.Select(x => x.Id).ToArray();

        if (superAdmins.Count > 0)
            _db.Users.RemoveRange(superAdmins);

        var superAdminRefreshTokens = await _db.RefreshTokens
            .Where(rt => rt.UserType == RefreshToken.TypeSuperAdmin || superAdminIds.Contains(rt.UserId))
            .ToListAsync(ct);
        if (superAdminRefreshTokens.Count > 0)
            _db.RefreshTokens.RemoveRange(superAdminRefreshTokens);

        var state = await GetOrCreateStateAsync(ct);
        state.ReopenFirstRun(SystemActorId);
        await _db.SaveChangesAsync(ct);

        var token = await EnsureTokenIssuedAsync(ct);
        if (string.IsNullOrWhiteSpace(token.PlainToken) || token.ExpiresAtUtc is null)
            throw new InvalidOperationException("No se pudo generar token de first-run tras reset.");

        return new FirstRunResetResult(
            Message: "First-run state reset",
            RemovedSuperAdmins: superAdmins.Count,
            SetupToken: token.PlainToken,
            ExpiresAtUtc: token.ExpiresAtUtc.Value);
    }

    private async Task<FirstRunSetupState> GetOrCreateStateAsync(CancellationToken ct)
    {
        var state = await _db.FirstRunSetupStates.FirstOrDefaultAsync(ct);
        if (state is not null)
            return state;

        state = FirstRunSetupState.Create(SystemActorId);
        await _db.FirstRunSetupStates.AddAsync(state, ct);
        await _db.SaveChangesAsync(ct);
        return state;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
