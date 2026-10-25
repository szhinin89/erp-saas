using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>Importe de una <see cref="PurchaseCostDistribution"/> asignado a una línea destino.</summary>
public sealed class PurchaseCostDistributionAllocation : IMustHaveTenant
{
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid DistributionId { get; private set; }
    public Guid TargetInvoiceDetailId { get; private set; }
    public decimal Amount { get; private set; }
    public PurchaseCostDistribution Distribution { get; private set; } = null!;

    private PurchaseCostDistributionAllocation() { }

    internal static PurchaseCostDistributionAllocation Create(PurchaseCostDistribution distribution,
        Guid lineId, decimal amount) => new()
        {
            TenantId = distribution.TenantId, InvoiceId = distribution.InvoiceId,
            DistributionId = distribution.Id, Distribution = distribution,
            TargetInvoiceDetailId = lineId, Amount = amount,
        };

    internal void SetAmount(decimal amount) => Amount = amount;
}
