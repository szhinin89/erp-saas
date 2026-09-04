using ERP.Domain.Common;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Events;

namespace ERP.Domain.Modules.Retentions.Entities;

/// <summary>
/// Agregado raíz del módulo transversal <c>Retentions</c> (ver
/// <c>docs/decisions/RETENTIONS-MODULE-DESIGN-01.md</c>). Se relaciona con su documento origen de
/// forma genérica (<see cref="SourceDocumentType"/> + <see cref="SourceDocumentId"/>), replicando
/// el patrón ya probado de <c>AccountsPayable.OriginType</c>/<c>OriginId</c> — nunca una FK fuerte
/// por tipo de documento como hace <c>IssuedWithholding.PurchaseInvoiceId</c>.
///
/// Fase <c>RETENTIONS-FOUNDATION-01A</c>: solo Domain puro. Sin persistencia EF, sin integración
/// con Expenses Confirm, sin API/UI. La guarda de elegibilidad (<c>RETENTIONS-ELIGIBILITY-01</c>)
/// se invoca desde Application antes de construir este agregado — este agregado no la conoce ni la
/// duplica.
/// </summary>
public sealed class RetentionDocument : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int RetentionNumberMaxLen = 30;
    public const int CancelReasonMaxLen = 500;

    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public RetentionSourceDocumentType SourceDocumentType { get; private set; }
    public Guid SourceDocumentId { get; private set; }
    public Guid SubjectBusinessPartnerId { get; private set; }
    public Guid EmissionPointId { get; private set; }
    public string? RetentionNumber { get; private set; }
    public DateOnly? IssueDate { get; private set; }
    public RetentionStatus Status { get; private set; } = RetentionStatus.Draft;

    public decimal TotalRetainedVat { get; private set; }
    public decimal TotalRetainedIncome { get; private set; }
    public decimal TotalRetained { get; private set; }

    public string? CancelReason { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }

    private readonly List<RetentionDocumentLine> _lines = new();
    public IReadOnlyCollection<RetentionDocumentLine> Lines => _lines.AsReadOnly();

    private RetentionDocument() { }

    public static RetentionDocument Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        RetentionSourceDocumentType sourceDocumentType,
        Guid sourceDocumentId,
        Guid subjectBusinessPartnerId,
        Guid emissionPointId,
        Guid createdBy
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (!Enum.IsDefined(sourceDocumentType))
            throw new ArgumentException(
                "El tipo de documento origen no es válido.",
                nameof(sourceDocumentType)
            );
        if (sourceDocumentId == Guid.Empty)
            throw new ArgumentException("El documento origen es obligatorio.", nameof(sourceDocumentId));
        if (subjectBusinessPartnerId == Guid.Empty)
            throw new ArgumentException(
                "El sujeto retenido (proveedor) es obligatorio.",
                nameof(subjectBusinessPartnerId)
            );
        if (emissionPointId == Guid.Empty)
            throw new ArgumentException("El punto de emisión es obligatorio.", nameof(emissionPointId));

        var document = new RetentionDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            SubjectBusinessPartnerId = subjectBusinessPartnerId,
            EmissionPointId = emissionPointId,
            Status = RetentionStatus.Draft,
            TotalRetainedVat = 0m,
            TotalRetainedIncome = 0m,
            TotalRetained = 0m,
        };
        document.SetCreated(createdBy);
        return document;
    }

    /// <summary>
    /// Agrega una línea de retención al borrador y recalcula totales inmediatamente desde las
    /// líneas reales (nunca acumulado incrementalmente, para evitar drift). Solo permitido en
    /// <see cref="RetentionStatus.Draft"/> — una vez emitida o anulada, el documento es inmutable.
    /// </summary>
    public void AddLine(RetentionDocumentLine line)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(line);

        _lines.Add(line);
        RecalculateTotals();
    }

    /// <summary>
    /// EMITE la retención: asigna número, congela fecha de emisión y pasa a
    /// <see cref="RetentionStatus.Issued"/>. Requiere al menos una línea y un total retenido mayor
    /// a cero — igual que <c>IssuedWithholding.Issue()</c>, nunca se emite un documento vacío.
    /// </summary>
    public void Issue(string retentionNumber, DateOnly issueDate, Guid issuedBy)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new InvalidOperationException("No se puede emitir una retención sin líneas.");
        if (string.IsNullOrWhiteSpace(retentionNumber))
            throw new ArgumentException(
                "El número de retención es obligatorio.",
                nameof(retentionNumber)
            );
        if (issueDate == default)
            throw new ArgumentException("La fecha de emisión es obligatoria.", nameof(issueDate));
        if (TotalRetained <= 0)
            throw new InvalidOperationException(
                "El total retenido debe ser mayor a cero para emitir la retención."
            );

        RetentionNumber = retentionNumber.Trim();
        IssueDate = issueDate;
        Status = RetentionStatus.Issued;
        SetUpdated(issuedBy);

        RaiseDomainEvent(
            new RetentionDocumentIssuedEvent(
                TenantId,
                Id,
                CompanyId,
                SourceDocumentType,
                SourceDocumentId,
                SubjectBusinessPartnerId,
                RetentionNumber,
                TotalRetainedVat,
                TotalRetainedIncome,
                TotalRetained,
                IssueDate.Value
            )
        );
    }

    /// <summary>
    /// Anula una retención ya emitida. Solo <see cref="RetentionStatus.Issued"/> puede anularse —
    /// anular un <see cref="RetentionStatus.Draft"/> falla (nada que revertir), y
    /// <see cref="RetentionStatus.Cancelled"/> es terminal (no hay transición posterior). No
    /// reversa nada por su cuenta (CxP, contabilidad) — esa orquestación es responsabilidad de
    /// Application, igual que <c>ExpenseDocument.Cancel()</c>.
    /// </summary>
    public void Cancel(string reason, Guid cancelledBy)
    {
        if (Status != RetentionStatus.Issued)
            throw new InvalidOperationException("Solo se pueden anular retenciones emitidas.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de anulación es obligatorio.", nameof(reason));
        if (cancelledBy == Guid.Empty)
            throw new ArgumentException("El usuario que anula es obligatorio.", nameof(cancelledBy));

        Status = RetentionStatus.Cancelled;
        CancelReason = reason.Trim();
        CancelledAt = DateTime.UtcNow;
        CancelledBy = cancelledBy;
        SetUpdated(cancelledBy);

        RaiseDomainEvent(
            new RetentionDocumentCancelledEvent(
                TenantId,
                Id,
                CompanyId,
                SourceDocumentType,
                SourceDocumentId,
                SubjectBusinessPartnerId,
                RetentionNumber,
                TotalRetained,
                CancelReason
            )
        );
    }

    private void RecalculateTotals()
    {
        TotalRetainedVat = _lines.Where(l => l.TaxType == RetentionTaxType.Vat).Sum(l => l.RetainedAmount);
        TotalRetainedIncome = _lines.Where(l => l.TaxType == RetentionTaxType.Income).Sum(l => l.RetainedAmount);
        TotalRetained = TotalRetainedVat + TotalRetainedIncome;
    }

    private void EnsureDraft()
    {
        if (Status != RetentionStatus.Draft)
            throw new InvalidOperationException(
                "Solo se pueden modificar retenciones en estado borrador."
            );
    }
}
