namespace ERP.Application.Subscriptions;

public interface ISaasPublicPlansQuery
{
    Task<IReadOnlyList<SaasPublicPlanDto>> GetVisiblePlansAsync(CancellationToken ct = default);
}
