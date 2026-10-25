using ERP.Domain.Exceptions;

namespace ERP.Domain.Modules.Purchases.Exceptions;

/// <summary>
/// PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — la revisión esperada de una distribución de costos ya no
/// es la vigente. Código público estable para que el cliente recargue la compra sin interpretar el texto.
/// </summary>
public sealed class PurchaseCostDistributionStaleException
    : DomainRuleViolationException,
        IApiCodedDomainRule
{
    public const string ErrorCode = "PURCHASE_COST_DISTRIBUTION_STALE";

    public PurchaseCostDistributionStaleException()
        : base("La distribución de costos cambió. Recargue la compra antes de modificarla.") { }

    public string ApiCode => ErrorCode;
}
