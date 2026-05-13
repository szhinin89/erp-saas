using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ERP.Application.Common;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// Permite a las herramientas EF Core (dotnet ef migrations add/remove)
/// instanciar ErpDbContext sin necesitar el startup project ERP.API.
/// Solo se usa en tiempo de diseño; no afecta el runtime.
/// </summary>
internal sealed class ErpDbContextFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5435;Database=dberpsaas;Username=postgres;Password=zhin@2024",
                npgsql => npgsql.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName))
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new ErpDbContext(options, new DesignTimeTenant(), new NoOpPublisher());
    }

    /// <summary>Tenant vacío para satisfacer ICurrentTenant en diseño.</summary>
    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId        => Guid.Empty;
        public bool IsAuthenticated => false;
    }
}
