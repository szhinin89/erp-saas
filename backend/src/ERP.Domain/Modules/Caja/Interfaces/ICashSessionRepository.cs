using ERP.Domain.Modules.Caja.Entities;

namespace ERP.Domain.Modules.Caja.Interfaces;

public interface ICashSessionRepository
{
    Task<CashSession?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<CashSession?> GetOpenByUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default
    );
    Task<CashSession?> GetOpenByCashRegisterAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Único lock oficial de la sesión de caja activa para todo flujo que registra movimientos en
    /// ella (pago a proveedor y su reversa, reembolso de crédito de proveedor y su reversa):
    /// <c>SELECT ... FOR UPDATE</c> adquirido dentro de la transacción ambiente ya abierta, liberado
    /// al COMMIT/ROLLBACK — nunca abre transacción propia. Reemplaza al anterior FOR SHARE
    /// (P0-02 Fase 8), que con el UPDATE posterior de la sesión producía deadlock entre dos
    /// operaciones concurrentes (ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-02A-FINAL/-CLOSE): con FOR UPDATE
    /// quedan serializadas y, tras esperar el lock, la recarga lee los movimientos ya confirmados por
    /// la otra transacción (saldo vigente). Si el llamador bloquea varias cajas, debe hacerlo en
    /// orden determinista (por <c>CashRegisterId</c>).
    /// </summary>
    Task<CashSession?> GetOpenByCashRegisterForUpdateAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken ct = default
    );

    /// <summary>
    /// ZH-SUPPLIER-PAYMENT-REVERSAL-SEMANTICS-02B-FINAL — mismo lock oficial (FOR UPDATE + recarga
    /// tras el lock) pero sobre una sesión PUNTUAL por Id, en cualquier estado: la reversa documental
    /// de un pago en efectivo debe actuar sobre la sesión ORIGINAL de la línea (nunca sobre otra
    /// sesión abierta de la misma caja) y distinguir "cerrada" de "inexistente". Mismo orden
    /// determinista por <c>CashRegisterId</c> que <see cref="GetOpenByCashRegisterForUpdateAsync"/>.
    /// </summary>
    Task<CashSession?> GetByIdForUpdateAsync(
        Guid tenantId,
        Guid cashSessionId,
        CancellationToken ct = default
    );

    /// <summary>Trazabilidad histórica: true si existe al menos una sesión (apertura) para esta Caja — nunca se borra ni se ignora una vez creada.</summary>
    Task<bool> ExistsByCashRegisterAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken ct = default
    );

    /// <summary>Versión masiva de <see cref="ExistsByCashRegisterAsync"/> para proyecciones de listado — evita N+1.</summary>
    Task<IReadOnlyCollection<Guid>> GetUsedCashRegisterIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> cashRegisterIds,
        CancellationToken ct = default
    );
    Task<(IReadOnlyList<CashSession> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        Guid branchId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task AddAsync(CashSession session, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
