using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Accounting.Repositories;

public sealed class AccountingOperationalPolicyRepository : IAccountingOperationalPolicyRepository
{
    // Espacio de claves propio: no colisiona con los locks de idempotencia/secuencia del libro.
    private const string LockNamespace = "Accounting.OperationalPolicy";

    private readonly ErpDbContext _context;

    public AccountingOperationalPolicyRepository(ErpDbContext context) => _context = context;

    public async Task<IReadOnlyList<AccountingOperationalPolicy>> ListAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    ) =>
        await _context
            .AccountingOperationalPolicies.Where(x =>
                x.TenantId == tenantId && x.CompanyId == companyId
            )
            .OrderBy(x => x.EffectiveFrom)
            .ToListAsync(ct);

    public async Task AddAsync(AccountingOperationalPolicy policy, CancellationToken ct = default) =>
        await _context.AccountingOperationalPolicies.AddAsync(policy, ct);

    // pg_advisory_xact_lock(_shared)(int4, int4) — ámbito de transacción: se libera al
    // COMMIT/ROLLBACK de la transacción ambiente; nunca abre ni comitea una propia. Sin transacción
    // el lock se liberaría al instante, así que se exige una activa.
    public Task AcquireSharedLockAsync(Guid companyId, CancellationToken ct = default) =>
        AcquireAsync(companyId, shared: true, ct);

    public Task AcquireExclusiveLockAsync(Guid companyId, CancellationToken ct = default) =>
        AcquireAsync(companyId, shared: false, ct);

    private async Task AcquireAsync(Guid companyId, bool shared, CancellationToken ct)
    {
        if (!_context.Database.IsRelational())
            return;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "El lock de la política contable requiere una transacción activa."
            );

        var hash1 = StableHash(System.Text.Encoding.UTF8.GetBytes(LockNamespace));
        var hash2 = StableHash(companyId.ToByteArray());
        if (shared)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock_shared({hash1}, {hash2})",
                ct
            );
        else
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({hash1}, {hash2})",
                ct
            );
    }

    // Hash estable (no depende de HashCode.GetHashCode, no-determinístico en .NET 5+) — mismo
    // algoritmo que JournalEntryRepository/DocumentSequenceRepository.
    private static int StableHash(byte[] bytes)
    {
        int h = 17;
        foreach (var b in bytes)
            h = unchecked(h * 31 + b);
        return h;
    }
}
