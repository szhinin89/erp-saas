using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ERP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Interceptor de SaveChanges — valida que toda entidad operacional persistida
/// tenga tenantId y CompanyId válidos (no Guid.Empty).
/// Falla en AddAsync/UpdateAsync antes de llegar a la BD.
/// Excepciones: entidades de solo lectura (Owned types, shadow FK), entidades
/// de plataforma (PlatformProvisioningAuditEvent) que son manejadas por IPlatformQueryAccessor.
/// </summary>
public sealed class CompanyTenantInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Validate(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Validate(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Validate(DbContext? ctx)
    {
        if (ctx is null) return;
        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            if (entry.Entity is ITenantScopedEntity sub && sub.TenantId == Guid.Empty)
                throw new InvalidOperationException(
                    $"[TenantGuard] {entry.Entity.GetType().Name}: tenantId is Guid.Empty. " +
                    "All tenant-scoped entities require a valid tenantId.");

            if (entry.Entity is ICompanyOperationalEntity op && op.CompanyId == Guid.Empty)
                throw new InvalidOperationException(
                    $"[TenantGuard] {entry.Entity.GetType().Name}: CompanyId is Guid.Empty. " +
                    "Operational entities require a valid CompanyId after migration.");

            if (entry.Entity is ICompanyScopedEntity cs && cs.CompanyId == Guid.Empty)
                throw new InvalidOperationException(
                    $"[TenantGuard] {entry.Entity.GetType().Name}: CompanyId is Guid.Empty. " +
                    "Company-scoped entities require a valid CompanyId.");
        }
    }
}
