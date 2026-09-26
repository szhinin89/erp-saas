using ERP.Application.Common;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Caja;

public sealed class CashSessionRepository : ICashSessionRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public CashSessionRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<CashSession> Scoped(Guid tenantId) =>
        _db.CashSessions.ForOperationalScope(tenantId, _company);

    public Task<CashSession?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Include(x => x.Movements.OrderBy(m => m.CreatedAt))
            .Include(x => x.ClosingCounts)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<CashSession?> GetOpenByUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Include(x => x.Movements)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Status == CashSessionStatus.Open, ct);

    public Task<CashSession?> GetOpenByCashRegisterAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Include(x => x.Movements)
            .FirstOrDefaultAsync(
                x => x.CashRegisterId == cashRegisterId && x.Status == CashSessionStatus.Open,
                ct
            );

    public async Task<CashSession?> GetOpenByCashRegisterForUpdateAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken ct = default
    )
    {
        // Un FOR UPDATE no admite ORDER BY/LIMIT en la misma sentencia bloqueada sin ambigüedad de
        // fila — se bloquean todas las sesiones abiertas de la caja (a lo sumo una por invariante de
        // negocio) y luego se recarga con el método ya existente. La recarga ocurre DESPUÉS de
        // obtener el lock, así ve el saldo que dejó la transacción que lo tenía.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM cash_sessions WHERE tenant_id = {tenantId} AND cash_register_id = {cashRegisterId} AND status = {(int)CashSessionStatus.Open} FOR UPDATE",
            ct
        );
        var session = await GetOpenByCashRegisterAsync(tenantId, cashRegisterId, ct);
        // 02A-FINAL-CLOSE — si el llamador ya tenía esta sesión trackeada ANTES del lock (p. ej.
        // ReverseSupplierCreditRefund la lee por Id para resolver la caja), la query de arriba
        // devuelve esa misma instancia por identity resolution, con escalares y xmin (concurrency
        // token) de antes del lock → el UPDATE posterior afectaría 0 filas y fallaría con
        // DbUpdateConcurrencyException aunque el lock ya serializó correctamente. Recargar tras el
        // lock garantiza el contrato para cualquier llamador: estado vigente bajo el lock (los
        // movimientos nuevos ya llegan por el Include de la query).
        if (session is not null)
            await _db.Entry(session).ReloadAsync(ct);
        return session;
    }

    public Task<bool> ExistsByCashRegisterAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken ct = default
    ) =>
        _db
            .CashSessions.AsPlatformQuery()
            .AnyAsync(x => x.TenantId == tenantId && x.CashRegisterId == cashRegisterId, ct);

    public async Task<IReadOnlyCollection<Guid>> GetUsedCashRegisterIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> cashRegisterIds,
        CancellationToken ct = default
    )
    {
        if (cashRegisterIds.Count == 0)
            return Array.Empty<Guid>();

        return await _db
            .CashSessions.AsPlatformQuery()
            .Where(x => x.TenantId == tenantId && cashRegisterIds.Contains(x.CashRegisterId))
            .Select(x => x.CashRegisterId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<CashSession> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        Guid branchId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var q = Scoped(tenantId).Where(x => x.BranchId == branchId);

        if (
            !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<CashSessionStatus>(status.Trim(), true, out var ss)
        )
            q = q.Where(x => x.Status == ss);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.OpenedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.Movements)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task AddAsync(CashSession session, CancellationToken ct = default) =>
        _db.CashSessions.AddAsync(session, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
