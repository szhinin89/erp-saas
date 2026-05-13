using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Application.Subscriptions;
using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public sealed class SaasPlansAdminService : ISaasPlansAdminService
{
    private readonly ErpDbContext _db;

    public SaasPlansAdminService(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SaasPlanAdminDto>> ListPlansAdminAsync(CancellationToken ct = default)
    {
        var plans = await _db.SaasPlans.AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
            .ToListAsync(ct);
        if (plans.Count == 0) return Array.Empty<SaasPlanAdminDto>();

        var planIds = plans.Select(p => p.Id).ToList();
        var links = await _db.SaasPlanFeatures.AsNoTracking()
            .Where(pf => planIds.Contains(pf.PlanId))
            .ToListAsync(ct);
        var featureIds = links.Select(l => l.FeatureId).Distinct().ToList();
        var defs = await _db.SaasFeatureDefinitions.AsNoTracking()
            .Where(f => featureIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f, ct);

        var result = new List<SaasPlanAdminDto>(plans.Count);
        foreach (var plan in plans)
        {
            var feats = links
                .Where(l => l.PlanId == plan.Id && defs.ContainsKey(l.FeatureId))
                .Select(l =>
                {
                    var d = defs[l.FeatureId];
                    return new SaasPlanFeatureAdminDto(
                        d.Id,
                        d.Code,
                        d.Name,
                        d.IsMetered,
                        d.Kind,
                        d.ResourceRef,
                        l.IsIncluded,
                        l.LimitPerPeriod);
                })
                .OrderBy(x => x.FeatureCode)
                .ToList();

            result.Add(new SaasPlanAdminDto(
                plan.Id,
                plan.Code,
                plan.Name,
                plan.ShortLabel,
                plan.IsActive,
                plan.PriceAmount,
                plan.Currency,
                plan.BillingCycle,
                plan.IsPubliclyVisible,
                plan.IsRecommended,
                plan.SortOrder,
                plan.ExternalBillingRef,
                !string.IsNullOrWhiteSpace(plan.MenuConfigJson),
                string.IsNullOrWhiteSpace(plan.MenuSidebarLayout)
                    ? SaasPlan.MenuSidebarLayoutHorizontal
                    : plan.MenuSidebarLayout,
                feats));
        }

        return result;
    }

    public async Task<Result<Guid>> CreatePlanAsync(CreateSaasPlanRequest request, CancellationToken ct = default)
    {
        try
        {
            var code = request.Code.Trim().ToLowerInvariant();
            if (await _db.SaasPlans.AnyAsync(p => p.Code == code, ct))
                return Result<Guid>.Failure("Ya existe un plan con ese código.");

            var plan = SaasPlan.Create(
                request.Code,
                request.Name,
                request.ShortLabel,
                request.IsActive,
                request.PriceAmount,
                request.Currency,
                request.BillingCycle,
                request.IsPubliclyVisible,
                request.IsRecommended,
                request.SortOrder,
                request.ExternalBillingRef);

            if (request.IsRecommended)
                await ClearRecommendedExceptAsync(plan.Id, ct);

            _db.SaasPlans.Add(plan);
            await _db.SaveChangesAsync(ct);
            return Result<Guid>.Success(plan.Id);
        }
        catch (ArgumentException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }
    }

    public async Task<Result<object?>> UpdatePlanAsync(Guid planId, UpdateSaasPlanRequest request, CancellationToken ct = default)
    {
        var plan = await _db.SaasPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null)
            return Result<object?>.Failure("Plan no encontrado.");

        try
        {
            plan.UpdateCatalog(
                request.Name,
                request.ShortLabel,
                request.PriceAmount,
                request.Currency,
                request.BillingCycle,
                request.IsPubliclyVisible,
                request.IsActive,
                request.ExternalBillingRef);

            if (request.MenuSidebarLayout is not null)
            {
                try
                {
                    plan.SetMenuSidebarLayout(request.MenuSidebarLayout);
                }
                catch (ArgumentException ex)
                {
                    return Result<object?>.Failure(ex.Message);
                }
            }

            await _db.SaveChangesAsync(ct);
            return Result<object?>.Success(null);
        }
        catch (ArgumentException ex)
        {
            return Result<object?>.Failure(ex.Message);
        }
    }

    public async Task<Result<object?>> DeletePlanAsync(Guid planId, CancellationToken ct = default)
    {
        var used = await _db.TenantSaasSubscriptions.AsNoTracking().AnyAsync(s => s.PlanId == planId, ct);
        if (used)
            return Result<object?>.Failure("No se puede eliminar: hay suscripciones de tenant usando este plan.");

        var links = await _db.SaasPlanFeatures.Where(pf => pf.PlanId == planId).ToListAsync(ct);
        _db.SaasPlanFeatures.RemoveRange(links);

        var plan = await _db.SaasPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null)
            return Result<object?>.Failure("Plan no encontrado.");

        _db.SaasPlans.Remove(plan);
        await _db.SaveChangesAsync(ct);
        return Result<object?>.Success(null);
    }

    public async Task<Result<object?>> ReorderPlansAsync(IReadOnlyList<Guid> orderedPlanIds, CancellationToken ct = default)
    {
        if (orderedPlanIds.Count == 0)
            return Result<object?>.Failure("Lista vacía.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var order = 0;
        foreach (var id in orderedPlanIds)
        {
            var plan = await _db.SaasPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (plan is null)
            {
                await tx.RollbackAsync(ct);
                return Result<object?>.Failure($"Plan no encontrado: {id}");
            }

            plan.SetSortOrder(order++);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result<object?>.Success(null);
    }

    public async Task<Result<object?>> SetRecommendedPlanAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _db.SaasPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null)
            return Result<object?>.Failure("Plan no encontrado.");

        await ClearRecommendedExceptAsync(planId, ct);
        plan.SetRecommended(true);
        await _db.SaveChangesAsync(ct);
        return Result<object?>.Success(null);
    }

    public async Task<Result<PlanMenuReadDto>> GetPlanMenuAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _db.SaasPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null)
            return Result<PlanMenuReadDto>.Failure("Plan no encontrado.");
        var layout = string.IsNullOrWhiteSpace(plan.MenuSidebarLayout)
            ? SaasPlan.MenuSidebarLayoutHorizontal
            : plan.MenuSidebarLayout;
        return Result<PlanMenuReadDto>.Success(new PlanMenuReadDto(plan.MenuConfigJson, layout));
    }

    public async Task<Result<object?>> SetPlanMenuJsonAsync(
        Guid planId,
        string? menuConfigJson,
        string? menuSidebarLayout = null,
        CancellationToken ct = default)
    {
        var plan = await _db.SaasPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null)
            return Result<object?>.Failure("Plan no encontrado.");

        var raw = menuConfigJson?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            plan.SetMenuConfigJson(null);
            if (menuSidebarLayout is not null)
            {
                try
                {
                    plan.SetMenuSidebarLayout(menuSidebarLayout);
                }
                catch (ArgumentException ex)
                {
                    return Result<object?>.Failure(ex.Message);
                }
            }

            await _db.SaveChangesAsync(ct);
            return Result<object?>.Success(null);
        }

        if (!SessionMenuJsonParser.TryDeserialize(raw, out var parsed) || parsed is null || parsed.Count == 0)
            return Result<object?>.Failure("JSON de menú inválido o vacío.");

        try
        {
            SessionMenuTreeValidator.Validate(parsed);
        }
        catch (InvalidOperationException ex)
        {
            return Result<object?>.Failure(ex.Message);
        }

        plan.SetMenuConfigJson(SessionMenuJsonParser.Serialize(parsed));
        if (menuSidebarLayout is not null)
        {
            try
            {
                plan.SetMenuSidebarLayout(menuSidebarLayout);
            }
            catch (ArgumentException ex)
            {
                return Result<object?>.Failure(ex.Message);
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result<object?>.Success(null);
    }

    public async Task<Result<object?>> CopyPlanFromAsync(
        Guid targetPlanId,
        Guid sourcePlanId,
        bool copyMenu,
        CancellationToken ct = default)
    {
        if (targetPlanId == sourcePlanId)
            return Result<object?>.Failure("El plan origen y destino no pueden ser el mismo.");

        if (!copyMenu)
            return Result<object?>.Failure("Seleccione al menos copiar menú.");

        var target = await _db.SaasPlans.FirstOrDefaultAsync(p => p.Id == targetPlanId, ct);
        if (target is null)
            return Result<object?>.Failure("Plan destino no encontrado.");

        var source = await _db.SaasPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == sourcePlanId, ct);
        if (source is null)
            return Result<object?>.Failure("Plan origen no encontrado.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            if (copyMenu)
            {
                target.SetMenuConfigJson(source.MenuConfigJson);
                try
                {
                    target.SetMenuSidebarLayout(source.MenuSidebarLayout);
                }
                catch (ArgumentException)
                {
                    target.SetMenuSidebarLayout(SaasPlan.MenuSidebarLayoutHorizontal);
                }
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<object?>.Success(null);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task ClearRecommendedExceptAsync(Guid? exceptPlanId, CancellationToken ct)
    {
        var recommended = await _db.SaasPlans.Where(p => p.IsRecommended && (exceptPlanId == null || p.Id != exceptPlanId)).ToListAsync(ct);
        foreach (var p in recommended)
            p.SetRecommended(false);
    }
}
