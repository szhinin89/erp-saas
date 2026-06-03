using System.Linq.Expressions;
using ERP.Domain.Common;
using ERP.Domain.Subscribers.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// Filtros globales ERP: suscriptor + empresa operativa (company scope).
///
/// Semántica fail-closed aplicada en dos capas:
///   1. Subscriber: si FilterSubscriberId == Guid.Empty → 0 filas (nunca actúa como wildcard).
///   2. Company (ICompanyScopedEntity): si no hay company context → 0 filas.
///      Esta semántica difiere de ICompanyOperationalEntity (entidades en migración),
///      donde "sin company context = ver todas las companies del subscriber" es intencional.
///
/// Para consultas legítimas sin filtro usar IPlatformQueryAccessor.Unfiltered().
///
/// Tabla de semánticas:
///   ISubscriberScopedEntity            → fail-closed subscriber
///   ICompanyOperationalEntity          → fail-closed subscriber + pass-all-companies si sin ctx
///   ICompanyScopedEntity + ISubscriber → fail-closed subscriber + fail-closed company
/// </summary>
internal static class EnterpriseQueryFilterConfigurator
{
    public static void Apply(ModelBuilder modelBuilder, ErpDbContext dbContext)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
                continue;

            var clrType = entityType.ClrType;
            if (clrType == typeof(Subscriber))
                continue;

            if (typeof(ICompanyOperationalEntity).IsAssignableFrom(clrType))
            {
                ApplyFilter(modelBuilder, clrType, BuildCompanyOperationalFilter(clrType, dbContext));
                continue;
            }

            if (typeof(ICompanyScopedEntity).IsAssignableFrom(clrType)
                && typeof(ISubscriberScopedEntity).IsAssignableFrom(clrType))
            {
                ApplyFilter(modelBuilder, clrType, BuildCompanyScopedFilter(clrType, dbContext));
                continue;
            }

            if (typeof(ISubscriberScopedEntity).IsAssignableFrom(clrType))
                ApplyFilter(modelBuilder, clrType, BuildSubscriberFilter(clrType, dbContext));
        }
    }

    private static void ApplyFilter(ModelBuilder modelBuilder, Type clrType, LambdaExpression filter)
        => modelBuilder.Entity(clrType).HasQueryFilter(filter);

    private static LambdaExpression BuildSubscriberFilter(Type clrType, ErpDbContext dbContext)
    {
        var parameter     = Expression.Parameter(clrType, "e");
        var dbConstant    = Expression.Constant(dbContext);

        // Fail-closed: si no hay suscriptor en contexto → ninguna fila (body = false).
        // Evita que Guid.Empty actúe como wildcard accidental.
        var currentSubscriber    = Expression.Property(dbConstant, nameof(ErpDbContext.FilterSubscriberId));
        var emptyGuid            = Expression.Constant(Guid.Empty);
        var hasSubscriberContext  = Expression.NotEqual(currentSubscriber, emptyGuid);

        var subscriberProperty = Expression.Property(parameter, nameof(ISubscriberScopedEntity.SubscriberId));
        var subscriberMatch    = Expression.Equal(subscriberProperty, currentSubscriber);

        // WHERE FilterSubscriberId != Guid.Empty AND subscriber_id = FilterSubscriberId
        var body = Expression.AndAlso(hasSubscriberContext, subscriberMatch);
        return Expression.Lambda(body, parameter);
    }

    private static LambdaExpression BuildCompanyOperationalFilter(Type clrType, ErpDbContext dbContext)
        => BuildOperationalScopedFilter(clrType, dbContext);

    private static LambdaExpression BuildCompanyScopedFilter(Type clrType, ErpDbContext dbContext)
        => BuildStrictCompanyScopedFilter(clrType, dbContext);

    /// <summary>
    /// ICompanyOperationalEntity — CompanyId NOT NULL tras migración final.
    /// Semántica FAIL-CLOSED: sin company context → 0 filas.
    /// Idéntica a ICompanyScopedEntity — toda entidad operacional requiere empresa activa.
    /// WHERE subscriber_ok AND company_id = currentCompany
    /// </summary>
    private static LambdaExpression BuildOperationalScopedFilter(Type clrType, ErpDbContext dbContext)
    {
        var parameter  = Expression.Parameter(clrType, "e");
        var dbConstant = Expression.Constant(dbContext);

        // Subscriber: fail-closed
        var currentSubscriber = Expression.Property(dbConstant, nameof(ErpDbContext.FilterSubscriberId));
        var emptyGuid         = Expression.Constant(Guid.Empty);
        var hasSubscriberCtx  = Expression.NotEqual(currentSubscriber, emptyGuid);
        var subscriberProp    = Expression.Property(parameter, nameof(ISubscriberScopedEntity.SubscriberId));
        var subscriberMatch   = Expression.AndAlso(hasSubscriberCtx,
                                  Expression.Equal(subscriberProp, currentSubscriber));

        // Company: fail-closed — CompanyId is now Guid (non-nullable)
        var hasCompanyCtx  = Expression.Property(dbConstant, nameof(ErpDbContext.FilterHasCompanyContext));
        var currentCompany = Expression.Property(dbConstant, nameof(ErpDbContext.FilterCompanyId));
        var companyProp    = Expression.Property(parameter, nameof(ICompanyOperationalEntity.CompanyId));
        var companyMatch   = Expression.AndAlso(hasCompanyCtx,
                               Expression.Equal(companyProp, currentCompany));

        return Expression.Lambda(Expression.AndAlso(subscriberMatch, companyMatch), parameter);
    }

    /// <summary>
    /// ICompanyScopedEntity + ISubscriberScopedEntity — entidades con company_id obligatorio (Guid).
    /// Semántica fail-closed en AMBAS dimensiones:
    ///   sin subscriber context → 0 filas
    ///   sin company context    → 0 filas  (diferencia clave vs BuildOperationalScopedFilter)
    /// WHERE subscriber_ok AND company_ok
    /// Para queries cross-company legítimas (admin, consolidación) usar IPlatformQueryAccessor.Unfiltered().
    /// </summary>
    private static LambdaExpression BuildStrictCompanyScopedFilter(Type clrType, ErpDbContext dbContext)
    {
        var parameter  = Expression.Parameter(clrType, "e");
        var dbConstant = Expression.Constant(dbContext);

        // Subscriber: fail-closed
        var currentSubscriber = Expression.Property(dbConstant, nameof(ErpDbContext.FilterSubscriberId));
        var emptyGuid         = Expression.Constant(Guid.Empty);
        var hasSubscriberCtx  = Expression.NotEqual(currentSubscriber, emptyGuid);
        var subscriberProp    = Expression.Property(parameter, nameof(ISubscriberScopedEntity.SubscriberId));
        var subscriberMatch   = Expression.AndAlso(hasSubscriberCtx,
                                  Expression.Equal(subscriberProp, currentSubscriber));

        // Company: fail-closed — si no hay company context → 0 filas
        var hasCompanyCtx  = Expression.Property(dbConstant, nameof(ErpDbContext.FilterHasCompanyContext));
        var currentCompany = Expression.Property(dbConstant, nameof(ErpDbContext.FilterCompanyId));
        var companyProp    = Expression.Property(parameter, nameof(ICompanyScopedEntity.CompanyId));
        var companyMatch   = Expression.AndAlso(hasCompanyCtx,
                               Expression.Equal(companyProp, currentCompany));

        return Expression.Lambda(Expression.AndAlso(subscriberMatch, companyMatch), parameter);
    }
}
