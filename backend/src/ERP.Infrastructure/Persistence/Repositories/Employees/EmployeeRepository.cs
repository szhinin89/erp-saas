using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Employees;

/// <summary>
/// Persistencia de la ficha (EMP-E4). Cada consulta aplica tenant y empresa como predicado propio,
/// sin apoyarse solo en el filtro global de EF (observación B).
/// </summary>
public sealed class EmployeeRepository : IEmployeeRepository
{
    private const string RowVersionProperty = "xmin";

    private readonly ErpDbContext _context;

    public EmployeeRepository(ErpDbContext context) => _context = context;

    public Task AcquireEmployeeLockAsync(Guid employeeId, CancellationToken ct = default) =>
        EmployeesAdvisoryLocks.AcquireEmployeeAsync(_context, employeeId, ct);

    public async Task<Employee?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    )
    {
        var wasTracked = _context.ChangeTracker.Entries<Employee>().Any(x => x.Entity.Id == id);
        var employee = await _context.Employees.FirstOrDefaultAsync(
            e => e.TenantId == tenantId && e.CompanyId == companyId && e.Id == id,
            ct
        );
        // Tras el lock el estado debe ser el confirmado: una instancia ya rastreada se refresca.
        if (employee is not null && wasTracked)
            await _context.Entry(employee).ReloadAsync(ct);
        return employee;
    }

    public Task<bool> ExistsForBusinessPartnerAsync(
        Guid tenantId,
        Guid companyId,
        Guid businessPartnerId,
        CancellationToken ct = default
    ) =>
        _context
            .Employees.AsNoTracking()
            .AnyAsync(
                e =>
                    e.TenantId == tenantId
                    && e.CompanyId == companyId
                    && e.BusinessPartnerId == businessPartnerId,
                ct
            );

    public async Task<Employee?> GetWithPeriodsAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    )
    {
        var wasTracked = _context.ChangeTracker.Entries<Employee>().Any(x => x.Entity.Id == id);
        var employee = await _context
            .Employees.Include(e => e.Periods)
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.CompanyId == companyId && e.Id == id, ct);
        // Tras el lock el estado debe ser el confirmado: una instancia ya rastreada se refresca.
        if (employee is not null && wasTracked)
        {
            await _context.Entry(employee).ReloadAsync(ct);
            foreach (var period in employee.Periods)
                await _context.Entry(period).ReloadAsync(ct);
        }
        return employee;
    }

    public uint GetRowVersion(Employee employee) =>
        _context.Entry(employee).Property<uint>(RowVersionProperty).CurrentValue;

    public async Task<IReadOnlyList<EmployeeBranchAssignment>> GetAssignmentsAsync(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        CancellationToken ct = default
    )
    {
        var tracked = _context
            .ChangeTracker.Entries<EmployeeBranchAssignment>()
            .Where(x => x.Entity.EmployeeId == employeeId)
            .Select(x => x.Entity.Id)
            .ToHashSet();
        var rows = await _context
            .EmployeeBranchAssignments.Where(a =>
                a.TenantId == tenantId && a.CompanyId == companyId && a.EmployeeId == employeeId
            )
            .ToListAsync(ct);
        // Tras el lock el estado debe ser el confirmado: solo las ya rastreadas se refrescan.
        foreach (var row in rows.Where(r => tracked.Contains(r.Id)))
            await _context.Entry(row).ReloadAsync(ct);
        return rows;
    }

    public async Task<EmployeeBranchAssignment?> GetAssignmentAsync(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        Guid assignmentId,
        CancellationToken ct = default
    )
    {
        var wasTracked = _context
            .ChangeTracker.Entries<EmployeeBranchAssignment>()
            .Any(x => x.Entity.Id == assignmentId);
        var row = await _context.EmployeeBranchAssignments.FirstOrDefaultAsync(
            a =>
                a.TenantId == tenantId
                && a.CompanyId == companyId
                && a.EmployeeId == employeeId
                && a.Id == assignmentId,
            ct
        );
        if (row is not null && wasTracked)
            await _context.Entry(row).ReloadAsync(ct);
        return row;
    }

    public async Task AddAssignmentAsync(EmployeeBranchAssignment assignment, CancellationToken ct = default) =>
        await _context.EmployeeBranchAssignments.AddAsync(assignment, ct);

    // Estado Added explícito: una entidad con Guid asignado por el cliente y descubierta por navegación en un
    // padre ya rastreado no se clasifica como nueva y se intentaría actualizar.
    public async Task AddPeriodAsync(EmployeeEmploymentPeriod period, CancellationToken ct = default) =>
        await _context.EmployeeEmploymentPeriods.AddAsync(period, ct);

    public uint GetAssignmentRowVersion(EmployeeBranchAssignment assignment) =>
        _context.Entry(assignment).Property<uint>(RowVersionProperty).CurrentValue;

    public void SetExpectedAssignmentRowVersion(EmployeeBranchAssignment assignment, uint rowVersion) =>
        _context.Entry(assignment).Property<uint>(RowVersionProperty).OriginalValue = rowVersion;

    public Task<bool> IsUserLinkedToOtherEmployeeAsync(
        Guid tenantId,
        Guid companyId,
        Guid identityUserId,
        Guid employeeId,
        CancellationToken ct = default
    ) =>
        _context
            .Employees.AsNoTracking()
            .AnyAsync(
                e =>
                    e.TenantId == tenantId
                    && e.CompanyId == companyId
                    && e.IdentityUserId == identityUserId
                    && e.Id != employeeId,
                ct
            );

    public async Task AddAsync(Employee employee, CancellationToken ct = default) =>
        await _context.Employees.AddAsync(employee, ct);

    public void SetExpectedRowVersion(Employee employee, uint rowVersion) =>
        _context.Entry(employee).Property<uint>(RowVersionProperty).OriginalValue = rowVersion;

    public async Task<IReadOnlyList<EmployeeSpecialty>> GetSpecialtyLinksAsync(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        CancellationToken ct = default
    ) =>
        await _context
            .EmployeeSpecialtyLinks.Where(l =>
                l.TenantId == tenantId && l.CompanyId == companyId && l.EmployeeId == employeeId
            )
            .ToListAsync(ct);

    public async Task AddSpecialtyLinkAsync(EmployeeSpecialty link, CancellationToken ct = default) =>
        await _context.EmployeeSpecialtyLinks.AddAsync(link, ct);

    public async Task<bool> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
