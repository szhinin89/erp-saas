using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Accounting.Repositories;

public sealed class AccountRepository : IAccountRepository
{
    private readonly ErpDbContext _context;

    public AccountRepository(ErpDbContext context)
    {
        _context = context;
    }

    private IQueryable<Account> Scoped(Guid tenantId, Guid companyId) =>
        _context.Accounts.Where(x => x.TenantId == tenantId && x.CompanyId == companyId);

    public Task<Account?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    ) => Scoped(tenantId, companyId).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Account?> GetByIdForShareAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    )
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM accounts WHERE id = {id} AND tenant_id = {tenantId} AND company_id = {companyId} FOR SHARE",
            ct
        );
        return await GetByIdAsync(tenantId, companyId, id, ct);
    }

    public async Task<IReadOnlyList<Account>> GetByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    )
    {
        // ACCOUNTING-CHART-CANONICAL-HIERARCHY-01: orden natural por segmentos de código (no
        // lexicográfico simple — "1.1.2" debe ordenar antes que "1.1.10"). No traducible a SQL,
        // así que se ordena en memoria tras materializar; el Plan de Cuentas de una Company es
        // acotado (decenas/pocos cientos de filas), no un dataset masivo.
        var accounts = await Scoped(tenantId, companyId).ToListAsync(ct);
        return accounts.OrderBy(x => x.Code.Value, AccountCodeComparer.Instance).ToList();
    }

    public Task<Account?> FindByCodeAsync(
        Guid tenantId,
        Guid companyId,
        string code,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId, companyId)
            .FirstOrDefaultAsync(x => x.Code == AccountCode.Create(code), ct);

    // ACCOUNTING-CHART-INTEGRITY-01 — pg_advisory_xact_lock(int4, int4), mismo mecanismo y algoritmo
    // de hash que AccountingOperationalPolicyRepository, con espacio de claves propio (no colisiona
    // con la política contable ni con los locks del libro). Sin transacción ambiente el lock se
    // liberaría al instante, así que se exige una activa.
    public async Task AcquireChartLockAsync(Guid companyId, CancellationToken ct = default)
    {
        if (!_context.Database.IsRelational())
            return;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "El lock del Plan de Cuentas requiere una transacción activa."
            );

        var hash1 = StableHash(System.Text.Encoding.UTF8.GetBytes(ChartLockNamespace));
        var hash2 = StableHash(companyId.ToByteArray());
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({hash1}, {hash2})",
            ct
        );
    }

    private const string ChartLockNamespace = "Accounting.ChartOfAccounts";

    private static int StableHash(byte[] bytes)
    {
        int h = 17;
        foreach (var b in bytes)
            h = unchecked(h * 31 + b);
        return h;
    }

    public Task AddAsync(Account account, CancellationToken ct = default) =>
        _context.Accounts.AddAsync(account, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
