using ERP.Application.Common;
using ERP.Domain.Common;

namespace ERP.Infrastructure.Persistence;

/// <summary>Filtros ERP por suscriptor + empresa operativa (oleada 1 inventario/productos).</summary>
internal static class CompanyOperationalQueryExtensions
{
    public static IQueryable<T> ForOperationalScope<T>(
        this IQueryable<T> query,
        Guid tenantId,
        ICurrentCompany company)
        where T : class, ITenantScopedEntity, ICompanyOperationalEntity
    {
        query = query.Where(e => e.TenantId == tenantId);
        if (company.HasCompanyContext)
            query = query.Where(e => e.CompanyId == company.CompanyId);
        return query;
    }
}
