namespace ERP.Application.Subscriptions;

public interface ICommercialCatalogQuery
{
    Task<IReadOnlyList<CommercialPlanCatalogItem>> GetPlansWithFeaturesAsync(CancellationToken ct = default);
}
