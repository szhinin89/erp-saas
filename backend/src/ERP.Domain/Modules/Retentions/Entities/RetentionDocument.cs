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

    // ── Periodo fiscal (RETENTIONS-TAX-COMPONENT-MODEL-02B) ──────────────────
    // Se deriva SIEMPRE de IssueDate en el momento de Issue() — nunca es un dato
    // independiente que el usuario ingresa (ver docs/decisions/RETENTIONS-MODULE-DESIGN-01.md,
    // el período fiscal de una retención es el mes/año de su fecha de emisión). Se persiste como
    // dos componentes (Month/Year) en vez de un string libre "mm/aaaa" para evitar bugs de
    // parsing/formato — FiscalPeriod expone el string compatible con el XML SRI solo cuando se
    // necesita. Permanece null mientras el documento sigue en Draft (aún no tiene IssueDate).
    public int? FiscalPeriodMonth { get; private set; }
    public int? FiscalPeriodYear { get; private set; }

    /// <summary>Formato SRI <c>mm/aaaa</c>, calculado — nunca almacenado como string suelto.</summary>
    public string? FiscalPeriod =>
        FiscalPeriodMonth is int month && FiscalPeriodYear is int year
            ? $"{month:D2}/{year:D4}"
            : null;

    // ── Snapshot del documento sustento (RETENTIONS-TAX-COMPONENT-MODEL-02B) ─
    // Datos del comprobante del proveedor que originó la retención, congelados en el momento de
    // Create() — ADITIVO a SourceDocumentType/SourceDocumentId (vínculo técnico existente, no se
    // quita). Se completan aquí porque un futuro mapper de XML/RIDE SRI necesita estos datos
    // directamente, sin joins frágiles contra el documento origen (que puede cambiar después, ver
    // ExpenseDocument.UpdateDraft — aunque en la práctica el origen ya está Confirmed/inmutable
    // cuando se emite la retención). Quien construye el agregado (RetentionIssuer) es responsable
    // de resolver estos valores desde el documento origen ya cargado — este agregado solo los
    // recibe y los guarda, nunca los resuelve por su cuenta (sin dependencia a repositorios).
    public string? SourceDocumentSriTypeCode { get; private set; }
    public string? SourceDocumentNumber { get; private set; }
    public DateOnly? SourceDocumentIssueDate { get; private set; }
    public string? SourceDocumentAuthorizationNumber { get; private set; }

    /// <summary>
    /// Código SRI de sustento tributario (codSustento, 01-19, catálogo <c>SriTaxSupport</c>).
    /// RETENTIONS-SOURCE-DOCUMENT-TAX-SUPPORT-02G: se copia de <c>ExpenseDocument.TaxSupportCode</c>
    /// (mismo campo que <c>PurchaseInvoice.TaxSupportCode</c> ya tenía para Compras), resuelto por
    /// <c>RetentionIssuer</c> al emitir. Sigue siendo nullable — permanece <c>null</c> cuando el
    /// gasto se creó antes de esta fase o el proveedor no tiene <c>SupplierRoleConfig.DefaultTaxSupportCode</c>
    /// configurado (gap de datos conocido y aceptado, nunca bloquea la emisión). El valor queda
    /// congelado en este snapshot: nunca se recalcula desde el documento origen después de emitir.
    /// </summary>
    public string? SourceDocumentTaxSupportCode { get; private set; }
    public decimal? SourceDocumentSubtotal { get; private set; }
    public decimal? SourceDocumentTotal { get; private set; }

    public decimal TotalRetainedVat { get; private set; }
    public decimal TotalRetainedIncome { get; private set; }
    public decimal TotalRetained { get; private set; }

    public string? CancelReason { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }

    private readonly List<RetentionDocumentLine> _lines = new();
    public IReadOnlyCollection<RetentionDocumentLine> Lines => _lines.AsReadOnly();

    private RetentionDocument() { }

    /// <summary>
    /// Snapshot inmutable del documento sustento (comprobante del proveedor) que originó la
    /// retención, resuelto por quien construye el agregado (<c>RetentionIssuer</c>) a partir del
    /// documento origen YA CARGADO — el agregado nunca lo resuelve por su cuenta (sin dependencia a
    /// repositorios). Todos los campos son opcionales porque <c>SourceDocumentType</c> contempla
    /// orígenes (<c>Manual</c>) que podrían no tener un comprobante sustento físico, y porque
    /// <see cref="TaxSupportCode"/> (codSustento SRI) puede seguir sin resolverse para gastos sin
    /// dato propio ni default de proveedor configurado (ver comentario de
    /// <see cref="SourceDocumentTaxSupportCode"/>).
    /// </summary>
    public sealed record SourceDocumentSnapshot(
        string? SriTypeCode,
        string? DocumentNumber,
        DateOnly? IssueDate,
        string? AuthorizationNumber,
        string? TaxSupportCode,
        decimal? Subtotal,
        decimal? Total
    );

    public static RetentionDocument Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        RetentionSourceDocumentType sourceDocumentType,
        Guid sourceDocumentId,
        Guid subjectBusinessPartnerId,
        Guid emissionPointId,
        Guid createdBy,
        SourceDocumentSnapshot? sourceDocumentSnapshot = null
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
        if (sourceDocumentSnapshot?.Subtotal is < 0)
            throw new ArgumentException(
                "El subtotal del documento sustento no puede ser negativo.",
                nameof(sourceDocumentSnapshot)
            );
        if (sourceDocumentSnapshot?.Total is < 0)
            throw new ArgumentException(
                "El total del documento sustento no puede ser negativo.",
                nameof(sourceDocumentSnapshot)
            );

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
            SourceDocumentSriTypeCode = Normalize(sourceDocumentSnapshot?.SriTypeCode),
            SourceDocumentNumber = Normalize(sourceDocumentSnapshot?.DocumentNumber),
            SourceDocumentIssueDate = sourceDocumentSnapshot?.IssueDate,
            SourceDocumentAuthorizationNumber = Normalize(sourceDocumentSnapshot?.AuthorizationNumber),
            SourceDocumentTaxSupportCode = Normalize(sourceDocumentSnapshot?.TaxSupportCode),
            SourceDocumentSubtotal = sourceDocumentSnapshot?.Subtotal,
            SourceDocumentTotal = sourceDocumentSnapshot?.Total,
        };
        document.SetCreated(createdBy);
        return document;
    }

    private static string? Normalize(string? value) => value?.Trim() is { Length: > 0 } text ? text : null;

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
        // Periodo fiscal SIEMPRE derivado de la fecha de emisión real (nunca un input
        // independiente) — evita que quede desincronizado del dato legal que representa.
        FiscalPeriodMonth = issueDate.Month;
        FiscalPeriodYear = issueDate.Year;
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
