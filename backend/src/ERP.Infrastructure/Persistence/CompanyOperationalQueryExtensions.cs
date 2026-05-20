using ERP.Application.Common;
using ERP.Domain.Common;

namespace ERP.Infrastructure.Persistence;

/// <summary>Filtros ERP por suscriptor + empresa operativa (oleada 1 inventario/productos).</summary>
internal static class CompanyOperationalQueryExtensions
{
    public static IQueryable<T> ForOperationalScope<T>(
        this IQueryable<T> query,
        Guid subscriberId,
        ICurrentCompany company)
        where T : class, ISubscriberScopedEntity, ICompanyOperationalEntity
    {
        query = query.Where(e => e.SubscriberId == subscriberId);
        if (company.HasCompanyContext)
            query = query.Where(e => e.CompanyId == company.CompanyId);
        return query;
    }
}
