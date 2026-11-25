using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Employees.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Employees;

/// <summary>
/// Lectura de solo lectura del BP para el alta (P-05, D-5). Sin seguimiento (siempre fresca tras el
/// lock) y con tenant explícito: un BP de otro tenant es indistinguible de uno inexistente.
/// </summary>
public sealed class EmployeeBusinessPartnerReader : IEmployeeBusinessPartnerReader
{
    private readonly ErpDbContext _context;

    public EmployeeBusinessPartnerReader(ErpDbContext context) => _context = context;

    public async Task<EmployeeBpEligibility?> GetEligibilityAsync(
        Guid tenantId,
        Guid businessPartnerId,
        CancellationToken ct = default
    )
    {
        var bp = await _context
            .BusinessPartners.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.Id == businessPartnerId)
            .Select(b => new
            {
                b.Id,
                b.IsActive,
                IdentificationType = b.Identification.Type,
            })
            .FirstOrDefaultAsync(ct);
        if (bp is null)
            return null;

        var hasRole = await _context
            .BusinessPartnerRoles.AsNoTracking()
            .AnyAsync(
                r =>
                    r.TenantId == tenantId
                    && r.BusinessPartnerId == businessPartnerId
                    && r.RoleType == RoleType.Employee
                    && r.IsActive,
                ct
            );
        return new EmployeeBpEligibility(bp.Id, bp.IsActive, hasRole, bp.IdentificationType);
    }
}
