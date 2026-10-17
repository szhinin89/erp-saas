using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Accounting.Repositories;

public sealed class JournalEntryRepository : IJournalEntryRepository
{
    private readonly ErpDbContext _context;

    public JournalEntryRepository(ErpDbContext context)
    {
        _context = context;
    }

    public Task<JournalEntry?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    )
        // Include(Lines) — Fase 5.4: ReverseJournalEntryHandler necesita las líneas del asiento
        // original para construir el reverso (JournalEntry.Reverse invierte cada línea); sin este
        // Include, la colección llegaría vacía (sin lazy loading, ver JournalFactory/PostingRule).
        =>
        _context
            .JournalEntries.Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id)
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<JournalEntry> Items, int TotalCount)> GetPageAsync(
        Guid tenantId,
        Guid companyId,
        JournalEntryListFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var scoped = _context.JournalEntries.Where(x =>
            x.TenantId == tenantId && x.CompanyId == companyId
        );

        if (filter.Status is not null)
            scoped = scoped.Where(x => x.Status == filter.Status);
        if (filter.FromDate is not null)
            scoped = scoped.Where(x => x.EntryDate >= filter.FromDate);
        if (filter.ToDate is not null)
            scoped = scoped.Where(x => x.EntryDate <= filter.ToDate);
        if (!string.IsNullOrWhiteSpace(filter.SourceModule))
            scoped = scoped.Where(x => x.SourceModule == filter.SourceModule);

        if (filter.Manual is bool isManual)
            scoped = scoped.Where(x => ((x.SourceModule == JournalEntry.ManualModule
                    && (x.SourceEventType == JournalEntry.ManualFact || x.SourceEventType == JournalEntry.FiscalYearClosingFact))
                || (x.OriginalJournalEntryId != null && _context.JournalEntries.Any(o => o.Id == x.OriginalJournalEntryId
                    && o.TenantId == tenantId && o.CompanyId == companyId && o.SourceModule == JournalEntry.ManualModule
                    && (o.SourceEventType == JournalEntry.ManualFact || o.SourceEventType == JournalEntry.FiscalYearClosingFact)))) == isManual);

        var totalCount = await scoped.CountAsync(ct);

        var items = await scoped
            .Include(x => x.Lines)
            .OrderByDescending(x => x.EntryDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<JournalEntry>> GetBySourceAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEventId,
        CancellationToken ct = default
    ) =>
        await _context
            .JournalEntries.Include(x => x.Lines)
            .Where(x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.SourceModule == sourceModule
                && x.SourceEventId == sourceEventId
            )
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public Task<JournalEntry?> FindByKeyAsync(Guid tenantId, Guid companyId, string sourceModule,
        string factType, Guid sourceEventId, CancellationToken ct = default)
    {
        var local = _context.JournalEntries.Local.FirstOrDefault(x => x.TenantId == tenantId
            && x.CompanyId == companyId && x.SourceModule == sourceModule
            && x.SourceEventType == factType && x.SourceEventId == sourceEventId);
        return local is not null ? Task.FromResult<JournalEntry?>(local)
            : _context.JournalEntries.Include(x => x.Lines).FirstOrDefaultAsync(x =>
                x.TenantId == tenantId && x.CompanyId == companyId && x.SourceModule == sourceModule
                && x.SourceEventType == factType && x.SourceEventId == sourceEventId, ct);
    }

    public async Task AcquireIdempotencyLockAsync(
        Guid companyId,
        string sourceModule,
        Guid sourceEventId,
        string factType,
        CancellationToken ct = default
    )
    {
        // Advisory lock de transacción — serializa concurrentes para la misma clave de
        // idempotencia (CompanyId, SourceModule, SourceEventId, FactType) sin bloquear otras
        // claves. pg_advisory_xact_lock(int4, int4) se libera automáticamente al hacer
        // COMMIT/ROLLBACK de la transacción ambiente — nunca abre ni comitea una transacción
        // propia (la persistencia pertenece exclusivamente a ErpDbContext.SaveChangesAsync).
        var hash1 = StableHash(
            companyId
                .ToByteArray()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sourceModule))
                .ToArray()
        );
        var hash2 = StableHash(
            sourceEventId
                .ToByteArray()
                .Concat(System.Text.Encoding.UTF8.GetBytes(factType))
                .ToArray()
        );
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({hash1}, {hash2})",
            ct
        );
    }

    // Hash estable (no depende de HashCode.GetHashCode, no-determinístico en .NET 5+) — mismo
    // algoritmo que DocumentSequenceRepository, duplicado deliberadamente (sin helper
    // compartido: un solo consumidor adicional no justifica extraerlo todavía). Un hash de 32
    // bits puede colisionar en teoría; una colisión solo produce una espera adicional entre dos
    // claves de negocio distintas (falso positivo de contención) — nunca un duplicado, porque la
    // protección final contra duplicados sigue siendo el índice único
    // uq_journal_entries_company_source_event_fact.
    private static int StableHash(byte[] bytes)
    {
        int h = 17;
        foreach (var b in bytes)
            h = unchecked(h * 31 + b);
        return h;
    }

    public Task AddAsync(JournalEntry entry, CancellationToken ct = default) =>
        _context.JournalEntries.AddAsync(entry, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public async Task<JournalEntryClosureCounts> GetClosureCountsAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountingPeriodId,
        CancellationToken ct = default
    )
    {
        var period = await _context.AccountingPeriods.AsNoTracking().FirstAsync(x =>
            x.TenantId == tenantId && x.CompanyId == companyId && x.Id == accountingPeriodId, ct);
        var scoped = _context.JournalEntries.Where(x =>
            x.TenantId == tenantId && x.CompanyId == companyId
            && (x.AccountingPeriodId == accountingPeriodId
                || (x.SourceModule == JournalEntry.ManualModule && x.SourceEventType == JournalEntry.ManualFact
                    && x.EntryDate >= period.StartDate && x.EntryDate <= period.EndDate))
            && x.Status != JournalEntryStatus.Discarded
        );

        // Cada CountAsync se traduce a "SELECT COUNT(*)" — nunca materializa JournalEntry
        // completos (ADR-026 §6.1/§9: cierre es cross-aggregate, pero no debe cargar el período
        // entero para decidir).
        var draftOrNonFinal = await scoped.CountAsync(
            x => x.Status != JournalEntryStatus.Posted && x.Status != JournalEntryStatus.Reversed,
            ct
        );

        var withoutEntryNumber = await scoped.CountAsync(
            x => x.Status != JournalEntryStatus.Draft && x.EntryNumber == null,
            ct
        );

        // El primer término solo aplica a datos previos a ACCOUNTING-CONTINUITY-03 (el original ya
        // no se marca Reversed); el segundo, a un reverso que no quedó publicado.
        var incompleteReversals = await scoped.CountAsync(
            x =>
                (x.Status == JournalEntryStatus.Reversed && x.ReverseJournalEntryId == null)
                || (x.OriginalJournalEntryId != null && x.Status != JournalEntryStatus.Posted),
            ct
        );

        return new JournalEntryClosureCounts(draftOrNonFinal, withoutEntryNumber, incompleteReversals);
    }

    public async Task<(
        IReadOnlyList<JournalEntry> Items,
        int TotalCount
    )> GetPostedEntriesPageAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly fromDate,
        DateOnly toDate,
        string? sourceModule,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default
    )
    {
        // ACCOUNTING-REPORTS-REVERSED-ENTRIES-NET-ZERO-01 — Posted Y Reversed, nunca solo Posted:
        // un asiento Reversed nunca perdió su efecto contable histórico (JournalEntry.Reverse deja
        // sus líneas/importes intactos, solo cambia Status), su reverso es un JournalEntry NUEVO,
        // separado, en Posted — excluirlo de reportes dejaba visible solo la mitad del par contable
        // (el reverso) y el neto del rango quedaba descuadrado en vez de en cero. Draft SÍ se
        // excluye (nunca tuvo efecto contable real, nunca debe aparecer en un reporte histórico).
        var scoped = _context.JournalEntries.Where(x =>
            x.TenantId == tenantId
            && x.CompanyId == companyId
            && (x.Status == JournalEntryStatus.Posted || x.Status == JournalEntryStatus.Reversed)
            && x.EntryDate >= fromDate
            && x.EntryDate <= toDate
        );

        if (!string.IsNullOrWhiteSpace(sourceModule))
            scoped = scoped.Where(x => x.SourceModule == sourceModule);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            if (int.TryParse(trimmed, out var entryNumber))
                scoped = scoped.Where(x =>
                    x.EntryNumber == entryNumber
                    || EF.Functions.ILike(x.Description, $"%{trimmed}%")
                );
            else
                scoped = scoped.Where(x => EF.Functions.ILike(x.Description, $"%{trimmed}%"));
        }

        var totalCount = await scoped.CountAsync(ct);

        var items = await scoped
            .Include(x => x.Lines)
            .OrderBy(x => x.EntryDate)
            .ThenBy(x => x.EntryNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<
        IReadOnlyDictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
    > GetAccountLineTotalsAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly? fromDate,
        DateOnly? toDate,
        IReadOnlyCollection<Guid>? accountIds,
        CancellationToken ct = default
    ) => LineTotalsAsync(tenantId, companyId, fromDate, toDate, accountIds, false, ct);

    public Task<
        IReadOnlyDictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
    > GetAccountLineTotalsExcludingYearEndClosingAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly? fromDate,
        DateOnly? toDate,
        IReadOnlyCollection<Guid>? accountIds,
        CancellationToken ct = default
    ) => LineTotalsAsync(tenantId, companyId, fromDate, toDate, accountIds, true, ct);

    public async Task<IReadOnlyList<(JournalEntry Entry, bool Reversed)>> GetFiscalYearClosingsAsync(
        Guid tenantId,
        Guid companyId,
        int? fiscalYear,
        CancellationToken ct = default
    )
    {
        var query = _context.JournalEntries.Include(x => x.Lines).Where(x =>
            x.TenantId == tenantId && x.CompanyId == companyId
            && x.SourceModule == JournalEntry.ManualModule
            && x.SourceEventType == JournalEntry.FiscalYearClosingFact);
        if (fiscalYear is not null)
            query = query.Where(x => x.FiscalYear == fiscalYear);
        var entries = await query.OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var ids = entries.Select(e => e.Id).ToList();
        var reversed = await _context.JournalEntries
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId
                && x.OriginalJournalEntryId != null && ids.Contains(x.OriginalJournalEntryId.Value))
            .Select(x => x.OriginalJournalEntryId!.Value)
            .ToListAsync(ct);
        return entries.Select(e => (e, reversed.Contains(e.Id))).ToList();
    }

    private async Task<
        IReadOnlyDictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
    > LineTotalsAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly? fromDate,
        DateOnly? toDate,
        IReadOnlyCollection<Guid>? accountIds,
        bool excludeYearEndClosing,
        CancellationToken ct
    )
    {
        // ACCOUNTING-REPORTS-REVERSED-ENTRIES-NET-ZERO-01 — ver GetPostedEntriesPageAsync: un
        // Reversed conserva sus importes históricos, su reverso vive en un JournalEntry Posted
        // aparte — ambos deben sumar aquí para que el rango quede neto en cero, nunca solo uno.
        var entries = _context.JournalEntries.Where(x =>
            x.TenantId == tenantId
            && x.CompanyId == companyId
            && (x.Status == JournalEntryStatus.Posted || x.Status == JournalEntryStatus.Reversed)
        );
        // ACCOUNTING-12D: el cierre anual y su eventual reverso se excluyen juntos, sin importar sus fechas.
        if (excludeYearEndClosing)
            entries = entries.Where(x =>
                !(x.SourceModule == JournalEntry.ManualModule && x.SourceEventType == JournalEntry.FiscalYearClosingFact)
                && !(x.OriginalJournalEntryId != null && _context.JournalEntries.Any(o =>
                    o.Id == x.OriginalJournalEntryId && o.TenantId == tenantId && o.CompanyId == companyId
                    && o.SourceModule == JournalEntry.ManualModule
                    && o.SourceEventType == JournalEntry.FiscalYearClosingFact)));
        var scoped = entries.SelectMany(x => x.Lines, (entry, line) => new { entry.EntryDate, line });

        if (fromDate is not null)
            scoped = scoped.Where(x => x.EntryDate >= fromDate);
        if (toDate is not null)
            scoped = scoped.Where(x => x.EntryDate <= toDate);
        if (accountIds is not null)
            scoped = scoped.Where(x => accountIds.Contains(x.line.AccountId));

        var totals = await scoped
            .GroupBy(x => x.line.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                TotalDebit = g.Sum(x => x.line.Debit),
                TotalCredit = g.Sum(x => x.line.Credit),
            })
            .ToListAsync(ct);

        return totals.ToDictionary(t => t.AccountId, t => (t.TotalDebit, t.TotalCredit));
    }

    public async Task<IReadOnlyList<JournalEntryLineReportRow>> GetPostedLinesByAccountAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default
    ) =>
        await _context
            // ACCOUNTING-REPORTS-REVERSED-ENTRIES-NET-ZERO-01 — ver GetPostedEntriesPageAsync.
            .JournalEntries.Where(x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && (
                    x.Status == JournalEntryStatus.Posted || x.Status == JournalEntryStatus.Reversed
                )
                && x.EntryDate >= fromDate
                && x.EntryDate <= toDate
            )
            // Proyecta a un tipo anónimo (entry, line) primero — EF Core no puede traducir un
            // OrderBy aplicado DESPUÉS de proyectar al constructor de un record
            // (JournalEntryLineReportRow es un `record`, no una entidad mapeada; ver el
            // InvalidOperationException "could not be translated" que este fix corrige). Ordenar
            // sobre las columnas reales de la entidad y recién al final construir el record.
            .SelectMany(
                x => x.Lines.Where(l => l.AccountId == accountId),
                (entry, line) => new { entry, line }
            )
            .OrderBy(x => x.entry.EntryDate)
            .ThenBy(x => x.entry.EntryNumber)
            .Select(x => new JournalEntryLineReportRow(
                x.entry.Id,
                x.entry.EntryNumber,
                x.entry.EntryDate,
                x.entry.Description,
                x.entry.SourceModule,
                x.entry.SourceEventType,
                x.entry.SourceEventId,
                x.line.Id,
                x.line.Debit,
                x.line.Credit
            ))
            .ToListAsync(ct);
}
