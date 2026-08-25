using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;

namespace ERP.Domain.Modules.Accounting.Interfaces;

/// <summary>ACCOUNTING-LEDGER-VISIBILITY-01: filtros opcionales del listado de asientos.</summary>
public sealed record JournalEntryListFilter(
    JournalEntryStatus? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    string? SourceModule
);

/// <summary>
/// ACCOUNTING-REPORTS-09: una línea Posted de un asiento, proyectada junto con los datos de su
/// asiento padre (EntryDate/EntryNumber/Description/SourceModule/SourceEventType/SourceEventId)
/// — usada por Libro Mayor para el detalle de movimientos de una cuenta específica. Es una
/// proyección de reporte, no una entidad de dominio (no reemplaza <see cref="JournalEntryLine"/>):
/// evita cargar el <see cref="JournalEntry"/> completo (con sus demás líneas, irrelevantes para
/// una cuenta puntual) solo para leer estos campos.
/// </summary>
public sealed record JournalEntryLineReportRow(
    Guid JournalEntryId,
    int? EntryNumber,
    DateOnly EntryDate,
    string Description,
    string SourceModule,
    string SourceEventType,
    Guid SourceEventId,
    Guid LineId,
    decimal Debit,
    decimal Credit
);

public interface IJournalEntryRepository
{
    Task<JournalEntry?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    );

    /// <summary>
    /// ACCOUNTING-LEDGER-VISIBILITY-01: listado paginado, solo lectura, con `Lines` incluidas
    /// (necesarias para calcular TotalDebit/TotalCredit por asiento). Orden fijo:
    /// EntryDate desc, luego CreatedAt desc — sin campo de orden configurable en esta fase.
    /// </summary>
    Task<(IReadOnlyList<JournalEntry> Items, int TotalCount)> GetPageAsync(
        Guid tenantId,
        Guid companyId,
        JournalEntryListFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// ACCOUNTING-LEDGER-VISIBILITY-01: asientos originados por un documento externo específico.
    /// No filtra por SourceEventType (a diferencia de <see cref="FindByKeyAsync"/>) porque un
    /// mismo documento de origen puede tener más de un asiento asociado (p. ej. el original y su
    /// reverso comparten el mismo aggregate root, pero un futuro caso con varios FactType sobre
    /// el mismo SourceEventId no debe quedar oculto por una clave demasiado estricta).
    /// </summary>
    Task<IReadOnlyList<JournalEntry>> GetBySourceAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEventId,
        CancellationToken ct = default
    );

    /// <summary>Búsqueda por la clave de idempotencia del Posting Engine — ver uq_journal_entries_company_source_event_fact.</summary>
    Task<JournalEntry?> FindByKeyAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string factType,
        Guid sourceEventId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Protege la clave natural de idempotencia del Posting Engine (CompanyId, SourceModule,
    /// SourceEventId, FactType) mediante un advisory lock transaccional de PostgreSQL — debe
    /// ejecutarse siempre antes de <see cref="FindByKeyAsync"/>. Utiliza la transacción ambiente
    /// ya existente (nunca abre una transacción propia); el lock se libera automáticamente al
    /// hacer COMMIT o ROLLBACK de esa transacción.
    /// </summary>
    Task AcquireIdempotencyLockAsync(
        Guid companyId,
        string sourceModule,
        Guid sourceEventId,
        string factType,
        CancellationToken ct = default
    );

    Task AddAsync(JournalEntry entry, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Fase 5.5 (ADR-026 §6.1/§9): resuelve las precondiciones cross-aggregate para
    /// <c>AccountingPeriod.Close</c> mediante consultas EXISTS — nunca materializa los
    /// <c>JournalEntry</c> del período completo.
    /// </summary>
    Task<JournalEntryClosureReadiness> GetClosureReadinessAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountingPeriodId,
        CancellationToken ct = default
    );

    /// <summary>
    /// ACCOUNTING-REPORTS-09: listado paginado de asientos Posted en un rango de fechas, con
    /// <c>Lines</c> incluidas — fuente única de Libro Diario. Nunca incluye Draft (aún no tuvo
    /// efecto contable) ni Reversed (el asiento original queda inválido una vez reversado; su
    /// reverso, que SÍ es Posted, aparece como un asiento normal más — ver
    /// <see cref="ERP.Domain.Modules.Accounting.Entities.JournalEntry.Reverse"/>). <paramref
    /// name="search"/> es opcional: compara contra <c>EntryNumber</c> (si es numérico) y
    /// <c>Description</c> (contains, case-insensitive) — no existe hoy una forma barata de buscar
    /// por "documento origen" sin resolverlo primero (ver <c>IJournalEntrySourceResolver</c>), así
    /// que ese campo queda fuera de este filtro (brecha reportada en el entregable).
    /// </summary>
    Task<(IReadOnlyList<JournalEntry> Items, int TotalCount)> GetPostedEntriesPageAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly fromDate,
        DateOnly toDate,
        string? sourceModule,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// ACCOUNTING-REPORTS-09: Σ Debit / Σ Credit de líneas Posted por cuenta, agregado en SQL
    /// (GroupBy AccountId + Sum) — nunca materializa <see cref="JournalEntry"/> completos. Usado
    /// dos veces por reporte (una para "saldo inicial" con <paramref name="toDate"/> = día antes
    /// del rango y <paramref name="fromDate"/> null, otra para "movimiento del período" con el
    /// rango real) — ver Libro Mayor / Balance de Comprobación. <paramref name="accountIds"/> nulo
    /// = todas las cuentas de la Company (Balance de Comprobación); una lista acotada = solo esas
    /// cuentas (Libro Mayor). Cuentas sin ningún movimiento Posted en el rango simplemente no
    /// aparecen en el diccionario resultado — el llamador decide si mostrarlas en cero.
    /// </summary>
    Task<
        IReadOnlyDictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
    > GetAccountLineTotalsAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly? fromDate,
        DateOnly? toDate,
        IReadOnlyCollection<Guid>? accountIds,
        CancellationToken ct = default
    );

    /// <summary>
    /// ACCOUNTING-REPORTS-09: detalle de movimientos Posted de UNA cuenta en un rango de fechas,
    /// ordenado por fecha/asiento — fuente del detalle de Libro Mayor. Proyecta directamente a
    /// <see cref="JournalEntryLineReportRow"/> (join contra el asiento padre) en vez de cargar
    /// <see cref="JournalEntry"/> completos, que traerían también las líneas de OTRAS cuentas del
    /// mismo asiento (irrelevantes aquí).
    /// </summary>
    Task<IReadOnlyList<JournalEntryLineReportRow>> GetPostedLinesByAccountAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default
    );
}
