using System.Linq.Expressions;
using ERP.Domain.Common;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// Filtros globales ERP: tenant + empresa operativa (company scope).
///
/// Semántica fail-closed aplicada en dos capas:
///   1. Tenant: si FilterTenantId == Guid.Empty → 0 filas (nunca actúa como wildcard).
///   2. Company: si FilterHasCompanyContext == false → 0 filas (fail-closed en ambas dimensiones).
///
/// Tabla de semánticas:
///   ITenantScopedEntity                          → fail-closed tenant only
///   ICompanyOperationalEntity                    → fail-closed tenant + fail-closed company
///   ICompanyScopedEntity + ITenantScopedEntity   → fail-closed tenant + fail-closed company
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

            if (typeof(ICompanyOperationalEntity).IsAssignableFrom(clrType))
            {
                ApplyFilter(modelBuilder, clrType, BuildCompanyOperationalFilter(clrType, dbContext));
                continue;
            }

            if (typeof(ICompanyScopedEntity).IsAssignableFrom(clrType)
                && typeof(ITenantScopedEntity).IsAssignableFrom(clrType))
            {
                ApplyFilter(modelBuilder, clrType, BuildCompanyScopedFilter(clrType, dbContext));
                continue;
            }

            if (typeof(ITenantScopedEntity).IsAssignableFrom(clrType))
                ApplyFilter(modelBuilder, clrType, BuildTenantFilter(clrType, dbContext));
        }
    }

    private static void ApplyFilter(ModelBuilder modelBuilder, Type clrType, LambdaExpression filter)
        => modelBuilder.Entity(clrType).HasQueryFilter(filter);

    private static LambdaExpression BuildTenantFilter(Type clrType, ErpDbContext dbContext)
    {
        var parameter  = Expression.Parameter(clrType, "e");
        var dbConstant = Expression.Constant(dbContext);

        var currentTenant    = Expression.Property(dbConstant, nameof(ErpDbContext.FilterTenantId));
        var emptyGuid        = Expression.Constant(Guid.Empty);
        var hasTenantContext = Expression.NotEqual(currentTenant, emptyGuid);

        var tenantProperty = Expression.Property(parameter, nameof(ITenantScopedEntity.TenantId));
        var tenantMatch    = Expression.Equal(tenantProperty, currentTenant);

        var body = Expression.AndAlso(hasTenantContext, tenantMatch);
        return Expression.Lambda(body, parameter);
    }

    private static LambdaExpression BuildCompanyOperationalFilter(Type clrType, ErpDbContext dbContext)
        => BuildOperationalScopedFilter(clrType, dbContext);

    private static LambdaExpression BuildCompanyScopedFilter(Type clrType, ErpDbContext dbContext)
        => BuildStrictCompanyScopedFilter(clrType, dbContext);

    private static LambdaExpression BuildOperationalScopedFilter(Type clrType, ErpDbContext dbContext)
    {
        var parameter  = Expression.Parameter(clrType, "e");
        var dbConstant = Expression.Constant(dbContext);

        var currentTenant = Expression.Property(dbConstant, nameof(ErpDbContext.FilterTenantId));
        var emptyGuid     = Expression.Constant(Guid.Empty);
        var hasTenantCtx  = Expression.NotEqual(currentTenant, emptyGuid);
        var tenantProp    = Expression.Property(parameter, nameof(ITenantScopedEntity.TenantId));
        var tenantMatch   = Expression.AndAlso(hasTenantCtx,
                              Expression.Equal(tenantProp, currentTenant));

        var hasCompanyCtx  = Expression.Property(dbConstant, nameof(ErpDbContext.FilterHasCompanyContext));
        var currentCompany = Expression.Property(dbConstant, nameof(ErpDbContext.FilterCompanyId));
        var companyProp    = Expression.Property(parameter, nameof(ICompanyOperationalEntity.CompanyId));
        var companyMatch   = Expression.AndAlso(hasCompanyCtx,
                               Expression.Equal(companyProp, currentCompany));

        return Expression.Lambda(Expression.AndAlso(tenantMatch, companyMatch), parameter);
    }

    private static LambdaExpression BuildStrictCompanyScopedFilter(Type clrType, ErpDbContext dbContext)
    {
        var parameter  = Expression.Parameter(clrType, "e");
        var dbConstant = Expression.Constant(dbContext);

        var currentTenant = Expression.Property(dbConstant, nameof(ErpDbContext.FilterTenantId));
        var emptyGuid     = Expression.Constant(Guid.Empty);
        var hasTenantCtx  = Expression.NotEqual(currentTenant, emptyGuid);
        var tenantProp    = Expression.Property(parameter, nameof(ITenantScopedEntity.TenantId));
        var tenantMatch   = Expression.AndAlso(hasTenantCtx,
                              Expression.Equal(tenantProp, currentTenant));

        var hasCompanyCtx  = Expression.Property(dbConstant, nameof(ErpDbContext.FilterHasCompanyContext));
        var currentCompany = Expression.Property(dbConstant, nameof(ErpDbContext.FilterCompanyId));
        var companyProp    = Expression.Property(parameter, nameof(ICompanyScopedEntity.CompanyId));
        var companyMatch   = Expression.AndAlso(hasCompanyCtx,
                               Expression.Equal(companyProp, currentCompany));

        return Expression.Lambda(Expression.AndAlso(tenantMatch, companyMatch), parameter);
    }
}
