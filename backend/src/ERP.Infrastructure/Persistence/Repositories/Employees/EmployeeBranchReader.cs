using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Employees.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Employees;

/// <summary>
/// Lectura de sucursales para las asignaciones (EMP-E5, observación B): tenant y empresa como predicado
/// propio, sin seguimiento y sin modificar el módulo de sucursales. La sucursal activa queda bloqueada
/// <c>FOR SHARE</c> hasta el fin de la transacción (mismo patrón que los catálogos de E4).
/// </summary>
public sealed class EmployeeBranchReader : IEmployeeBranchReader
{
    private readonly ErpDbContext _context;

    public EmployeeBranchReader(ErpDbContext context) => _context = context;

    public async Task<bool> TryLockActiveAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CancellationToken ct = default
    )
    {
        if (_context.Database.IsRelational() && _context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("La validación de la sucursal requiere una transacción activa.");

        var active = await _context
            .Set<Branch>()
            .AsNoTracking()
            .AnyAsync(b => b.TenantId == tenantId && b.CompanyId == companyId && b.Id == branchId && b.IsActive, ct);
        if (!active || !_context.Database.IsRelational())
            return active;

        // Bloquea la fila hasta el fin de la transacción: una desactivación concurrente (UPDATE) espera, y
        // una ya confirmada se re-evalúa aquí como inactiva (READ COMMITTED). Tabla y columnas son constantes.
        var locked = await _context
            .Database.SqlQueryRaw<Guid>(
                "SELECT id AS \"Value\" FROM branches WHERE id = {0} AND tenant_id = {1} AND company_id = {2} AND is_active FOR SHARE",
                branchId,
                tenantId,
                companyId
            )
            .ToListAsync(ct);
        return locked.Count == 1;
    }
}
