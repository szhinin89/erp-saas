namespace ERP.Domain.Common;

/// <summary>
/// Clase base para documentos transaccionales.
/// Ejemplos: JournalEntry, Invoice, Order, Payment, etc.
///
/// Hereda: BaseEntity (Id, tenantId)
///       → AggregateRoot (DomainEvents)
///       → AuditableEntity (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
///
/// REGLA: Nunca se eliminan físicamente.
/// Ciclo de vida: Draft → Posted → Voided.
/// </summary>
public abstract class DocumentEntity : AuditableEntity
{
    public DocumentStatus Status { get; protected set; } = DocumentStatus.Draft;

    public DateTime? PostedAt { get; protected set; }
    public Guid? PostedBy { get; protected set; }

    public DateTime? VoidedAt { get; protected set; }
    public Guid? VoidedBy { get; protected set; }
    public string? VoidReason { get; protected set; }

    /// <summary>
    /// Indica si el documento puede ser modificado (solo en Draft).
    /// </summary>
    public bool IsEditable => Status == DocumentStatus.Draft;

    /// <summary>
    /// Indica si el documento está vigente (no anulado).
    /// </summary>
    public bool IsActive => Status != DocumentStatus.Voided;

    /// <summary>
    /// Confirma el documento. Pasa de Draft a Posted.
    /// </summary>
    public void Post(Guid postedBy)
    {
        if (Status != DocumentStatus.Draft)
            throw new InvalidOperationException(
                $"Solo se puede confirmar un documento en borrador. Estado actual: {Status}"
            );

        Status = DocumentStatus.Posted;
        PostedAt = DateTime.UtcNow;
        PostedBy = postedBy;
        SetUpdated(postedBy);
    }

    /// <summary>
    /// Anula el documento. No lo elimina.
    /// Se puede anular desde Draft o Posted.
    /// </summary>
    public void Void(Guid voidedBy, string reason)
    {
        if (Status == DocumentStatus.Voided)
            throw new InvalidOperationException("El documento ya está anulado.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Se requiere un motivo de anulación.", nameof(reason));

        Status = DocumentStatus.Voided;
        VoidedAt = DateTime.UtcNow;
        VoidedBy = voidedBy;
        VoidReason = reason;
        SetUpdated(voidedBy);
    }
}
