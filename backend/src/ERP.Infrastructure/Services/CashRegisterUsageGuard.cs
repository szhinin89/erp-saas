using ERP.Application.Modules.Caja;
using ERP.Domain.Modules.Caja.Interfaces;

namespace ERP.Infrastructure.Services;

/// <inheritdoc cref="ICashRegisterUsageGuard"/>
public sealed class CashRegisterUsageGuard : ICashRegisterUsageGuard
{
    private readonly ICashSessionRepository _sessions;

    public CashRegisterUsageGuard(ICashSessionRepository sessions) => _sessions = sessions;

    public Task<bool> HasHistoryAsync(Guid tenantId, Guid cashRegisterId, CancellationToken ct = default)
        => _sessions.ExistsByCashRegisterAsync(tenantId, cashRegisterId, ct);

    public async Task<bool> HasOpenSessionAsync(Guid tenantId, Guid cashRegisterId, CancellationToken ct = default)
        => await _sessions.GetOpenByCashRegisterAsync(tenantId, cashRegisterId, ct) is not null;

    public Task<IReadOnlyCollection<Guid>> GetUsedIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> cashRegisterIds, CancellationToken ct = default)
        => _sessions.GetUsedCashRegisterIdsAsync(tenantId, cashRegisterIds, ct);
}
