using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Enums;
using ERP.Domain.Modules.Employees.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Employees;

/// <summary>
/// Persistencia de los tres catálogos laborales: una tabla por catálogo, un mismo comportamiento.
/// Cada consulta aplica tenant y empresa como predicado propio (observación B).
/// </summary>
public sealed class EmployeeCatalogRepository : IEmployeeCatalogRepository
{
    private readonly ErpDbContext _context;

    public EmployeeCatalogRepository(ErpDbContext context) => _context = context;

    private ICatalogOps OpsFor(EmployeeCatalogType type) =>
        type switch
        {
            EmployeeCatalogType.JobPosition => new Ops<JobPosition>(_context, "employee_job_positions"),
            EmployeeCatalogType.Profession => new Ops<Profession>(_context, "employee_professions"),
            EmployeeCatalogType.Specialty => new Ops<Specialty>(_context, "employee_specialties"),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Catálogo desconocido."),
        };

    public Task<EmployeeCatalogEntry?> GetByIdAsync(
        EmployeeCatalogType type,
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    ) => OpsFor(type).GetAsync(tenantId, companyId, id, ct);

    public Task<(IReadOnlyList<EmployeeCatalogRow> Items, int Total)> ListAsync(
        EmployeeCatalogType type,
        Guid tenantId,
        Guid companyId,
        bool? isActive,
        string? search,
        int skip,
        int take,
        CancellationToken ct = default
    ) => OpsFor(type).ListAsync(tenantId, companyId, isActive, search, skip, take, ct);

    public Task<bool> ExistsByNormalizedNameAsync(
        EmployeeCatalogType type,
        Guid tenantId,
        Guid companyId,
        string normalizedName,
        Guid? excludeId,
        CancellationToken ct = default
    ) => OpsFor(type).ExistsByNormalizedNameAsync(tenantId, companyId, normalizedName, excludeId, ct);

    public async Task AddAsync(EmployeeCatalogEntry entry, CancellationToken ct = default) =>
        await _context.AddAsync(entry, ct);

    public Task<IReadOnlyDictionary<Guid, bool>> ResolveForAssignmentAsync(
        EmployeeCatalogType type,
        Guid tenantId,
        Guid companyId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    ) => OpsFor(type).ResolveForAssignmentAsync(tenantId, companyId, ids, ct);

    public uint GetRowVersion(EmployeeCatalogEntry entry) =>
        _context.Entry(entry).Property<uint>("xmin").CurrentValue;

    public void SetExpectedRowVersion(EmployeeCatalogEntry entry, uint rowVersion) =>
        _context.Entry(entry).Property<uint>("xmin").OriginalValue = rowVersion;

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

    private interface ICatalogOps
    {
        Task<EmployeeCatalogEntry?> GetAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct);

        Task<(IReadOnlyList<EmployeeCatalogRow> Items, int Total)> ListAsync(
            Guid tenantId, Guid companyId, bool? isActive, string? search, int skip, int take, CancellationToken ct);

        Task<bool> ExistsByNormalizedNameAsync(
            Guid tenantId, Guid companyId, string normalizedName, Guid? excludeId, CancellationToken ct);

        Task<IReadOnlyDictionary<Guid, bool>> ResolveForAssignmentAsync(
            Guid tenantId, Guid companyId, IReadOnlyCollection<Guid> ids, CancellationToken ct);
    }

    private sealed class Ops<T> : ICatalogOps
        where T : EmployeeCatalogEntry
    {
        private readonly ErpDbContext _context;
        private readonly string _table;

        public Ops(ErpDbContext context, string table) => (_context, _table) = (context, table);

        private IQueryable<T> Scoped(Guid tenantId, Guid companyId) =>
            _context.Set<T>().Where(e => e.TenantId == tenantId && e.CompanyId == companyId);

        public async Task<EmployeeCatalogEntry?> GetAsync(
            Guid tenantId, Guid companyId, Guid id, CancellationToken ct) =>
            await Scoped(tenantId, companyId).FirstOrDefaultAsync(e => e.Id == id, ct);

        public async Task<(IReadOnlyList<EmployeeCatalogRow> Items, int Total)> ListAsync(
            Guid tenantId, Guid companyId, bool? isActive, string? search, int skip, int take, CancellationToken ct)
        {
            var query = Scoped(tenantId, companyId).AsNoTracking();
            if (isActive is { } active)
                query = query.Where(e => e.IsActive == active);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(e => EF.Functions.ILike(e.Name, pattern));
            }

            var total = await query.CountAsync(ct);
            var rows = await query
                .OrderBy(e => e.NormalizedName)
                .ThenBy(e => e.Id)
                .Skip(skip)
                .Take(take)
                .Select(e => new { Entry = e, RowVersion = EF.Property<uint>(e, "xmin") })
                .ToListAsync(ct);
            return (rows.Select(r => new EmployeeCatalogRow(r.Entry, r.RowVersion)).ToList(), total);
        }

        public Task<bool> ExistsByNormalizedNameAsync(
            Guid tenantId, Guid companyId, string normalizedName, Guid? excludeId, CancellationToken ct) =>
            Scoped(tenantId, companyId)
                .AsNoTracking()
                .AnyAsync(
                    e => e.NormalizedName == normalizedName && (excludeId == null || e.Id != excludeId),
                    ct
                );

        public async Task<IReadOnlyDictionary<Guid, bool>> ResolveForAssignmentAsync(
            Guid tenantId, Guid companyId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        {
            if (_context.Database.IsRelational() && _context.Database.CurrentTransaction is null)
                throw new InvalidOperationException(
                    "La asignación de catálogos requiere una transacción activa."
                );

            var idList = ids.Distinct().ToArray();
            var status = await Scoped(tenantId, companyId)
                .AsNoTracking()
                .Where(e => idList.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.IsActive, ct);

            var active = status.Where(kv => kv.Value).Select(kv => kv.Key).ToArray();
            if (active.Length == 0 || !_context.Database.IsRelational())
                return status;

            // Bloquea los activos hasta el fin de la transacción: una inactivación concurrente
            // (UPDATE de la fila) espera, y una ya confirmada se re-evalúa aquí como inactiva
            // (READ COMMITTED). El nombre de tabla es una constante del repositorio.
            var stillActive = await _context
                .Database.SqlQueryRaw<Guid>(
                    $"SELECT id AS \"Value\" FROM {_table} WHERE id = ANY({{0}}) AND tenant_id = {{1}} AND company_id = {{2}} AND is_active FOR SHARE",
                    active,
                    tenantId,
                    companyId
                )
                .ToListAsync(ct);
            var locked = stillActive.ToHashSet();
            foreach (var id in active.Where(id => !locked.Contains(id)))
                status[id] = false;
            return status;
        }
    }
}
