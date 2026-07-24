using ERP.Domain.Modules.Caja.Entities;

namespace ERP.Domain.Modules.Caja.Interfaces;

public interface ICashSessionRepository
{
    Task<CashSession?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<CashSession?> GetOpenByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<CashSession?> GetOpenByCashRegisterAsync(Guid tenantId, Guid cashRegisterId, CancellationToken ct = default);
    /// <summary>Trazabilidad histórica: true si existe al menos una sesión (apertura) para esta Caja — nunca se borra ni se ignora una vez creada.</summary>
    Task<bool> ExistsByCashRegisterAsync(Guid tenantId, Guid cashRegisterId, CancellationToken ct = default);
    /// <summary>Versión masiva de <see cref="ExistsByCashRegisterAsync"/> para proyecciones de listado — evita N+1.</summary>
    Task<IReadOnlyCollection<Guid>> GetUsedCashRegisterIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> cashRegisterIds, CancellationToken ct = default);
    Task<(IReadOnlyList<CashSession> Items, int Total)> GetPagedAsync(Guid tenantId, string? status, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(CashSession session, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
