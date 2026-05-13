using ERP.Domain.Auth.Entities;

namespace ERP.Application.Common.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken entity, CancellationToken ct = default);
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task InvalidateActiveForUserAsync(Guid userId, string userKind, Guid? tenantId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
