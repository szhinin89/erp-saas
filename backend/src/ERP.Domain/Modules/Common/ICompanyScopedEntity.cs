namespace ERP.Domain.Common;

/// <summary>
/// Marca entidades del dominio ERP cuyo aislamiento operativo es por empresa (company_id).
/// Fase 6: query filter por <see cref="ICurrentCompany"/>.
/// </summary>
public interface ICompanyScopedEntity
{
    Guid CompanyId { get; }
}
