namespace ERP.Application.Subscriptions.CommercialPlanLimits;

/// <summary>Proveedor de uso actual para un <c>limit_code</c> de plan comercial.</summary>
public interface ICommercialLimitUsageProvider
{
    bool Supports(string limitCode);

    Task<long> GetCurrentUsageAsync(Guid subscriberId, CancellationToken ct = default);
}
