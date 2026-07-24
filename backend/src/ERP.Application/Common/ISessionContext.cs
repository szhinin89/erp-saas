namespace ERP.Application.Common;

/// <summary>
/// Contexto de sesión para RLS PostgreSQL (<c>app.tenant_id</c>, <c>app.company_id</c>).
/// </summary>
public interface ISessionContext
{
    Guid TenantId { get; }
    Guid CompanyId { get; }
    bool HasTenantContext { get; }
    bool HasCompanyContext { get; }
}
