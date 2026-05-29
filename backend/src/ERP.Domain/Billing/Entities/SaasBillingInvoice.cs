using ERP.Domain.Billing.Enums;
using ERP.Domain.Common;

namespace ERP.Domain.Billing.Entities;

/// <summary>Factura SaaS del suscriptor (suscripción/plan). No confundir con <c>sales_invoice</c> ERP.</summary>
public sealed class SaasBillingInvoice : AuditableEntity
{
    public const int InvoiceNumberMaxLen = 40;
    public const int CurrencyMaxLen = 3;
    public const int ExternalIdMaxLen = 200;

    public string InvoiceNumber { get; private set; } = null!;
    public SaasBillingInvoiceStatus Status { get; private set; }
    public PaymentProviderType ProviderType { get; private set; }
    public string? ExternalInvoiceId { get; private set; }
    public string CurrencyCode { get; private set; } = "USD";
    public decimal Subtotal { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal Total { get; private set; }
    public DateTime PeriodStartUtc { get; private set; }
    public DateTime PeriodEndUtc { get; private set; }
    public DateTime? IssuedAtUtc { get; private set; }
    public DateTime? DueAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    /// <summary>ID de la factura ERP emitida por ZH Technologies para este cobro SaaS.</summary>
    public Guid? ErpInvoiceId { get; private set; }

    public ICollection<SaasBillingInvoiceLine> Lines { get; private set; } = [];

    private SaasBillingInvoice() { }

    public static SaasBillingInvoice CreateDraft(
        Guid subscriberId,
        string invoiceNumber,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        string currencyCode,
        Guid createdBy,
        PaymentProviderType providerType = PaymentProviderType.None)
    {
        var inv = new SaasBillingInvoice
        {
            Id = Guid.NewGuid(),
            SubscriberId = subscriberId,
            InvoiceNumber = invoiceNumber.Trim(),
            Status = SaasBillingInvoiceStatus.Draft,
            ProviderType = providerType,
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
        };
        inv.SetCreated(createdBy);
        return inv;
    }

    public void AddLine(SaasBillingInvoiceLine line)
    {
        Lines.Add(line);
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        Subtotal = Lines.Where(l => l.LineType != SaasBillingInvoiceLineType.Tax)
            .Sum(l => l.LineTotal);
        TaxAmount = Lines.Where(l => l.LineType == SaasBillingInvoiceLineType.Tax)
            .Sum(l => l.LineTotal);
        Total = Subtotal + TaxAmount;
    }

    public void Issue(DateTime? dueAtUtc, Guid updatedBy)
    {
        Status = SaasBillingInvoiceStatus.Open;
        IssuedAtUtc = DateTime.UtcNow;
        DueAtUtc = dueAtUtc;
        SetUpdated(updatedBy);
    }

    public void MarkPaid(Guid updatedBy)
    {
        Status = SaasBillingInvoiceStatus.Paid;
        PaidAtUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    public void Void(Guid updatedBy)
    {
        Status = SaasBillingInvoiceStatus.Void;
        SetUpdated(updatedBy);
    }

    public void LinkErpInvoice(Guid erpInvoiceId, Guid updatedBy)
    {
        ErpInvoiceId = erpInvoiceId;
        SetUpdated(updatedBy);
    }
}
