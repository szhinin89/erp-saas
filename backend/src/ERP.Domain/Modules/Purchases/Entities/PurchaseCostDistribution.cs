using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Exceptions;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — única fuente de verdad de un costo adicional repartido
/// entre líneas de la compra. Sin origen (<see cref="SourceInvoiceDetailId"/> nulo) es un costo externo
/// que se suma al total del documento; con origen es la línea fiscal de transporte del XML, cuya base
/// neta ya está en el total y solo se transfiere al costo de los productos. Identidad estable +
/// <see cref="Revision"/> para reemplazo idempotente y control de concurrencia.
/// </summary>
public sealed class PurchaseCostDistribution : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public PurchaseCostType CostType { get; private set; }
    public Guid? SourceInvoiceDetailId { get; private set; }
    public decimal Amount { get; private set; }
    public long Revision { get; private set; }
    private readonly List<PurchaseCostDistributionAllocation> _allocations = new();
    public IReadOnlyList<PurchaseCostDistributionAllocation> Allocations => _allocations.AsReadOnly();
    public bool IsExternal => SourceInvoiceDetailId is null;

    private PurchaseCostDistribution() { }

    internal static PurchaseCostDistribution Create(Guid id, Guid tenantId, Guid invoiceId,
        PurchaseCostType costType, Guid? sourceDetailId) => new()
        {
            Id = id, TenantId = tenantId, InvoiceId = invoiceId, CostType = costType,
            SourceInvoiceDetailId = sourceDetailId,
        };

    internal bool Matches(decimal amount, IReadOnlyList<(Guid LineId, decimal Amount)> allocations) =>
        Amount == amount && _allocations.Count == allocations.Count
        && allocations.All(a => _allocations.Any(b => b.TargetInvoiceDetailId == a.LineId && b.Amount == a.Amount));

    internal void EnsureRevision(long expectedRevision)
    {
        if (Revision != expectedRevision)
            throw new PurchaseCostDistributionStaleException();
    }

    /// <summary>Reemplaza importe y destinos; nunca suma sobre lo anterior. Devuelve false si no cambió.</summary>
    internal bool Apply(decimal amount, IReadOnlyList<(Guid LineId, decimal Amount)> allocations)
    {
        if (amount <= 0 || amount != Math.Round(amount, FiscalPrecision.TaxAmount)
            || allocations.Count == 0 || allocations.Sum(a => a.Amount) != amount
            || allocations.Any(a => a.Amount < 0 || a.Amount != Math.Round(a.Amount, FiscalPrecision.UnitCost))
            || allocations.Select(a => a.LineId).Distinct().Count() != allocations.Count)
            throw new DomainRuleViolationException("Las asignaciones deben conservar exactamente el importe distribuido.");
        if (Matches(amount, allocations)) return false;
        Amount = amount;
        Revision++;
        _allocations.RemoveAll(a => allocations.All(n => n.LineId != a.TargetInvoiceDetailId));
        foreach (var (lineId, share) in allocations)
        {
            var current = _allocations.SingleOrDefault(a => a.TargetInvoiceDetailId == lineId);
            if (current is null) _allocations.Add(PurchaseCostDistributionAllocation.Create(this, lineId, share));
            else current.SetAmount(share);
        }
        return true;
    }
}
