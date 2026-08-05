using ERP.Application.Common;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Sales;

public sealed class SalesReturnRepository : ISalesReturnRepository
{
    // Namespace de hash independiente del usado por Accounting
    // (IJournalEntryRepository.AcquireIdempotencyLockAsync) para que los advisory lock keys de
    // ambos módulos no colisionen — mismo criterio explícito exigido por el plan de
    // implementación (Fase 3).
    private const string LockNamespace = "SalesReturn.Lock";

    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public SalesReturnRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<SalesReturn> Scoped(Guid tenantId) =>
        _db.SalesReturns.ForOperationalScope(tenantId, _company);

    public Task<SalesReturn?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Include(x => x.Lines)
            .Include(x => x.RefundAllocations)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<SalesReturn> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var q = Scoped(tenantId);

        if (
            !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<SalesReturnStatus>(status.Trim(), true, out var parsedStatus)
        )
            q = q.Where(x => x.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.ReturnNumber.Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.Lines)
            .Include(x => x.RefundAllocations)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<decimal> GetReturnedQuantityByInvoiceDetailAsync(
        Guid tenantId,
        Guid invoiceDetailId,
        CancellationToken ct = default
    ) =>
        (
            from d in _db.SalesReturnDetails
            join r in _db.SalesReturns on d.ReturnId equals r.Id
            where
                d.TenantId == tenantId
                && d.OriginalInvoiceDetailId == invoiceDetailId
                && r.TenantId == tenantId
                && r.Status == SalesReturnStatus.Authorized
            select d.Quantity
        ).SumAsync(ct);

    public async Task AcquireReturnLockAsync(
        Guid tenantId,
        Guid salesInvoiceId,
        CancellationToken ct = default
    )
    {
        // Advisory lock de transacción — serializa concurrentes para la misma factura
        // (TenantId, SalesInvoiceId) sin bloquear devoluciones de otras facturas.
        // pg_advisory_xact_lock(int4, int4) se libera automáticamente al hacer
        // COMMIT/ROLLBACK de la transacción ambiente — nunca abre ni comitea una transacción
        // propia (la persistencia pertenece exclusivamente a ErpDbContext.SaveChangesAsync).
        var hash1 = StableHash(
            System
                .Text.Encoding.UTF8.GetBytes(LockNamespace)
                .Concat(tenantId.ToByteArray())
                .ToArray()
        );
        var hash2 = StableHash(salesInvoiceId.ToByteArray());
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({hash1}, {hash2})",
            ct
        );
    }

    // Hash estable (no depende de HashCode.GetHashCode, no-determinístico en .NET 5+) — mismo
    // algoritmo que JournalEntryRepository/DocumentSequenceRepository, duplicado
    // deliberadamente (sin helper compartido: un solo consumidor adicional no justifica
    // extraerlo todavía). Un hash de 32 bits puede colisionar en teoría; una colisión solo
    // produce una espera adicional entre dos claves de negocio distintas (falso positivo de
    // contención), nunca un duplicado.
    private static int StableHash(byte[] bytes)
    {
        int h = 17;
        foreach (var b in bytes)
            h = unchecked(h * 31 + b);
        return h;
    }

    public Task AddAsync(SalesReturn salesReturn, CancellationToken ct = default) =>
        _db.SalesReturns.AddAsync(salesReturn, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
