using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesInvoicePayment : IMustHaveTenant
{
    public const int CodeMaxLen = 20;
    public const int NameMaxLen = 100;
    public const int ReferenceMaxLen = 100;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid PaymentMethodId { get; private set; }
    public string PaymentMethodCode { get; private set; } = null!;
    public string PaymentMethodName { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string? Reference { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // ── Detail (1:1 nullable — only one can exist) ─────────────────
    public PaymentCardDetail? CardDetail { get; private set; }
    public PaymentTransferDetail? TransferDetail { get; private set; }
    public PaymentChequeDetail? ChequeDetail { get; private set; }

    private SalesInvoicePayment() { }

    public static SalesInvoicePayment Create(
        Guid invoiceId,
        Guid tenantId,
        Guid paymentMethodId,
        string paymentMethodCode,
        string paymentMethodName,
        decimal amount,
        string? reference = null)
    {
        if (invoiceId == Guid.Empty)
            throw new ArgumentException("La factura es obligatoria.", nameof(invoiceId));
        if (paymentMethodId == Guid.Empty)
            throw new ArgumentException("El método de pago es obligatorio.", nameof(paymentMethodId));
        if (string.IsNullOrWhiteSpace(paymentMethodCode))
            throw new ArgumentException("El código del método de pago es obligatorio.", nameof(paymentMethodCode));
        if (string.IsNullOrWhiteSpace(paymentMethodName))
            throw new ArgumentException("El nombre del método de pago es obligatorio.", nameof(paymentMethodName));
        if (amount <= 0)
            throw new ArgumentException("El monto debe ser mayor a cero.", nameof(amount));

        return new SalesInvoicePayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoiceId,
            PaymentMethodId = paymentMethodId,
            PaymentMethodCode = paymentMethodCode.Trim(),
            PaymentMethodName = paymentMethodName.Trim(),
            Amount = amount,
            Reference = reference?.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void SetCardDetail(PaymentCardDetail detail)
    {
        CardDetail = detail ?? throw new ArgumentNullException(nameof(detail));
    }

    public void SetTransferDetail(PaymentTransferDetail detail)
    {
        TransferDetail = detail ?? throw new ArgumentNullException(nameof(detail));
    }

    public void SetChequeDetail(PaymentChequeDetail detail)
    {
        ChequeDetail = detail ?? throw new ArgumentNullException(nameof(detail));
    }
}
