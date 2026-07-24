using ERP.Domain.Audit;

namespace ERP.Application.Audit;

/// <summary>
/// Resuelve una sola vez por request los datos comunes de auditoría (tenant, empresa,
/// usuario, correlación) para que los event handlers de dominio no vuelvan a inyectar
/// <c>ICurrentTenant</c>/<c>ICurrentCompany</c>/<c>ICurrentUser</c> por separado.
/// </summary>
public interface IAuditContext
{
    Guid TenantId { get; }
    Guid CompanyId { get; }

    /// <summary>Actor común (tenant/usuario/correlación) listo para pasar a un factory de auditoría de dominio.</summary>
    AuditActor Actor { get; }
}
