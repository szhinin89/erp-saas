using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// Secuencia de numeración definitiva de <see cref="JournalEntry"/>, correlativa por CompanyId y
/// ejercicio fiscal (Fase 5.3). Mismo patrón arquitectónico de <c>DocumentSequence</c> (ADR-019):
/// una fila por clave natural, incremento atómico bajo advisory lock. A diferencia de
/// <c>DocumentSequence.CaptureAndIncrement()</c> (que se persiste en una transacción propia,
/// independiente del documento SRI), <see cref="NextNumber"/> se persiste en la misma transacción
/// ambiente que el <see cref="JournalEntry"/> que numera — ver
/// <c>IJournalEntrySequenceRepository.ReserveNextNumberAsync</c> y <c>JournalEntry.Post</c>
/// (ADR-026 §7/§8).
/// </summary>
public sealed class JournalEntrySequence : BaseEntity, ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid CompanyId { get; private set; }
    public int FiscalYear { get; private set; }

    /// <summary>Último número asignado. 0 significa que todavía no se ha asignado ningún número.</summary>
    public int LastNumber { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private JournalEntrySequence() { }

    public static JournalEntrySequence Create(Guid tenantId, Guid companyId, int fiscalYear)
    {
        var now = DateTime.UtcNow;
        return new JournalEntrySequence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            FiscalYear = fiscalYear,
            LastNumber = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Incrementa y devuelve el siguiente número correlativo. PRECONDICIÓN: usar únicamente desde
    /// <c>IJournalEntrySequenceRepository.ReserveNextNumberAsync</c>, que garantiza el advisory
    /// lock sobre la transacción ambiente antes de leer/crear esta fila.
    /// </summary>
    public int NextNumber()
    {
        LastNumber++;
        UpdatedAt = DateTime.UtcNow;
        return LastNumber;
    }
}
