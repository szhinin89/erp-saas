using ERP.Domain.Billing.Enums;
using ERP.Domain.Common;

namespace ERP.Domain.Billing.Entities;

public sealed class SaasBillingInvoiceLine : BaseEntity
{
    public const int DescriptionMaxLen = 500;

    public Guid BillingInvoiceId { get; private set; }
    public SaasBillingInvoiceLineType LineType { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitAmount { get; private set; }
    public decimal LineTotal { get; private set; }
    public Guid? CommercialPlanId { get; private set; }

    private SaasBillingInvoiceLine() { }

    public static SaasBillingInvoiceLine Create(
        Guid subscriberId,
        Guid billingInvoiceId,
        SaasBillingInvoiceLineType lineType,
        string description,
        decimal quantity,
        decimal unitAmount,
        Guid? commercialPlanId = null)
    {
        var qty = quantity <= 0 ? 1 : quantity;
        var total = Math.Round(qty * unitAmount, 2, MidpointRounding.AwayFromZero);
        return new SaasBillingInvoiceLine
        {
            Id = Guid.NewGuid(),
            SubscriberId = subscriberId,
            BillingInvoiceId = billingInvoiceId,
            LineType = lineType,
            Description = description.Trim(),
            Quantity = qty,
            UnitAmount = unitAmount,
            LineTotal = total,
            CommercialPlanId = commercialPlanId,
        };
    }
}
