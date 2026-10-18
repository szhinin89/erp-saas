using ERP.Application.Common;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ERP.API.Hosting;

/// <summary>
/// Contexto para las herramientas EF (<c>dotnet ef</c> con <c>--startup-project ERP.API</c>). Lee la
/// conexión con las mismas fuentes que el runtime (<see cref="ErpHostRegistration.AddErpConfigurationSources"/>):
/// appsettings, user-secrets en Development, <c>ConnectionStrings__DefaultConnection</c> y argumentos.
/// Entorno por defecto Development, como las herramientas EF; otro entorno vía
/// <c>ASPNETCORE_ENVIRONMENT</c>/<c>DOTNET_ENVIRONMENT</c>. Solo tiempo de diseño; no afecta el runtime.
/// </summary>
internal sealed class ErpDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args)
    {
        var environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environments.Development;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(FindApiProjectDirectory(Directory.GetCurrentDirectory()) ?? AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddErpConfigurationSources(environment, args)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"No se encontró ConnectionStrings:DefaultConnection para `dotnet ef` (entorno {environment}). "
                    + "En Development configúrala en los user-secrets de ERP.API; en otro entorno exporta "
                    + "ConnectionStrings__DefaultConnection."
            );

        if (args.Contains("--launcher-reset=true", StringComparer.Ordinal))
            LauncherResetGuard.Confirm(connectionString, environment, Console.WriteLine, Console.ReadLine);

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
            if (
                Directory.Exists(candidate)
                && File.Exists(Path.Combine(candidate, "ERP.API.csproj"))
            )
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
