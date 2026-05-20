namespace ERP.Domain.Common;

/// <summary>
/// Entidad ERP en migración a aislamiento por <c>company_id</c>.
/// Fase 6 oleada 1+: Product, Warehouse, StockMovement, CurrentStock.
/// Mientras <see cref="CompanyId"/> sea null, el filtro sigue siendo <c>subscriber_id</c>.
/// </summary>
public interface ICompanyOperationalEntity : ISubscriberScopedEntity
{
    Guid? CompanyId { get; }
}
