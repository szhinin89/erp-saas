using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Application.Navigation.DTOs;
using ERP.Domain.Navigation.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public sealed class SubscriberMenuService : ITenantSessionMenuResolver, ISubscriberMenuAdminService
{
    private readonly ErpDbContext _db;
    private readonly INavigationMenuReader _reader;

    public SubscriberMenuService(ErpDbContext db, INavigationMenuReader reader)
    {
        _db = db;
        _reader = reader;
    }

    public async Task<IReadOnlyList<SessionMenuGroupDto>> ResolveForTenantAsync(Guid subscriberId, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
        {
            var m = await _reader.GetActiveMenuAsync(ct);
            return SubscriberIamMenuMerger.EnsureCompanyIamGroup(m);
        }

        var custom = await _db.SubscriberCustomMenus.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriberId == subscriberId, ct);
        if (custom is not null &&
            SessionMenuJsonParser.TryDeserialize(custom.MenuConfigJson, out var customMenu) &&
            customMenu is { Count: > 0 })
            return SubscriberIamMenuMerger.EnsureCompanyIamGroup(customMenu);

        var tenant = await _db.Subscribers.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == subscriberId, ct);
        if (tenant?.PlanCode is { Length: > 0 } pc)
        {
            var plan = await _db.CommercialPlans.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == pc, ct);
            if (plan?.MenuConfigJson is { Length: > 0 } pj &&
                SessionMenuJsonParser.TryDeserialize(pj, out var planMenu) &&
                planMenu is { Count: > 0 })
                return SubscriberIamMenuMerger.EnsureCompanyIamGroup(planMenu);
        }

        var global = await _reader.GetActiveMenuAsync(ct);
        return SubscriberIamMenuMerger.EnsureCompanyIamGroup(global);
    }

    public async Task<Result<TenantMenuResolvedDto>> GetResolvedMenuForTenantAsync(Guid subscriberId, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return Result<TenantMenuResolvedDto>.Failure("Subscriber inválido.");

        var exists = await _db.Subscribers.AsNoTracking().AnyAsync(t => t.Id == subscriberId, ct);
        if (!exists)
            return Result<TenantMenuResolvedDto>.Failure("Empresa no encontrada.");

        var custom = await _db.SubscriberCustomMenus.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriberId == subscriberId, ct);
        if (custom is not null &&
            SessionMenuJsonParser.TryDeserialize(custom.MenuConfigJson, out var customMenu) &&
            customMenu is { Count: > 0 })
        {
            return Result<TenantMenuResolvedDto>.Success(new TenantMenuResolvedDto(
                SubscriberIamMenuMerger.EnsureCompanyIamGroup(customMenu), HasCustomMenu: true, UsedPlanMenu: false, UsedGlobalFallback: false));
        }

        var tenant = await _db.Subscribers.AsNoTracking().FirstAsync(t => t.Id == subscriberId, ct);
        if (tenant.PlanCode is { Length: > 0 } pc)
        {
            var plan = await _db.CommercialPlans.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == pc, ct);
            if (plan?.MenuConfigJson is { Length: > 0 } pj &&
                SessionMenuJsonParser.TryDeserialize(pj, out var planMenu) &&
                planMenu is { Count: > 0 })
            {
                return Result<TenantMenuResolvedDto>.Success(new TenantMenuResolvedDto(
                    SubscriberIamMenuMerger.EnsureCompanyIamGroup(planMenu), HasCustomMenu: false, UsedPlanMenu: true, UsedGlobalFallback: false));
            }
        }

        var global = await _reader.GetActiveMenuAsync(ct);
        return Result<TenantMenuResolvedDto>.Success(new TenantMenuResolvedDto(
            SubscriberIamMenuMerger.EnsureCompanyIamGroup(global), HasCustomMenu: false, UsedPlanMenu: false, UsedGlobalFallback: true));
    }

    public async Task<Result<object?>> UpsertSubscriberCustomMenuAsync(Guid subscriberId, string menuConfigJson, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return Result<object?>.Failure("Subscriber inválido.");

        if (!SessionMenuJsonParser.TryDeserialize(menuConfigJson, out var parsed) || parsed is null || parsed.Count == 0)
            return Result<object?>.Failure("JSON de menú inválido o vacío.");

        try
        {
            SessionMenuTreeValidator.Validate(parsed);
        }
        catch (InvalidOperationException ex)
        {
            return Result<object?>.Failure(ex.Message);
        }

        var tenantExists = await _db.Subscribers.AnyAsync(t => t.Id == subscriberId, ct);
        if (!tenantExists)
            return Result<object?>.Failure("Empresa no encontrada.");

        var normalized = SessionMenuJsonParser.Serialize(parsed);
        var utc = DateTime.UtcNow;
        var row = await _db.SubscriberCustomMenus.FirstOrDefaultAsync(x => x.SubscriberId == subscriberId, ct);
        if (row is null)
        {
            _db.SubscriberCustomMenus.Add(SubscriberCustomMenu.Create(subscriberId, normalized, utc));
        }
        else
        {
            row.UpdateMenuJson(normalized, utc);
        }

        await _db.SaveChangesAsync(ct);
        return Result<object?>.Success(null);
    }

    public async Task<Result<object?>> DeleteSubscriberCustomMenuAsync(Guid subscriberId, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return Result<object?>.Failure("Subscriber inválido.");

        var row = await _db.SubscriberCustomMenus.FirstOrDefaultAsync(x => x.SubscriberId == subscriberId, ct);
        if (row is null)
            return Result<object?>.Success(null);

        _db.SubscriberCustomMenus.Remove(row);
        await _db.SaveChangesAsync(ct);
        return Result<object?>.Success(null);
    }

    public async Task<IReadOnlySet<Guid>> GetSubscriberIdsWithCustomMenuAsync(CancellationToken ct = default)
    {
        var ids = await _db.SubscriberCustomMenus.AsNoTracking()
            .Select(x => x.SubscriberId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }
}
