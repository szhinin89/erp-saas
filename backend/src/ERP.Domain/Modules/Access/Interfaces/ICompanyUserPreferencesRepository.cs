using ERP.Domain.Access.Entities;

namespace ERP.Domain.Access.Interfaces;

public interface ICompanyUserPreferencesRepository
{
    /// <summary>Relación 1:1 con la membresía — devuelve una única fila o ninguna.</summary>
    Task<CompanyUserPreferences?> GetByMembershipAsync(
        Guid companyUserMembershipId,
        CancellationToken cancellationToken = default
    );

    Task<bool> ExistsAsync(
        Guid companyUserMembershipId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(CompanyUserPreferences entity, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
