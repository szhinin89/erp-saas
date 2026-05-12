using ERP.Domain.Auth.Entities;

namespace ERP.Domain.Auth.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<User?> GetByIdSystemAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default);
    Task<User?> GetByEmailSystemAsync(string email, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<User?> GetSingleSuperAdminByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> AnySuperAdminAsync(CancellationToken ct = default);
    Task<int> CountAllSystemAsync(CancellationToken ct = default);
    Task<int> CountActiveSystemAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(string email, Guid tenantId, CancellationToken ct = default);

    /// <summary>Indica si ya existe un usuario legacy (<c>users</c>) con ese email en cualquier tenant (ignora filtro global).</summary>
    Task<bool> ExistsByEmailGloballyAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Usuarios legacy (no SuperAdmin) con el mismo email en cualquier empresa activa.
    /// Usado para recuperación de contraseña sin <c>tenantId</c> en el formulario.
    /// </summary>
    Task<IReadOnlyList<User>> GetNonSuperAdminLegacyUsersByEmailAsync(string email, CancellationToken ct = default);

    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
