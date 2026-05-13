using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Application.Navigation.DTOs;
using ERP.Domain.Navigation.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public sealed class TenantMenuService : ITenantSessionMenuResolver, ITenantMenuAdminService
{
    private readonly ErpDbContext _db;
    private readonly INavigationMenuReader _reader;

    public TenantMenuService(ErpDbContext db, INavigationMenuReader reader)
    {
        _db = db;
        _reader = reader;
    }

    public async Task<IReadOnlyList<SessionMenuGroupDto>> ResolveForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return await _reader.GetActiveMenuAsync(ct);

        var custom = await _db.TenantCustomMenus.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (custom is not null &&
            SessionMenuJsonParser.TryDeserialize(custom.MenuConfigJson, out var customMenu) &&
            customMenu is { Count: > 0 })
            return customMenu;

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant?.PlanCode is { Length: > 0 } pc)
        {
            var plan = await _db.SaasPlans.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == pc, ct);
            if (plan?.MenuConfigJson is { Length: > 0 } pj &&
                SessionMenuJsonParser.TryDeserialize(pj, out var planMenu) &&
                planMenu is { Count: > 0 })
                return planMenu;
        }

        return await _reader.GetActiveMenuAsync(ct);
    }

    public async Task<Result<TenantMenuResolvedDto>> GetResolvedMenuForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return Result<TenantMenuResolvedDto>.Failure("Tenant inválido.");

        var exists = await _db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, ct);
        if (!exists)
            return Result<TenantMenuResolvedDto>.Failure("Empresa no encontrada.");

        var custom = await _db.TenantCustomMenus.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (custom is not null &&
            SessionMenuJsonParser.TryDeserialize(custom.MenuConfigJson, out var customMenu) &&
            customMenu is { Count: > 0 })
        {
            return Result<TenantMenuResolvedDto>.Success(new TenantMenuResolvedDto(
                customMenu, HasCustomMenu: true, UsedPlanMenu: false, UsedGlobalFallback: false));
        }

        var tenant = await _db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct);
        if (tenant.PlanCode is { Length: > 0 } pc)
        {
            var plan = await _db.SaasPlans.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == pc, ct);
            if (plan?.MenuConfigJson is { Length: > 0 } pj &&
                SessionMenuJsonParser.TryDeserialize(pj, out var planMenu) &&
                planMenu is { Count: > 0 })
            {
                return Result<TenantMenuResolvedDto>.Success(new TenantMenuResolvedDto(
                    planMenu, HasCustomMenu: false, UsedPlanMenu: true, UsedGlobalFallback: false));
            }
        }

        var global = await _reader.GetActiveMenuAsync(ct);
        return Result<TenantMenuResolvedDto>.Success(new TenantMenuResolvedDto(
            global, HasCustomMenu: false, UsedPlanMenu: false, UsedGlobalFallback: true));
    }

    public async Task<Result<object?>> UpsertTenantCustomMenuAsync(Guid tenantId, string menuConfigJson, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return Result<object?>.Failure("Tenant inválido.");

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

        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == tenantId, ct);
        if (!tenantExists)
            return Result<object?>.Failure("Empresa no encontrada.");

        var normalized = SessionMenuJsonParser.Serialize(parsed);
        var utc = DateTime.UtcNow;
        var row = await _db.TenantCustomMenus.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (row is null)
        {
            _db.TenantCustomMenus.Add(TenantCustomMenu.Create(tenantId, normalized, utc));
        }
        else
        {
            row.UpdateMenuJson(normalized, utc);
        }

        await _db.SaveChangesAsync(ct);
        return Result<object?>.Success(null);
    }

    public async Task<Result<object?>> DeleteTenantCustomMenuAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return Result<object?>.Failure("Tenant inválido.");

        var row = await _db.TenantCustomMenus.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (row is null)
            return Result<object?>.Success(null);

        _db.TenantCustomMenus.Remove(row);
        await _db.SaveChangesAsync(ct);
        return Result<object?>.Success(null);
    }

    public async Task<IReadOnlySet<Guid>> GetTenantIdsWithCustomMenuAsync(CancellationToken ct = default)
    {
        var ids = await _db.TenantCustomMenus.AsNoTracking()
            .Select(x => x.TenantId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }
}
