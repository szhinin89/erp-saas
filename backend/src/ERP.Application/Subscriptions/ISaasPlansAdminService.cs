using ERP.Application.Common;

namespace ERP.Application.Subscriptions;

public interface ISaasPlansAdminService
{
    Task<IReadOnlyList<SaasPlanAdminDto>> ListPlansAdminAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SaasFeatureDefinitionAdminDto>> ListFeatureDefinitionsAsync(CancellationToken ct = default);

    Task<Result<Guid>> CreatePlanAsync(CreateSaasPlanRequest request, CancellationToken ct = default);

    Task<Result<object?>> UpdatePlanAsync(Guid planId, UpdateSaasPlanRequest request, CancellationToken ct = default);

    Task<Result<object?>> DeletePlanAsync(Guid planId, CancellationToken ct = default);

    Task<Result<object?>> ReorderPlansAsync(IReadOnlyList<Guid> orderedPlanIds, CancellationToken ct = default);

    Task<Result<object?>> SetRecommendedPlanAsync(Guid planId, CancellationToken ct = default);

    Task<Result<object?>> ReplacePlanFeaturesAsync(Guid planId, IReadOnlyList<PlanFeatureAssignDto> rows, CancellationToken ct = default);

    Task<Result<Guid>> CreateFeatureDefinitionAsync(CreateSaasFeatureDefinitionRequest request, CancellationToken ct = default);

    Task<Result<object?>> UpdateFeatureDefinitionAsync(Guid featureId, UpdateSaasFeatureDefinitionRequest request, CancellationToken ct = default);
}
