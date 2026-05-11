using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public sealed class SaasPlansAdminService : ISaasPlansAdminService
{
    private readonly ErpDbContext _db;

    public SaasPlansAdminService(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SaasFeatureDefinitionAdminDto>> ListFeatureDefinitionsAsync(CancellationToken ct = default)
    {
        var rows = await _db.SaasFeatureDefinitions.AsNoTracking()
            .OrderBy(f => f.Code)
            .ToListAsync(ct);
        return rows.Select(f => new SaasFeatureDefinitionAdminDto(
            f.Id,
            f.Code,
            f.Name,
            f.Description,
            f.IsMetered,
            f.Kind,
            f.ResourceRef)).ToList();
    }

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

    public async Task<Result<object?>> ReplacePlanFeaturesAsync(
        Guid planId,
        IReadOnlyList<PlanFeatureAssignDto> rows,
        CancellationToken ct = default)
    {
        var planExists = await _db.SaasPlans.AnyAsync(p => p.Id == planId, ct);
        if (!planExists)
            return Result<object?>.Failure("Plan no encontrado.");

        var featureIds = rows.Select(r => r.FeatureId).Distinct().ToList();
        var found = await _db.SaasFeatureDefinitions.CountAsync(f => featureIds.Contains(f.Id), ct);
        if (found != featureIds.Count)
            return Result<object?>.Failure("Una o más features no existen en el catálogo.");

        var existing = await _db.SaasPlanFeatures.Where(pf => pf.PlanId == planId).ToListAsync(ct);
        _db.SaasPlanFeatures.RemoveRange(existing);

        foreach (var row in rows)
        {
            _db.SaasPlanFeatures.Add(SaasPlanFeature.Create(planId, row.FeatureId, row.IsIncluded, row.LimitPerPeriod));
        }

        await _db.SaveChangesAsync(ct);
        return Result<object?>.Success(null);
    }

    public async Task<Result<Guid>> CreateFeatureDefinitionAsync(CreateSaasFeatureDefinitionRequest request, CancellationToken ct = default)
    {
        try
        {
            var code = request.Code.Trim().ToUpperInvariant();
            if (await _db.SaasFeatureDefinitions.AnyAsync(f => f.Code == code, ct))
                return Result<Guid>.Failure("Ya existe una feature con ese código.");

            var f = SaasFeatureDefinition.Create(
                request.Code,
                request.Name,
                request.Description,
                request.IsMetered,
                request.Kind,
                request.ResourceRef);
            _db.SaasFeatureDefinitions.Add(f);
            await _db.SaveChangesAsync(ct);
            return Result<Guid>.Success(f.Id);
        }
        catch (ArgumentException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }
    }

    public async Task<Result<object?>> UpdateFeatureDefinitionAsync(
        Guid featureId,
        UpdateSaasFeatureDefinitionRequest request,
        CancellationToken ct = default)
    {
        var f = await _db.SaasFeatureDefinitions.FirstOrDefaultAsync(x => x.Id == featureId, ct);
        if (f is null)
            return Result<object?>.Failure("Feature no encontrada.");

        try
        {
            f.Update(request.Name, request.Description, request.IsMetered, request.Kind, request.ResourceRef);
            await _db.SaveChangesAsync(ct);
            return Result<object?>.Success(null);
        }
        catch (ArgumentException ex)
        {
            return Result<object?>.Failure(ex.Message);
        }
    }

    public async Task<Result<object?>> DeleteFeatureDefinitionAsync(Guid featureId, CancellationToken ct = default)
    {
        var f = await _db.SaasFeatureDefinitions.FirstOrDefaultAsync(x => x.Id == featureId, ct);
        if (f is null)
            return Result<object?>.Failure("Feature no encontrada.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await _db.SaasPlanFeatures.Where(pf => pf.FeatureId == featureId).ExecuteDeleteAsync(ct);
            // IQF: borrar overrides/usos de la feature en todos los tenants (operación global SuperAdmin).
            await _db.TenantSubscriptionFeatureOverrides
                .IgnoreQueryFilters()
                .Where(o => o.FeatureId == featureId)
                .ExecuteDeleteAsync(ct);
            await _db.TenantSubscriptionUsages
                .IgnoreQueryFilters()
                .Where(u => u.FeatureId == featureId)
                .ExecuteDeleteAsync(ct);
            await _db.UiNavItems
                .Where(i => i.SaasFeatureDefinitionId == featureId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.SaasFeatureDefinitionId, (Guid?)null), ct);

            _db.SaasFeatureDefinitions.Remove(f);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<object?>.Success(null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return Result<object?>.Failure($"No se pudo eliminar la definición: {ex.Message}");
        }
    }

    private async Task ClearRecommendedExceptAsync(Guid? exceptPlanId, CancellationToken ct)
    {
        var recommended = await _db.SaasPlans.Where(p => p.IsRecommended && (exceptPlanId == null || p.Id != exceptPlanId)).ToListAsync(ct);
        foreach (var p in recommended)
            p.SetRecommended(false);
    }
}
