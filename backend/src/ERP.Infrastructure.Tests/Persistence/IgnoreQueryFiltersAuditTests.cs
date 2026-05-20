using FluentAssertions;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// Evita nuevos usos directos de <c>IgnoreQueryFilters()</c> fuera del wrapper y excepciones documentadas.
/// </summary>
public sealed class IgnoreQueryFiltersAuditTests
{
    private static readonly HashSet<string> AllowedRelativePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "src/ERP.Infrastructure/Persistence/PlatformQueryAccessor.cs",
        "src/ERP.Infrastructure/Persistence/ErpDbContext.cs",
        "src/ERP.Infrastructure/Seeding/TenantOnboardingService.cs",
        "src/ERP.Infrastructure/Seeding/DefaultProfileSeeder.cs",
        "src/ERP.Infrastructure/Deployment/FirstRunSetupService.cs",
        "src/ERP.Infrastructure/BackgroundServices/KardexReporteProcessor.cs",
        "src/ERP.API/Hangfire/SriRetryJob.cs",
        "src/ERP.API/Extensions/DevDatabaseSeeder.cs",
    };

    [Fact]
    public void IgnoreQueryFilters_is_only_used_in_allowlisted_files()
    {
        var backendRoot = ResolveBackendRoot();
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = File.ReadAllText(file);
            if (!text.Contains(".IgnoreQueryFilters(", StringComparison.Ordinal))
                continue;

            var relative = Path.GetRelativePath(backendRoot, file).Replace('\\', '/');
            if (!AllowedRelativePaths.Contains(relative))
                violations.Add(relative);
        }

        violations.Should().BeEmpty(
            "nuevos IgnoreQueryFilters deben usar IPlatformQueryAccessor; excepciones solo en la allowlist del test");
    }

    private static string ResolveBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ERP.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, "src", "ERP.API")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró la raíz backend (ERP.API / ERP.sln).");
    }
}
