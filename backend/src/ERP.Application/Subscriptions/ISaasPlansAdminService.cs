using ERP.Application.Common;

namespace ERP.Application.Subscriptions;

public interface ISaasPlansAdminService
{
    Task<IReadOnlyList<SaasPlanAdminDto>> ListPlansAdminAsync(CancellationToken ct = default);

    Task<Result<Guid>> CreatePlanAsync(CreateSaasPlanRequest request, CancellationToken ct = default);

    Task<Result<object?>> UpdatePlanAsync(Guid planId, UpdateSaasPlanRequest request, CancellationToken ct = default);

    Task<Result<object?>> DeletePlanAsync(Guid planId, CancellationToken ct = default);

    Task<Result<object?>> ReorderPlansAsync(IReadOnlyList<Guid> orderedPlanIds, CancellationToken ct = default);

    Task<Result<object?>> SetRecommendedPlanAsync(Guid planId, CancellationToken ct = default);

    Task<Result<PlanMenuReadDto>> GetPlanMenuAsync(Guid planId, CancellationToken ct = default);

    Task<Result<object?>> SetPlanMenuJsonAsync(
        Guid planId,
        string? menuConfigJson,
        string? menuSidebarLayout = null,
        CancellationToken ct = default);

    Task<Result<object?>> CopyPlanFromAsync(
        Guid targetPlanId,
        Guid sourcePlanId,
        bool copyMenu,
        CancellationToken ct = default);
}
