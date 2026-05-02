namespace ERP.Application.Subscriptions;

public interface ISaasCatalogQuery
{
    Task<IReadOnlyList<SaasPlanCatalogItem>> GetPlansWithFeaturesAsync(CancellationToken ct = default);
}
