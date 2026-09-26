using ERP.Application.Common;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Caja;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B — implementación de <see cref="ICashFundingRequestRepository"/>.
/// Todas las lecturas pasan por <c>ForOperationalScope</c> (tenant + empresa operativa, fail-closed).
/// </summary>
public sealed class CashFundingRequestRepository : ICashFundingRequestRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public CashFundingRequestRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<CashFundingRequest> Scoped(Guid tenantId) =>
        _db.CashFundingRequests.ForOperationalScope(tenantId, _company);

    public Task<CashFundingRequest?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        Scoped(tenantId).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<CashFundingRequest?> GetByIdForUpdateAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    )
    {
        // Mismo patrón oficial que CashSessionRepository.GetByIdForUpdateAsync: lock → recarga →
        // Reload (si el llamador ya tenía la entidad trackeada antes del lock, sus escalares/xmin
        // serían previos al lock). Orden de locks: la CashSession objetivo SIEMPRE antes que esto.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM cash_funding_requests WHERE tenant_id = {tenantId} AND id = {id} FOR UPDATE",
            ct
        );
        var request = await GetByIdAsync(tenantId, id, ct);
        if (request is not null)
            await _db.Entry(request).ReloadAsync(ct);
        return request;
    }

    public Task<CashFundingRequest?> GetByClientRequestIdAsync(
        Guid tenantId,
        Guid clientRequestId,
        CancellationToken ct = default
    ) => Scoped(tenantId).AsNoTracking().FirstOrDefaultAsync(x => x.ClientRequestId == clientRequestId, ct);

    public async Task<IReadOnlyList<CashFundingRequest>> ListBySessionAsync(
        Guid tenantId,
        Guid cashSessionId,
        CashFundingRequestStatus status,
        CancellationToken ct = default
    ) =>
        await Scoped(tenantId)
            .AsNoTracking()
            .Where(x => x.CashSessionId == cashSessionId && x.Status == status)
            .OrderBy(x => x.RequestedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CashFundingRequest>> ListByRequesterAsync(
        Guid tenantId,
        Guid requestedByUserId,
        CashFundingRequestStatus status,
        CancellationToken ct = default
    ) =>
        await Scoped(tenantId)
            .AsNoTracking()
            .Where(x => x.RequestedByUserId == requestedByUserId && x.Status == status)
            .OrderByDescending(x => x.RequestedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

    public Task AddAsync(CashFundingRequest request, CancellationToken ct = default) =>
        _db.CashFundingRequests.AddAsync(request, ct).AsTask();
}
