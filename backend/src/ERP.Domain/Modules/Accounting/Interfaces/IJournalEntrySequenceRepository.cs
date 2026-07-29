namespace ERP.Domain.Modules.Accounting.Interfaces;

public interface IJournalEntrySequenceRepository
{
    /// <summary>
    /// Reserva atómicamente el siguiente número correlativo para (CompanyId, FiscalYear).
    /// Advisory lock sobre la transacción ambiente (ADR-026 §7/§8) — nunca abre una transacción
    /// propia: a diferencia de <c>IDocumentSequenceRepository.CaptureNextAsync</c> (ADR-019), la
    /// fila de secuencia queda en staging junto con el <c>JournalEntry</c> que la consume, a
    /// cargo del mismo <c>SaveChangesAsync</c> externo que lo persiste. Crea la fila on-demand si
    /// no existe.
    /// </summary>
    Task<int> ReserveNextNumberAsync(
        Guid tenantId,
        Guid companyId,
        int fiscalYear,
        CancellationToken ct = default
    );
}
