using ERP.Domain.Auth.Entities;

namespace ERP.Application.Common.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken entity, CancellationToken cancellationToken = default);
    Task<PasswordResetToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    );
    Task InvalidateActiveForUserAsync(
        Guid userId,
        string userKind,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    );
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
