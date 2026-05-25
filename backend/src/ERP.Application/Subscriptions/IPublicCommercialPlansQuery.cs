namespace ERP.Application.Subscriptions;

public interface IPublicCommercialPlansQuery
{
    Task<IReadOnlyList<PublicCommercialPlanDto>> GetVisiblePlansAsync(CancellationToken ct = default);
}
