using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Accounting.Repositories;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — filtro explícito Tenant + Company además del query filter global
/// (mismo criterio que JournalEntryRepository).
/// </summary>
public sealed class AccountingPostingRepository : IAccountingPostingRepository
{
    private readonly ErpDbContext _context;

    public AccountingPostingRepository(ErpDbContext context) => _context = context;

    private IQueryable<AccountingPosting> Scoped(Guid tenantId, Guid companyId) =>
        _context.AccountingPostings.Where(x => x.TenantId == tenantId && x.CompanyId == companyId);

    public async Task<AccountingPosting?> FindByKeyAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string factType,
        Guid sourceEventId,
        CancellationToken ct = default
    ) =>
        _context.AccountingPostings.Local.FirstOrDefault(x =>
            x.TenantId == tenantId
            && x.CompanyId == companyId
            && x.SourceModule == sourceModule
            && x.FactType == factType
            && x.SourceEventId == sourceEventId
        )
        ?? await Scoped(tenantId, companyId)
            .FirstOrDefaultAsync(
                x =>
                    x.SourceModule == sourceModule
                    && x.FactType == factType
                    && x.SourceEventId == sourceEventId,
                ct
            );

    public async Task<AccountingPosting?> LockByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    )
    {
        await LockRowsAsync(
            $"SELECT 1 FROM accounting_postings WHERE id = {id} AND tenant_id = {tenantId} AND company_id = {companyId} FOR UPDATE",
            ct
        );
        return await ReloadedAsync(
            Scoped(tenantId, companyId).Where(x => x.Id == id),
            ct
        );
    }

    public async Task<AccountingPosting?> LockByKeyAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string factType,
        Guid sourceEventId,
        CancellationToken ct = default
    )
    {
        await LockRowsAsync(
            $"SELECT 1 FROM accounting_postings WHERE company_id = {companyId} AND tenant_id = {tenantId} AND source_module = {sourceModule} AND source_event_id = {sourceEventId} AND fact_type = {factType} FOR UPDATE",
            ct
        );
        return await ReloadedAsync(
            Scoped(tenantId, companyId)
                .Where(x =>
                    x.SourceModule == sourceModule
                    && x.FactType == factType
                    && x.SourceEventId == sourceEventId
                ),
            ct
        );
    }

    public Task<AccountingPosting?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    ) => Scoped(tenantId, companyId).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<AccountingPosting>> ListAsync(
        Guid tenantId,
        Guid companyId,
        AccountingPostingStatus? status,
        string? sourceModule,
        Guid? sourceEventId,
        int take,
        CancellationToken ct = default
    )
    {
        var query = Scoped(tenantId, companyId).AsNoTracking();
        if (status is { } s)
            query = query.Where(x => x.Status == s);
        if (sourceModule is not null)
            query = query.Where(x => x.SourceModule == sourceModule);
        if (sourceEventId is { } eventId)
            query = query.Where(x => x.SourceEventId == eventId);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AccountingPostingAttempt>> ListAttemptsAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountingPostingId,
        CancellationToken ct = default
    ) =>
        await _context
            .AccountingPostingAttempts.AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.AccountingPostingId == accountingPostingId
            )
            .OrderBy(x => x.AttemptNumber)
            .ToListAsync(ct);

    public Task<bool> HasUnresolvedInRangeAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId, companyId)
            .AnyAsync(
                x =>
                    (
                        x.Status == AccountingPostingStatus.Pending
                        || x.Status == AccountingPostingStatus.Failed
                    )
                    && x.EntryDate >= from
                    && x.EntryDate <= to,
                ct
            );

    public async Task AddAsync(AccountingPosting posting, CancellationToken ct = default) =>
        await _context.AccountingPostings.AddAsync(posting, ct);

    public async Task AddAttemptAsync(AccountingPostingAttempt attempt, CancellationToken ct = default) =>
        await _context.AccountingPostingAttempts.AddAsync(attempt, ct);

    private async Task LockRowsAsync(FormattableString sql, CancellationToken ct)
    {
        // Proveedor en memoria (tests unitarios): sin transacciones reales ni FOR UPDATE.
        if (!_context.Database.IsRelational())
            return;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "El bloqueo de la obligación contable requiere una transacción activa."
            );
        await _context.Database.ExecuteSqlInterpolatedAsync(sql, ct);
    }

    /// <summary>
    /// Tras el lock, devuelve el estado comiteado: si la entidad ya estaba trackeada (identity
    /// resolution) se recarga para no decidir con valores obsoletos.
    /// </summary>
    private async Task<AccountingPosting?> ReloadedAsync(
        IQueryable<AccountingPosting> query,
        CancellationToken ct
    )
    {
        var posting = await query.FirstOrDefaultAsync(ct);
        if (posting is not null && _context.Database.IsRelational())
            await _context.Entry(posting).ReloadAsync(ct);
        return posting;
    }
}
