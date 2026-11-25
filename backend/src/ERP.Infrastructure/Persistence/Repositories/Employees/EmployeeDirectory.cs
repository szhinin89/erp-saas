using ERP.Application.Common;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.Interfaces;
using ERP.Domain.Modules.Employees.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Employees;

/// <summary>
/// Implementación de <see cref="IEmployeeDirectory"/> (EMP-E6b): solo lectura, sin seguimiento, con tenant y
/// empresa del contexto operacional como predicado propio de cada consulta (el directorio no pasa por
/// <c>CompanyScopeBehavior</c>) y sin <c>IgnoreQueryFilters</c>. La lista usa un número fijo de consultas,
/// independiente de la cantidad de fichas. Sin transacciones ni locks.
/// </summary>
public sealed class EmployeeDirectory : IEmployeeDirectory
{
    private readonly ErpDbContext _context;
    private readonly IOperationalContext _operational;
    private readonly IEmployeeIdentityProjector _identity;

    public EmployeeDirectory(
        ErpDbContext context,
        IOperationalContext operational,
        IEmployeeIdentityProjector identity
    ) => (_context, _operational, _identity) = (context, operational, identity);

    /// <summary>Fallo cerrado: sin contexto operacional no hay consulta posible.</summary>
    private (Guid TenantId, Guid CompanyId) Scope()
    {
        var scope = (_operational.TenantId, _operational.CompanyId);
        if (scope.TenantId == Guid.Empty || scope.CompanyId == Guid.Empty)
            throw new InvalidOperationException(
                "Invariante violada: el directorio de empleados requiere tenant y empresa en el contexto."
            );
        return scope;
    }

    private IQueryable<Employee> Employees((Guid TenantId, Guid CompanyId) s) =>
        _context
            .Employees.AsNoTracking()
            .Where(e => e.TenantId == s.TenantId && e.CompanyId == s.CompanyId);

    private IQueryable<EmployeeEmploymentPeriod> Periods((Guid TenantId, Guid CompanyId) s) =>
        _context
            .EmployeeEmploymentPeriods.AsNoTracking()
            .Where(p => p.TenantId == s.TenantId && p.CompanyId == s.CompanyId);

    private IQueryable<EmployeeBranchAssignment> Assignments((Guid TenantId, Guid CompanyId) s) =>
        _context
            .EmployeeBranchAssignments.AsNoTracking()
            .Where(a => a.TenantId == s.TenantId && a.CompanyId == s.CompanyId);

    public async Task<EmployeeSummary?> GetByIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var scope = Scope();
        var list = await SummariesAsync(scope, Employees(scope).Where(e => e.Id == employeeId), ct);
        return list.Count == 0 ? null : list[0];
    }

    public async Task<IReadOnlyList<EmployeeSummary>> ListAsync(
        Guid companyId,
        bool includeInactive,
        EmployeeDirectoryFilter? filter = null,
        CancellationToken ct = default
    )
    {
        var scope = Scope();
        // La empresa solicitada solo se verifica: nunca amplía el alcance del contexto.
        if (companyId != scope.CompanyId)
            return Array.Empty<EmployeeSummary>();

        var query = Employees(scope);
        if (!includeInactive)
            query = query.Where(e => e.IsActive);
        if (filter?.SpecialtyId is { } specialtyId)
        {
            var links = _context
                .EmployeeSpecialtyLinks.AsNoTracking()
                .Where(l =>
                    l.TenantId == scope.TenantId
                    && l.CompanyId == scope.CompanyId
                    && l.SpecialtyId == specialtyId
                    && l.IsActive
                );
            query = query.Where(e => links.Any(l => l.EmployeeId == e.Id));
        }
        return await SummariesAsync(scope, query, ct);
    }

    public Task<bool> IsActiveOnAsync(Guid employeeId, DateOnly date, CancellationToken ct = default)
    {
        var scope = Scope();
        return Periods(scope)
            .Where(p => p.EmployeeId == employeeId)
            .AnyAsync(EmployeeTemporalPredicates.PeriodCovers(date), ct);
    }

    public async Task<IReadOnlyList<EmployeeAssignmentSummary>> GetBranchAssignmentsAsync(
        Guid employeeId,
        Guid? branchId = null,
        DateOnly? date = null,
        CancellationToken ct = default
    )
    {
        var scope = Scope();
        var query = Assignments(scope).Where(a => a.EmployeeId == employeeId);
        if (branchId is { } branch)
            query = query.Where(a => a.BranchId == branch);
        if (date is { } on)
            query = query.Where(EmployeeTemporalPredicates.AssignmentCovers(on));
        return await query
            .OrderBy(a => a.StartDate)
            .ThenBy(a => a.Id)
            .Select(a => new EmployeeAssignmentSummary(a.Id, a.BranchId, a.StartDate, a.EndDate))
            .ToListAsync(ct);
    }

    public Task<bool> IsEligibleForBranchAsync(
        Guid employeeId,
        Guid branchId,
        DateOnly date,
        CancellationToken ct = default
    )
    {
        var scope = Scope();
        var periods = Periods(scope).Where(EmployeeTemporalPredicates.PeriodCovers(date));
        var assignments = Assignments(scope)
            .Where(EmployeeTemporalPredicates.AssignmentCovers(date))
            .Where(a => a.BranchId == branchId);
        // Una sola sentencia (EXISTS): ambas condiciones se evalúan sobre la misma instantánea.
        return Employees(scope)
            .AnyAsync(
                e =>
                    e.Id == employeeId
                    && periods.Any(p => p.EmployeeId == e.Id)
                    && assignments.Any(a => a.EmployeeId == e.Id),
                ct
            );
    }

    public async Task<Guid?> GetLinkedUserAsync(Guid employeeId, CancellationToken ct = default)
    {
        var scope = Scope();
        var linked = await Employees(scope)
            .Where(e => e.Id == employeeId)
            .Select(e => e.IdentityUserId)
            .FirstOrDefaultAsync(ct);
        if (linked is not { } userId)
            return null;
        return (await _identity.ProjectAsync(scope.CompanyId, userId, ct))?.Id;
    }

    private async Task<IReadOnlyList<EmployeeSummary>> SummariesAsync(
        (Guid TenantId, Guid CompanyId) scope,
        IQueryable<Employee> employees,
        CancellationToken ct
    )
    {
        var headers = await (
            from e in employees
            join bp in _context.BusinessPartners.AsNoTracking().Where(b => b.TenantId == scope.TenantId)
                on e.BusinessPartnerId equals bp.Id
            orderby bp.Name.LegalName, e.Id
            select new
            {
                e.Id,
                e.CompanyId,
                e.BusinessPartnerId,
                bp.Name.TradeName,
                bp.Name.LegalName,
                e.IdentityUserId,
                e.IsActive,
                e.JobPositionId,
                e.ProfessionId,
            }
        ).ToListAsync(ct);
        if (headers.Count == 0)
            return Array.Empty<EmployeeSummary>();

        var ids = employees.Select(e => e.Id);
        var periods = (
            await Periods(scope)
                .Where(p => ids.Contains(p.EmployeeId))
                .OrderBy(p => p.StartDate)
                .Select(p => new { p.EmployeeId, p.StartDate, p.EndDate })
                .ToListAsync(ct)
        ).ToLookup(p => p.EmployeeId, p => new EmployeePeriodSummary(p.StartDate, p.EndDate));
        var specialties = (
            await _context
                .EmployeeSpecialtyLinks.AsNoTracking()
                .Where(l =>
                    l.TenantId == scope.TenantId
                    && l.CompanyId == scope.CompanyId
                    && l.IsActive
                    && ids.Contains(l.EmployeeId)
                )
                .OrderBy(l => l.SpecialtyId)
                .Select(l => new { l.EmployeeId, l.SpecialtyId })
                .ToListAsync(ct)
        ).ToLookup(l => l.EmployeeId, l => l.SpecialtyId);
        var assignments = (
            await Assignments(scope)
                .Where(a => ids.Contains(a.EmployeeId))
                .OrderBy(a => a.StartDate)
                .ThenBy(a => a.Id)
                .Select(a => new { a.EmployeeId, a.Id, a.BranchId, a.StartDate, a.EndDate })
                .ToListAsync(ct)
        ).ToLookup(
            a => a.EmployeeId,
            a => new EmployeeAssignmentSummary(a.Id, a.BranchId, a.StartDate, a.EndDate)
        );

        // IAM-V1-F1 por lote (consultas fijas, no una por ficha).
        var linkedIds = headers.Where(h => h.IdentityUserId is not null).Select(h => h.IdentityUserId!.Value).ToList();
        var visible = await _identity.FilterVisibleAsync(scope.CompanyId, linkedIds, ct);

        return headers
            .Select(h => new EmployeeSummary(
                h.Id,
                h.CompanyId,
                h.BusinessPartnerId,
                !string.IsNullOrWhiteSpace(h.TradeName) ? h.TradeName : h.LegalName,
                h.IdentityUserId is { } user && visible.Contains(user) ? user : null,
                h.IsActive,
                h.JobPositionId,
                h.ProfessionId,
                specialties[h.Id].ToList(),
                periods[h.Id].ToList(),
                assignments[h.Id].ToList()
            ))
            .ToList();
    }
}
