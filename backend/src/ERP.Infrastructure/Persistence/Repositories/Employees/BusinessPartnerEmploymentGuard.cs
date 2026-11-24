using ERP.Domain.MasterData.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Employees;

/// <summary>
/// Implementación (EMP-E3, ADR-050 §12) de <see cref="IBusinessPartnerEmploymentGuard"/>.
///
/// La consulta usa <c>AsPlatformQuery()</c> porque el filtro global de <c>Employee</c> restringe a la
/// empresa activa y la regla abarca todas las empresas del tenant. Ese bypass no aplica tenant:
/// el filtro explícito <c>TenantId == tenantId</c> es parte del contrato y está probado
/// (T-06). Devuelve solo un booleano.
///
/// Aislamiento: la consulta debe ejecutarse DESPUÉS del lock y con READ COMMITTED (por defecto de
/// PostgreSQL y de <c>UnitOfWork</c>): cada sentencia ve lo confirmado por la transacción competidora
/// que terminó antes de que se adquiriera el lock. No usar REPEATABLE READ/SERIALIZABLE aquí.
/// </summary>
public sealed class BusinessPartnerEmploymentGuard : IBusinessPartnerEmploymentGuard
{
    private readonly ErpDbContext _context;

    public BusinessPartnerEmploymentGuard(ErpDbContext context) => _context = context;

    public Task AcquireBpEmploymentLockAsync(
        Guid tenantId,
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    )
    {
        EnsureIds(tenantId, businessPartnerId);
        return EmployeesAdvisoryLocks.AcquireBpEmploymentAsync(
            _context,
            tenantId,
            businessPartnerId,
            cancellationToken
        );
    }

    public async Task<bool> HasActiveEmploymentAsync(
        Guid tenantId,
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    )
    {
        EnsureIds(tenantId, businessPartnerId);
        return await _context
            .Employees.AsPlatformQuery()
            .AnyAsync(
                e =>
                    e.TenantId == tenantId && e.BusinessPartnerId == businessPartnerId && e.IsActive,
                cancellationToken
            );
    }

    // Fail-closed: sin tenant autenticado no hay consulta sin filtro de tenant.
    private static void EnsureIds(Guid tenantId, Guid businessPartnerId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId requerido.", nameof(tenantId));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException("businessPartnerId requerido.", nameof(businessPartnerId));
    }
}
