using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesInvoicePayment : IMustHaveTenant
{
    public const int CodeMaxLen = 20;
    public const int NameMaxLen = 100;
    public const int ReferenceMaxLen = 100;

    /// <summary>Escala de Amount/TenderedAmount — columnas numeric(18,2).</summary>
    public const int AmountDecimals = 2;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid PaymentMethodId { get; private set; }
    public string PaymentMethodCode { get; private set; } = null!;
    public string PaymentMethodName { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string? Reference { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// POS-CASH-TENDERED-01 — efectivo físico entregado por el cliente para ESTE pago (solo formas
    /// de cobro que mueven el cajón, <c>PaymentMethod.AffectsPhysicalCash</c>). Dato operacional
    /// del settlement: <see cref="Amount"/> sigue siendo el único importe financiero aplicado —
    /// el entregado nunca altera total, cobrado, caja, CxC ni contabilidad. Null = no aplica o no
    /// se registró (ventas anteriores a este dato).
    /// </summary>
    public decimal? TenderedAmount { get; private set; }

    /// <summary>Vuelto entregado = <see cref="TenderedAmount"/> − <see cref="Amount"/>. Derivado,
    /// no persistido: toda la información ya está en el pago.</summary>
    public decimal? ChangeAmount => TenderedAmount is { } tendered ? tendered - Amount : null;

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
        string? reference = null
    )
    {
        if (invoiceId == Guid.Empty)
            throw new ArgumentException("La factura es obligatoria.", nameof(invoiceId));
        if (paymentMethodId == Guid.Empty)
            throw new ArgumentException(
                "El método de pago es obligatorio.",
                nameof(paymentMethodId)
            );
        if (string.IsNullOrWhiteSpace(paymentMethodCode))
            throw new ArgumentException(
                "El código del método de pago es obligatorio.",
                nameof(paymentMethodCode)
            );
        if (string.IsNullOrWhiteSpace(paymentMethodName))
            throw new ArgumentException(
                "El nombre del método de pago es obligatorio.",
                nameof(paymentMethodName)
            );
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

    /// <summary>POS-CASH-TENDERED-01 — registra el efectivo entregado. Debe cubrir el importe
    /// aplicado (no existe "vuelto negativo") y respetar la escala de dinero (numeric(18,2)).</summary>
    public void SetTenderedAmount(decimal tenderedAmount)
    {
        if (tenderedAmount <= 0)
            throw new ArgumentException(
                "El efectivo recibido debe ser mayor a cero.",
                nameof(tenderedAmount)
            );
        if (decimal.Round(tenderedAmount, AmountDecimals) != tenderedAmount)
            throw new ArgumentException(
                "El efectivo recibido admite como máximo 2 decimales.",
                nameof(tenderedAmount)
            );
        if (tenderedAmount < Amount)
            throw new ArgumentException(
                "El efectivo recibido no puede ser menor al monto aplicado en efectivo.",
                nameof(tenderedAmount)
            );
        TenderedAmount = tenderedAmount;
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
