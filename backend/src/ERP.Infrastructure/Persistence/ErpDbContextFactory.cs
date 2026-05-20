using ERP.Application.Common;
using ERP.Application.Common.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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

        var platform = new PlatformQueryAccessor(
            NullLogger<PlatformQueryAccessor>.Instance,
            Microsoft.Extensions.Options.Options.Create(new SaasEntitlementsOptions()));
        return new ErpDbContext(options, new DesignTimeSubscriber(), new NoOpPublisher(), platform);
    }

    /// <summary>Subscriber vacío para satisfacer ICurrentSubscriber en diseño.</summary>
    private sealed class DesignTimeSubscriber : ICurrentSubscriber
    {
        public Guid SubscriberId        => Guid.Empty;
        public bool IsAuthenticated => false;
    }
}
