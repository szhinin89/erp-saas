using ERP.Domain.Modules.Employees.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Employees;

/// <summary>
/// Bloqueo de lectura de la membresía (EMP-E6, autorizado): tenant y empresa como predicado explícito, sin
/// <c>IgnoreQueryFilters</c> y sin escribir nada de Access. Tablas y columnas son constantes.
/// </summary>
public sealed class EmployeeUserReader : IEmployeeUserReader
{
    private readonly ErpDbContext _context;

    public EmployeeUserReader(ErpDbContext context) => _context = context;

    public async Task<bool> TryLockMembershipAsync(
        Guid tenantId,
        Guid companyId,
        Guid identityUserId,
        CancellationToken ct = default
    )
    {
        if (!_context.Database.IsRelational())
            throw new NotSupportedException("El bloqueo de membresía requiere una base relacional.");
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Invariante violada: el bloqueo de la membresía requiere una transacción activa."
            );

        var locked = await _context
            .Database.SqlQueryRaw<Guid>(
                "SELECT m.id AS \"Value\" FROM company_user_memberships m "
                    + "JOIN company c ON c.id = m.company_id "
                    + "WHERE m.company_id = {0} AND m.identity_user_id = {1} AND c.tenant_id = {2} "
                    + "FOR SHARE OF m",
                companyId,
                identityUserId,
                tenantId
            )
            .ToListAsync(ct);
        return locked.Count == 1;
    }
}
