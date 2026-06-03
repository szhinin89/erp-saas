namespace ERP.Domain.Common;

/// <summary>
/// Entidad ERP operativa con aislamiento por empresa.
/// CompanyId es OBLIGATORIO y NOT NULL — no existen entidades operacionales sin empresa.
/// Query filter: fail-closed en ambas dimensiones (subscriber + company).
/// </summary>
public interface ICompanyOperationalEntity : ISubscriberScopedEntity
{
    Guid CompanyId { get; }
}
