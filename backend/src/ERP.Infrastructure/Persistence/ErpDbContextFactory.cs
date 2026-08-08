using ERP.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// Permite a las herramientas EF Core (dotnet ef migrations add/remove)
/// instanciar ErpDbContext sin necesitar el startup project ERP.API.
/// Solo se usa en tiempo de diseño; no afecta el runtime.
///
/// Nunca hardcodear la connection string aquí (incluye password) — se lee de
/// appsettings.Development.json (gitignored, junto a ERP.API) y/o de la variable
/// de entorno ConnectionStrings__DefaultConnection, igual que en runtime.
/// </summary>
internal sealed class ErpDbContextFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args)
    {
        var apiProjectDir = FindApiProjectDirectory(Directory.GetCurrentDirectory());

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectDir ?? Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "No se encontró ConnectionStrings:DefaultConnection para `dotnet ef`. "
                    + "Configúrala en backend/src/ERP.API/appsettings.Development.json (gitignored) "
                    + "o exporta ConnectionStrings__DefaultConnection antes de ejecutar el comando."
            );

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName)
            )
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new ErpDbContext(
            options,
            new DesignTimeTenant(),
            new NoOpPublisher(),
            new DesignTimeCompany()
        );
    }

    /// <summary>Busca backend/src/ERP.API hacia arriba desde el directorio de invocación de `dotnet ef`.</summary>
    private static string? FindApiProjectDirectory(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "ERP.API");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "ERP.API.csproj")))
                return candidate;

            if (
                string.Equals(dir.Name, "ERP.API", StringComparison.OrdinalIgnoreCase)
                && File.Exists(Path.Combine(dir.FullName, "ERP.API.csproj"))
            )
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId => Guid.Empty;
        public string? Slug => null;
    }

    private sealed class DesignTimeCompany : ICurrentCompany
    {
        public Guid CompanyId => Guid.Empty;
        public bool IsAuthenticated => false;
        public bool HasCompanyContext => false;
    }
}
