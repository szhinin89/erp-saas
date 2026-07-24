namespace ERP.Domain.Common;

/// <summary>
/// Entidad ERP con <c>company_id</c> obligatorio. Usar junto con <see cref="ITenantScopedEntity"/>
/// para el query filter global (suscriptor + company scope).
/// </summary>
public interface ICompanyScopedEntity
{
    Guid CompanyId { get; }
}
