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
/// Semántica fail-closed: si FilterSubscriberId == Guid.Empty (no hay contexto de
/// suscriptor activo) el filtro evalúa a false y EF no retorna ninguna fila.
/// Esto protege contra queries accidentales sin contexto de seguridad.
/// Para consultas legítimas sin filtro usar IPlatformQueryAccessor.Unfiltered().
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
        => BuildScopedFilter(clrType, dbContext, nameof(ICompanyOperationalEntity.CompanyId), typeof(Guid?));

    private static LambdaExpression BuildCompanyScopedFilter(Type clrType, ErpDbContext dbContext)
        => BuildScopedFilter(clrType, dbContext, nameof(ICompanyScopedEntity.CompanyId), typeof(Guid));

    private static LambdaExpression BuildScopedFilter(
        Type clrType,
        ErpDbContext dbContext,
        string companyPropertyName,
        Type companyPropertyType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var dbConstant = Expression.Constant(dbContext);

        // Fail-closed: si no hay suscriptor en contexto → ninguna fila.
        var currentSubscriber    = Expression.Property(dbConstant, nameof(ErpDbContext.FilterSubscriberId));
        var emptyGuid            = Expression.Constant(Guid.Empty);
        var hasSubscriberContext  = Expression.NotEqual(currentSubscriber, emptyGuid);

        var subscriberProperty = Expression.Property(parameter, nameof(ISubscriberScopedEntity.SubscriberId));
        var subscriberMatch    = Expression.AndAlso(
            hasSubscriberContext,
            Expression.Equal(subscriberProperty, currentSubscriber));

        var companyProperty = Expression.Property(parameter, companyPropertyName);
        var hasCompanyContext = Expression.Property(dbConstant, nameof(ErpDbContext.FilterHasCompanyContext));
        var currentCompanyId = Expression.Property(dbConstant, nameof(ErpDbContext.FilterCompanyId));

        Expression currentComparable = companyPropertyType == typeof(Guid?)
            ? Expression.Convert(currentCompanyId, typeof(Guid?))
            : currentCompanyId;

        var companyMatch = Expression.Equal(companyProperty, currentComparable);
        var noCompanyContext = Expression.Not(hasCompanyContext);
        var companyScope = Expression.OrElse(noCompanyContext, companyMatch);

        var body = Expression.AndAlso(subscriberMatch, companyScope);
        return Expression.Lambda(body, parameter);
    }
}
