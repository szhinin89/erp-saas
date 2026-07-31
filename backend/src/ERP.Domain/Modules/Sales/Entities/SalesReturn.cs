using ERP.Domain.Common;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Events;

namespace ERP.Domain.Modules.Sales.Entities;

/// <summary>
/// Devolución de venta (P0-01) — agregado independiente de <see cref="SalesInvoice"/>, referenciada
/// solo por Id (mismo patrón que <see cref="SalesReceivable.InvoiceId"/>). Una factura autorizada
/// puede tener múltiples devoluciones parciales a lo largo del tiempo. El invariante de "no exceder
/// lo vendido" se valida en Application (requiere consulta cross-agregado), no aquí.
/// </summary>
public sealed class SalesReturn : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int ReturnNumberMaxLen = 30;
    public const int ReasonMaxLen = 500;

    public Guid CompanyId { get; private set; }
    public Guid SalesInvoiceId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string ReturnNumber { get; private set; } = null!;
    public string Reason { get; private set; } = null!;

    /// <summary>
    /// Secuencial SRI ("04" Nota de Crédito, formato "EST-PTO-SECUENCIAL") congelado una única
    /// vez tras autorizar la devolución — mismo patrón exacto que
    /// <see cref="SalesInvoice.SetInvoiceNumber"/>: se captura una sola vez
    /// (<c>IDocumentSequenceRepository.CaptureNextAsync</c>) y nunca se vuelve a solicitar, para
    /// no reemitir secuenciales SRI en reintentos de emisión electrónica. Independiente del
    /// número de factura original — la Nota de Crédito es su propio documento fiscal.
    /// </summary>
    public string? CreditNoteDocumentNumber { get; private set; }

    public SalesReturnStatus Status { get; private set; } = SalesReturnStatus.Draft;

    // ── Totales snapshot (congelados al autorizar) ──────────────────────
    public decimal? AuthorizedSubtotal { get; private set; }
    public decimal? AuthorizedTotalVat { get; private set; }
    public decimal? AuthorizedTotalIce { get; private set; }
    public decimal? AuthorizedTotalDiscount { get; private set; }
    public decimal? AuthorizedGrandTotal { get; private set; }

    private readonly List<SalesReturnDetail> _lines = new();
    public IReadOnlyList<SalesReturnDetail> Lines => _lines.AsReadOnly();

    private readonly List<SalesReturnRefundAllocation> _refundAllocations = new();
    public IReadOnlyList<SalesReturnRefundAllocation> RefundAllocations =>
        _refundAllocations.AsReadOnly();

    // ── Calculated (NOT persisted) ──────────────────────────────────────
    public decimal Subtotal => AuthorizedSubtotal ?? _lines.Sum(l => l.LineSubtotal);
    public decimal TotalDiscount => AuthorizedTotalDiscount ?? _lines.Sum(l => l.DiscountAmount);
    public decimal TotalIce => AuthorizedTotalIce ?? _lines.Sum(l => l.IceAmount);
    public decimal TotalVat => AuthorizedTotalVat ?? _lines.Sum(l => l.VatAmount);
    public decimal GrandTotal => AuthorizedGrandTotal ?? _lines.Sum(l => l.TaxInclusiveTotal);

    private SalesReturn() { }

    // ── Factory ─────────────────────────────────────────────────────────
    public static SalesReturn CreateDraft(
        Guid tenantId,
        Guid companyId,
        Guid salesInvoiceId,
        Guid customerId,
        string returnNumber,
        string reason,
        Guid createdBy
    )
    {
        if (salesInvoiceId == Guid.Empty)
            throw new ArgumentException("La factura origen es obligatoria.", nameof(salesInvoiceId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("El cliente es obligatorio.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(returnNumber))
            throw new ArgumentException(
                "El número de devolución es obligatorio.",
                nameof(returnNumber)
            );
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "El motivo de la devolución es obligatorio.",
                nameof(reason)
            );

        var salesReturn = new SalesReturn
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            SalesInvoiceId = salesInvoiceId,
            CustomerId = customerId,
            ReturnNumber = returnNumber.Trim(),
            Reason = reason.Trim(),
            Status = SalesReturnStatus.Draft,
        };
        salesReturn.SetCreated(createdBy);

        salesReturn.RaiseDomainEvent(
            new SalesReturnDraftCreatedEvent(
                salesReturn.Id,
                salesInvoiceId,
                customerId,
                salesReturn.ReturnNumber,
                salesReturn.Reason,
                tenantId,
                companyId,
                createdBy
            )
        );

        return salesReturn;
    }

    // ── Lines ───────────────────────────────────────────────────────────
    public void AddLine(SalesReturnDetail line, Guid updatedBy)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(line);

        _lines.Add(line);
        SetUpdated(updatedBy);
    }

    public void RemoveLine(Guid lineId, Guid updatedBy)
    {
        EnsureDraft();
        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            throw new InvalidOperationException(
                "La línea indicada no pertenece a esta devolución."
            );

        _lines.Remove(line);
        SetUpdated(updatedBy);
    }

    // ── Refund allocations (asignación explícita del operador — sin prorrateo automático) ──
    public void AddRefundAllocation(SalesReturnRefundAllocation allocation, Guid updatedBy)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(allocation);

        _refundAllocations.Add(allocation);
        SetUpdated(updatedBy);
    }

    public void RemoveRefundAllocation(Guid allocationId, Guid updatedBy)
    {
        EnsureDraft();
        var allocation = _refundAllocations.FirstOrDefault(a => a.Id == allocationId);
        if (allocation is null)
            throw new InvalidOperationException(
                "La asignación de reembolso indicada no pertenece a esta devolución."
            );

        _refundAllocations.Remove(allocation);
        SetUpdated(updatedBy);
    }

    // ── Authorize (final — immutable after this) ─────────────────────────
    public void Authorize(Guid updatedBy)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new InvalidOperationException(
                "No puedes autorizar esta devolución porque no tiene líneas agregadas. Agrega al menos una línea antes de autorizar."
            );
        if (_refundAllocations.Count == 0)
            throw new InvalidOperationException(
                "No puedes autorizar esta devolución porque no has indicado cómo se reembolsará. Agrega al menos una asignación de reembolso (efectivo o crédito) antes de autorizar."
            );

        Freeze();

        AuthorizedSubtotal = _lines.Sum(l => l.LineSubtotal);
        AuthorizedTotalDiscount = _lines.Sum(l => l.DiscountAmount);
        AuthorizedTotalVat = _lines.Sum(l => l.VatAmount);
        AuthorizedTotalIce = _lines.Sum(l => l.IceAmount);
        AuthorizedGrandTotal = _lines.Sum(l => l.TaxInclusiveTotal);

        if (AuthorizedGrandTotal <= 0)
            throw new InvalidOperationException(
                "No puedes autorizar esta devolución porque su total es $0 o negativo. Revisa las cantidades de las líneas antes de autorizar."
            );

        var allocationSum = _refundAllocations.Sum(a => a.Amount);
        if (Math.Abs(allocationSum - AuthorizedGrandTotal.Value) > 0.01m)
            throw new InvalidOperationException(
                $"El total de las asignaciones de reembolso (${allocationSum:F2}) no coincide con el total devuelto (${AuthorizedGrandTotal.Value:F2}). "
                    + "Ajusta las asignaciones de reembolso hasta que coincidan con el total."
            );

        Status = SalesReturnStatus.Authorized;
        SetUpdated(updatedBy);

        RaiseDomainEvent(
            new SalesReturnAuthorizedEvent(
                Id,
                SalesInvoiceId,
                ReturnNumber,
                AuthorizedGrandTotal!.Value,
                updatedBy,
                TenantId,
                CompanyId,
                AuthorizedSubtotal!.Value,
                AuthorizedTotalVat!.Value,
                AuthorizedTotalIce!.Value,
                AuthorizedTotalDiscount!.Value,
                Reason
            )
        );
    }

    // ── Nota de Crédito (secuencial SRI, asignación única — irreversible) ────────
    public void SetCreditNoteDocumentNumber(string documentNumber)
    {
        if (!string.IsNullOrWhiteSpace(CreditNoteDocumentNumber))
            throw new InvalidOperationException(
                "El número de Nota de Crédito ya fue asignado y no puede modificarse."
            );
        if (string.IsNullOrWhiteSpace(documentNumber))
            throw new ArgumentException(
                "El número de Nota de Crédito es obligatorio.",
                nameof(documentNumber)
            );
        CreditNoteDocumentNumber = documentNumber.Trim();
    }

    // ── Cancel ─────────────────────────────────────────────────────────
    public void Cancel(Guid cancelledBy)
    {
        EnsureDraft();

        Status = SalesReturnStatus.Cancelled;
        SetUpdated(cancelledBy);

        RaiseDomainEvent(
            new SalesReturnCancelledEvent(
                Id,
                SalesInvoiceId,
                CustomerId,
                ReturnNumber,
                Reason,
                TenantId,
                CompanyId,
                cancelledBy
            )
        );
    }

    // ── Freeze (called once on Authorize — irreversible) ─────────────────
    private void Freeze()
    {
        foreach (var line in _lines)
            line.Freeze();
    }

    // ── Guards ─────────────────────────────────────────────────────────
    private void EnsureDraft()
    {
        if (Status != SalesReturnStatus.Draft)
            throw new InvalidOperationException(
                "Esta devolución ya no está en borrador (fue autorizada o cancelada), por lo que no se puede modificar."
            );
    }
}
