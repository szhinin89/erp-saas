namespace ERP.Application.Common;

/// <summary>
/// Punto único para <c>IgnoreQueryFilters</c> en código de aplicación/repositorios.
/// El caller debe filtrar por <c>SubscriberId</c> cuando <see cref="PlatformQueryReason.SubscriberScopedExplicit"/>.
/// </summary>
public interface IPlatformQueryAccessor
{
    IQueryable<TEntity> Unfiltered<TEntity>(IQueryable<TEntity> query, PlatformQueryReason reason)
        where TEntity : class;
}
