using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Exceptions;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — única operación de distribución de costos adicionales.
/// Flete/otros costos de línea y totales de cabecera son siempre derivados de estas distribuciones.
/// </summary>
public sealed partial class PurchaseInvoice
{
    private readonly List<PurchaseCostDistribution> _costDistributions = new();
    public IReadOnlyList<PurchaseCostDistribution> CostDistributions => _costDistributions.AsReadOnly();

    /// <summary>Costos externos al XML: los únicos que se suman al total del documento (CxP).</summary>
    public decimal ExternalDistributedCost => _costDistributions.Where(d => d.IsExternal).Sum(d => d.Amount);

    public void BindCostDistributions()
    {
        foreach (var line in _lines) line.BindInvoice(this);
    }

    /// <summary>
    /// Crea o reemplaza (nunca suma) la distribución <c>change.Id</c>. Un reintento idéntico no
    /// cambia nada. Externa: importe obligatorio, destinos Product o Service. Transporte XML
    /// (<c>SourceLineId</c>): importe = base neta de la línea fiscal Service, destinos solo Product.
    /// </summary>
    public void ReplaceCostDistribution(PurchaseCostDistributionChange change,
        IReadOnlyDictionary<Guid, ItemNature> itemNatures, Guid updatedBy)
    {
        EnsureDraft();
        if (change.Id == Guid.Empty || !Enum.IsDefined(change.CostType) || change.TargetLineIds.Count == 0
            || change.TargetLineIds.Distinct().Count() != change.TargetLineIds.Count)
            throw new DomainRuleViolationException("La distribución requiere identidad, tipo y destinos válidos.");
        var existing = _costDistributions.SingleOrDefault(d => d.Id == change.Id);
        if (existing is not null && (existing.CostType != change.CostType
            || existing.SourceInvoiceDetailId != change.SourceLineId))
            throw new DomainRuleViolationException("No se puede cambiar el origen ni el tipo de una distribución.");
        var targets = ResolveDistributionTargets(change.TargetLineIds);
        var amount = ResolveDistributionAmount(change, targets, itemNatures);
        var shares = Prorate(amount, targets);
        if (existing is null)
        {
            if (change.ExpectedRevision != 0)
                throw new PurchaseCostDistributionStaleException();
            var created = PurchaseCostDistribution.Create(change.Id, TenantId, Id, change.CostType, change.SourceLineId);
            created.Apply(amount, shares);
            _costDistributions.Add(created);
        }
        else
        {
            if (existing.Matches(amount, shares)) return;
            existing.EnsureRevision(change.ExpectedRevision);
            existing.Apply(amount, shares);
        }
        RecalculateLineCosts();
        SetUpdated(updatedBy);
    }

    private decimal ResolveDistributionAmount(PurchaseCostDistributionChange change,
        IReadOnlyList<PurchaseInvoiceDetail> targets, IReadOnlyDictionary<Guid, ItemNature> itemNatures)
    {
        if (change.SourceLineId is not { } sourceId)
        {
            EnsureExternalTargets(targets, itemNatures);
            return change.Amount ?? throw new DomainRuleViolationException("Indique el importe a distribuir.");
        }
        if (change.Amount is not null)
            throw new DomainRuleViolationException("El importe del transporte XML se toma de su línea fiscal.");
        var source = ResolveXmlTransportSource(sourceId, targets, itemNatures);
        if (_costDistributions.Any(d => d.SourceInvoiceDetailId == sourceId && d.Id != change.Id))
            throw new DomainRuleViolationException("La línea fiscal ya tiene una distribución. Edite la existente.");
        return source.TaxableBase;
    }

    /// <summary>Retira una distribución del borrador; repetir la operación no tiene efecto.</summary>
    public void RemoveCostDistribution(Guid distributionId, long expectedRevision, Guid updatedBy)
    {
        EnsureDraft();
        var existing = _costDistributions.SingleOrDefault(d => d.Id == distributionId);
        if (existing is null) return;
        existing.EnsureRevision(expectedRevision);
        _costDistributions.Remove(existing);
        RecalculateLineCosts();
        SetUpdated(updatedBy);
    }

    /// <summary>Re-prorratea cada distribución entre sus propios destinos (bases de línea actuales).</summary>
    public void RecalculateCostDistributions(Guid updatedBy)
    {
        EnsureDraft();
        RecalculateCostDistributionsCore();
        SetUpdated(updatedBy);
    }

    private void RecalculateCostDistributionsCore()
    {
        foreach (var distribution in _costDistributions)
        {
            var targets = ResolveDistributionTargets(distribution.Allocations.Select(a => a.TargetInvoiceDetailId).ToList());
            var amount = distribution.SourceInvoiceDetailId is { } sourceId
                ? _lines.Single(l => l.Id == sourceId).TaxableBase
                : distribution.Amount;
            if (amount <= 0)
                throw new DomainRuleViolationException("La línea de transporte XML quedó sin base. Retire su distribución antes de continuar.");
            distribution.Apply(amount, Prorate(amount, targets));
        }
        RecalculateLineCosts();
    }

    /// <summary>C3 — una línea origen o destino de una distribución no se elimina ni se desglosa.</summary>
    private void EnsureLinesKeepCostDistributions(IReadOnlyCollection<PurchaseInvoiceDetail> newLines)
    {
        var referenced = _costDistributions
            .SelectMany(d => d.Allocations.Select(a => a.TargetInvoiceDetailId).Append(d.SourceInvoiceDetailId ?? Guid.Empty))
            .Where(id => id != Guid.Empty).ToHashSet();
        var missing = _lines.FirstOrDefault(l => referenced.Contains(l.Id) && newLines.All(n => n.Id != l.Id));
        if (missing is not null)
            throw new DomainRuleViolationException(
                $"La línea '{missing.Description}' participa en una distribución de costos. Edite o retire la distribución antes de eliminarla o desglosarla.");
    }

    /// <summary>Confirmación: el transporte XML conserva origen Service y destinos Product.</summary>
    private void EnsureCostDistributionsConsistent(IReadOnlyDictionary<Guid, ItemNature>? itemNatures)
    {
        if (_costDistributions.Count == 0) return;
        foreach (var distribution in _costDistributions)
        {
            var targets = ResolveDistributionTargets(distribution.Allocations.Select(a => a.TargetInvoiceDetailId).ToList());
            if (!distribution.IsExternal)
                ResolveXmlTransportSource(distribution.SourceInvoiceDetailId!.Value, targets,
                    itemNatures ?? new Dictionary<Guid, ItemNature>());
            else if (itemNatures is not null)
                EnsureExternalTargets(targets, itemNatures);
        }
        RecalculateCostDistributionsCore();
    }

    private List<PurchaseInvoiceDetail> ResolveDistributionTargets(IReadOnlyCollection<Guid> targetLineIds)
    {
        var targets = _lines.Where(l => targetLineIds.Contains(l.Id)).OrderBy(l => l.SortOrder).ToList();
        if (targets.Count != targetLineIds.Count || targets.Any(l => l.QuantityInBaseUom <= 0))
            throw new DomainRuleViolationException("Los destinos deben pertenecer a la compra y tener cantidad positiva.");
        return targets;
    }

    private PurchaseInvoiceDetail ResolveXmlTransportSource(Guid sourceId,
        IReadOnlyList<PurchaseInvoiceDetail> targets, IReadOnlyDictionary<Guid, ItemNature> itemNatures)
    {
        var source = _lines.SingleOrDefault(l => l.Id == sourceId);
        if (source is null || source.PurchaseReceptionLineId is null || source.IsXmlBreakdown
            || !HasNature(source, ItemNature.Service, itemNatures) || targets.Any(t => t.Id == sourceId)
            || targets.Any(t => !HasNature(t, ItemNature.Product, itemNatures)))
            throw new DomainRuleViolationException("El origen XML debe ser Service y sus destinos exclusivamente Product de la misma compra.");
        return source;
    }

    /// <summary>Costo externo: cada destino es un Item Product o Service de la compra (nunca una línea sin Item).</summary>
    private static void EnsureExternalTargets(IReadOnlyList<PurchaseInvoiceDetail> targets,
        IReadOnlyDictionary<Guid, ItemNature> itemNatures)
    {
        if (targets.Any(t => t.ItemId is not { } itemId || !itemNatures.ContainsKey(itemId)))
            throw new DomainRuleViolationException("Los destinos de un costo externo deben ser ítems Product o Service de la compra.");
    }

    private static bool HasNature(PurchaseInvoiceDetail line, ItemNature expected,
        IReadOnlyDictionary<Guid, ItemNature> itemNatures) =>
        line.ItemId is { } itemId && itemNatures.TryGetValue(itemId, out var nature) && nature == expected;

    private static List<(Guid LineId, decimal Amount)> Prorate(decimal amount, IReadOnlyList<PurchaseInvoiceDetail> targets)
    {
        var shares = new List<(Guid LineId, decimal Amount)>();
        ProrateAmount(amount, targets, (line, share) => shares.Add((line.Id, share)));
        return shares;
    }

    private void RecalculateLineCosts()
    {
        BindCostDistributions();
        foreach (var line in _lines) line.RecalculateDistributedCosts();
    }
}

/// <summary>Solicitud de reemplazo de una distribución (identidad + revisión esperada).</summary>
public sealed record PurchaseCostDistributionChange(Guid Id, long ExpectedRevision, PurchaseCostType CostType,
    decimal? Amount, IReadOnlyCollection<Guid> TargetLineIds, Guid? SourceLineId);
