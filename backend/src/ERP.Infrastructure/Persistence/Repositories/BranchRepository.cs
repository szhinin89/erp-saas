using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using Microsoft.EntityFrameworkCore;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class BranchRepository : IBranchRepository
{
    private readonly ErpDbContext _context;

    public BranchRepository(ErpDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(Branch branch, CancellationToken cancellationToken = default) =>
        _context.Branches.AddAsync(branch, cancellationToken).AsTask();

    public async Task<IReadOnlyList<Branch>> GetAsync(
        Guid tenantId,
        bool? activeFilter = true,
        string? search = null,
        CancellationToken cancellationToken = default
    )
    {
        // IgnoreQueryFilters: Branch es ICompanyOperationalEntity (filtro fail-closed tenant+company).
        // LoginHandler llama este método (heurístico de sucursal principal) antes de que exista
        // JWT/ICurrentTenant/ICurrentCompany — sin esto, cualquier login sin preferencias devuelve
        // 0 sucursales siempre. El WHERE explícito por tenantId de abajo sigue siendo la autoridad
        // real de scoping, igual que en AccessRepository/UserSessionRepository/CompanyUserBranchRepository.
        var q = _context.Branches.AsPlatformQuery().AsQueryable().Where(x => x.TenantId == tenantId);
        if (activeFilter is true)
            q = q.Where(x => x.IsActive);
        else if (activeFilter is false)
            q = q.Where(x => !x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(x =>
                EF.Functions.ILike(x.Name, pattern)
                || EF.Functions.ILike(x.Address, pattern)
                || (x.Phone != null && EF.Functions.ILike(x.Phone, pattern))
            );
        }

        return await q.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Branch>> GetByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        bool? activeFilter = true,
        string? search = null,
        CancellationToken cancellationToken = default
    )
    {
        // IgnoreQueryFilters: mismo patrón que GetAsync, pero aquí el alcance operativo
        // sí exige empresa activa explícita para no mezclar sucursales de otras empresas del tenant.
        var q = _context
            .Branches.AsPlatformQuery()
            .AsQueryable()
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId);

        if (activeFilter is true)
            q = q.Where(x => x.IsActive);
        else if (activeFilter is false)
            q = q.Where(x => !x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(x =>
                EF.Functions.ILike(x.Name, pattern)
                || EF.Functions.ILike(x.Address, pattern)
                || (x.Phone != null && EF.Functions.ILike(x.Phone, pattern))
            );
        }

        return await q.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<Branch?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        // IgnoreQueryFilters: mismo motivo que GetAsync — CompanyUserPreferencesDefaultBranchValidation
        // llama esto durante el login (revalidación de DefaultBranchId), antes de que exista contexto
        // de tenant/company. El WHERE explícito por tenantId sigue siendo la autoridad real.
        _context
            .Branches.AsPlatformQuery()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task<Branch?> GetByIdForCompanyAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        _context
            .Branches.AsPlatformQuery()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id,
                cancellationToken
            );

    public async Task<IReadOnlyList<Guid>> ClearMainBranchExceptAsync(
        Guid tenantId,
        Guid companyId,
        Guid? exceptBranchId,
        Guid updatedBy,
        CancellationToken cancellationToken = default
    )
    {
        var q = _context.Branches.Where(b =>
            b.TenantId == tenantId && b.CompanyId == companyId && b.IsMainBranch
        );
        if (exceptBranchId is not null)
            q = q.Where(b => b.Id != exceptBranchId.Value);

        var list = await q.ToListAsync(cancellationToken);
        foreach (var b in list)
            b.SetMainBranchFlag(false, updatedBy);

        return list.Select(b => b.Id).ToList();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
