using ERP.Application.Modules.Employees.DTOs;
using ERP.Domain.Branches.Entities;
using ERP.Application.Modules.Employees.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Employees;

/// <summary>
/// Lecturas de Empleados (D-5, P-05): sin seguimiento, con tenant y empresa como predicado propio y
/// una proyección mínima del BP. Ficha: 3 consultas fijas (cabecera, períodos, especialidades);
/// lista: una página + un conteo. Sin N+1.
/// </summary>
public sealed class EmployeeQueries : IEmployeeQueries
{
    private readonly ErpDbContext _context;
    private readonly IEmployeeIdentityProjector _identity;

    public EmployeeQueries(ErpDbContext context, IEmployeeIdentityProjector identity) =>
        (_context, _identity) = (context, identity);

    // Clase con inicializador de miembros (no record posicional): EF solo compone filtros
    // posteriores (Where/OrderBy) sobre proyecciones de este tipo.
    private sealed class Row
    {
        public Guid Id { get; init; }
        public Guid CompanyId { get; init; }
        public bool IsActive { get; init; }
        public uint RowVersion { get; init; }
        public Guid BpId { get; init; }
        public string? TradeName { get; init; }
        public string LegalName { get; init; } = null!;
        public string IdentificationNumber { get; init; } = null!;
        public string IdentificationType { get; init; } = null!;
        public Guid? JobPositionId { get; init; }
        public string? JobPositionName { get; init; }
        public bool? JobPositionActive { get; init; }
        public Guid? ProfessionId { get; init; }
        public Guid? IdentityUserId { get; init; }
        public string? ProfessionName { get; init; }
        public bool? ProfessionActive { get; init; }
    }

    private IQueryable<Row> Rows(Guid tenantId, Guid companyId) =>
        from e in _context.Employees.AsNoTracking()
        where e.TenantId == tenantId && e.CompanyId == companyId
        join bp in _context.BusinessPartners.AsNoTracking().Where(b => b.TenantId == tenantId)
            on e.BusinessPartnerId equals bp.Id
        join jp in _context
            .EmployeeJobPositions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            on e.JobPositionId equals (Guid?)jp.Id into jps
        from jp in jps.DefaultIfEmpty()
        join pr in _context
            .EmployeeProfessions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            on e.ProfessionId equals (Guid?)pr.Id into prs
        from pr in prs.DefaultIfEmpty()
        select new Row
        {
            Id = e.Id,
            CompanyId = e.CompanyId,
            IsActive = e.IsActive,
            RowVersion = EF.Property<uint>(e, "xmin"),
            BpId = bp.Id,
            TradeName = bp.Name.TradeName,
            LegalName = bp.Name.LegalName,
            IdentificationNumber = bp.Identification.Number,
            IdentificationType = bp.Identification.Type,
            JobPositionId = e.JobPositionId,
            JobPositionName = jp.Name,
            JobPositionActive = (bool?)jp.IsActive,
            ProfessionId = e.ProfessionId,
            IdentityUserId = e.IdentityUserId,
            ProfessionName = pr.Name,
            ProfessionActive = (bool?)pr.IsActive,
        };

    private static EmployeeBusinessPartnerDto Bp(Row r) =>
        new(
            r.BpId,
            !string.IsNullOrWhiteSpace(r.TradeName) ? r.TradeName : r.LegalName,
            r.IdentificationType,
            r.IdentificationNumber
        );

    private static EmployeeCatalogRefDto? Ref(Guid? id, string? name, bool? active) =>
        id is { } value && name is not null ? new(value, name, active ?? false) : null;

    // Una sola consulta con join a la sucursal (nombre y código), con tenant y empresa explícitos.
    private IQueryable<EmployeeBranchAssignmentDto> AssignmentRows(Guid tenantId, Guid companyId) =>
        from a in _context.EmployeeBranchAssignments.AsNoTracking()
        where a.TenantId == tenantId && a.CompanyId == companyId
        join b in _context.Set<Branch>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            on a.BranchId equals b.Id
        select new EmployeeBranchAssignmentDto(
            a.Id, b.Id, b.Name, b.Code, a.StartDate, a.EndDate, EF.Property<uint>(a, "xmin"));

    private async Task<IReadOnlyList<EmployeeBranchAssignmentDto>> AssignmentsAsync(
        Guid tenantId, Guid companyId, Guid employeeId, CancellationToken ct) =>
        await (
            from a in _context.EmployeeBranchAssignments.AsNoTracking()
            where a.TenantId == tenantId && a.CompanyId == companyId && a.EmployeeId == employeeId
            join b in _context.Set<Branch>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
                on a.BranchId equals b.Id
            orderby a.StartDate, b.Name, a.Id
            select new EmployeeBranchAssignmentDto(
                a.Id, b.Id, b.Name, b.Code, a.StartDate, a.EndDate, EF.Property<uint>(a, "xmin"))
        ).ToListAsync(ct);

    public async Task<IReadOnlyList<EmployeeBranchAssignmentDto>?> ListAssignmentsAsync(
        Guid tenantId, Guid companyId, Guid employeeId, CancellationToken ct = default)
    {
        var exists = await _context.Employees.AsNoTracking()
            .AnyAsync(e => e.TenantId == tenantId && e.CompanyId == companyId && e.Id == employeeId, ct);
        return exists ? await AssignmentsAsync(tenantId, companyId, employeeId, ct) : null;
    }

    public async Task<EmployeeBranchAssignmentDto?> GetAssignmentAsync(
        Guid tenantId, Guid companyId, Guid employeeId, Guid assignmentId, CancellationToken ct = default) =>
        await (
            from a in _context.EmployeeBranchAssignments.AsNoTracking()
            where a.TenantId == tenantId && a.CompanyId == companyId && a.EmployeeId == employeeId && a.Id == assignmentId
            join b in _context.Set<Branch>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
                on a.BranchId equals b.Id
            select new EmployeeBranchAssignmentDto(
                a.Id, b.Id, b.Name, b.Code, a.StartDate, a.EndDate, EF.Property<uint>(a, "xmin"))
        ).FirstOrDefaultAsync(ct);

    public async Task<EmployeeDto?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        CancellationToken ct = default
    )
    {
        var row = await Rows(tenantId, companyId).FirstOrDefaultAsync(r => r.Id == employeeId, ct);
        if (row is null)
            return null;

        var periods = await _context
            .EmployeeEmploymentPeriods.AsNoTracking()
            .Where(p =>
                p.TenantId == tenantId && p.CompanyId == companyId && p.EmployeeId == employeeId
            )
            .OrderBy(p => p.StartDate)
            .Select(p => new EmployeePeriodDto(p.Id, p.StartDate, p.EndDate, p.TerminationReason))
            .ToListAsync(ct);

        var specialties = await (
            from l in _context.EmployeeSpecialtyLinks.AsNoTracking()
            where
                l.TenantId == tenantId
                && l.CompanyId == companyId
                && l.EmployeeId == employeeId
                && l.IsActive
            join s in _context.EmployeeSpecialties.AsNoTracking()
                on l.SpecialtyId equals s.Id
            where s.TenantId == tenantId && s.CompanyId == companyId
            orderby s.NormalizedName
            select new EmployeeCatalogRefDto(s.Id, s.Name, s.IsActive)
        ).ToListAsync(ct);

        return new EmployeeDto(
            row.Id,
            row.CompanyId,
            Bp(row),
            row.IsActive,
            Ref(row.JobPositionId, row.JobPositionName, row.JobPositionActive),
            Ref(row.ProfessionId, row.ProfessionName, row.ProfessionActive),
            specialties,
            row.IdentityUserId is { } linked
                ? await _identity.ProjectAsync(companyId, linked, ct)
                : null,
            periods,
            await AssignmentsAsync(tenantId, companyId, employeeId, ct),
            row.RowVersion,
            Array.Empty<string>() // solo la respuesta de la baja agrega ACTIVE_MEMBERSHIP
        );
    }

    public async Task<(IReadOnlyList<EmployeeListItemDto> Items, int Total)> ListAsync(
        Guid tenantId,
        Guid companyId,
        EmployeeListFilter filter,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        var query = Rows(tenantId, companyId);
        if (filter.IsActive is { } active)
            query = query.Where(r => r.IsActive == active);
        if (filter.JobPositionId is { } jobId)
            query = query.Where(r => r.JobPositionId == jobId);
        if (filter.ProfessionId is { } professionId)
            query = query.Where(r => r.ProfessionId == professionId);
        if (filter.SpecialtyId is { } specialtyId)
            query = query.Where(r =>
                _context.EmployeeSpecialtyLinks.Any(l =>
                    l.TenantId == tenantId
                    && l.CompanyId == companyId
                    && l.EmployeeId == r.Id
                    && l.SpecialtyId == specialtyId
                    && l.IsActive
                )
            );
        if (filter.BranchId is { } branchId)
        {
            // D-E5-4: asignación vigente en la fecha de referencia (bordes inclusivos).
            var asOf = filter.AsOf ?? throw new InvalidOperationException("Fecha de referencia requerida.");
            var current = _context
                .EmployeeBranchAssignments.Where(EmployeeTemporalPredicates.AssignmentCovers(asOf))
                .Where(a => a.TenantId == tenantId && a.CompanyId == companyId && a.BranchId == branchId);
            query = query.Where(r => current.Any(a => a.EmployeeId == r.Id));
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim()}%";
            query = query.Where(r =>
                EF.Functions.ILike(r.LegalName, pattern)
                || (r.TradeName != null && EF.Functions.ILike(r.TradeName, pattern))
                || EF.Functions.ILike(r.IdentificationNumber, pattern)
            );
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(r => r.LegalName)
            .ThenBy(r => r.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (
            rows.Select(r => new EmployeeListItemDto(
                    r.Id,
                    r.CompanyId,
                    Bp(r),
                    r.IsActive,
                    Ref(r.JobPositionId, r.JobPositionName, r.JobPositionActive),
                    Ref(r.ProfessionId, r.ProfessionName, r.ProfessionActive),
                    r.RowVersion
                ))
                .ToList(),
            total
        );
    }
}
