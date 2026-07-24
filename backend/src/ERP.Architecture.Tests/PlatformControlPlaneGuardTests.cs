using System.Text.RegularExpressions;
using FluentAssertions;

namespace ERP.Architecture.Tests;

/// <summary>
/// ERP API surface guard — CI bloqueante.
/// Verifica que no existan rutas legacy de superadmin ni endpoints de control plane SaaS.
/// </summary>
public sealed class PlatformControlPlaneGuardTests
{
    private static readonly Regex RouteAttributeRegex = new(
        @"\[Route\s*\(\s*""([^""]+)""\s*\)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void Controllers_must_not_expose_api_superadmin_routes()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var controllersDir = Path.Combine(backendRoot, "ERP.API", "Controllers");
        Directory.Exists(controllersDir).Should().BeTrue();

        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(controllersDir, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(backendRoot, file).Replace('\\', '/');
            var text = File.ReadAllText(file);

            if (Path.GetFileName(file).Contains("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                violations.Add($"{rel}: legacy superadmin controller file name");

            foreach (Match match in RouteAttributeRegex.Matches(text))
            {
                var route = match.Groups[1].Value;
                if (route.Contains("superadmin", StringComparison.OrdinalIgnoreCase))
                    violations.Add($"{rel}: [Route(\"{route}\")]");
            }

            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith("///", StringComparison.Ordinal) ||
                    trimmed.StartsWith('*'))
                    continue;

                if (line.Contains("SuperAdminController", StringComparison.Ordinal) ||
                    line.Contains("SuperAdminService", StringComparison.Ordinal) ||
                    line.Contains("superadmin-login", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("/api/superadmin/", StringComparison.OrdinalIgnoreCase))
                    violations.Add($"{rel}: {trimmed[..Math.Min(trimmed.Length, 120)]}");
            }
        }

        violations.Should().BeEmpty("legacy superadmin API surface must not exist in ERP");
    }

    /// <summary>ERP pure must not contain a Platform controllers directory.</summary>
    [Fact]
    public void ERP_must_not_contain_platform_controllers_directory()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var platformDir = Path.Combine(backendRoot, "ERP.API", "Controllers", "Platform");
        Directory.Exists(platformDir).Should().BeFalse(
            "ERP does not own the Platform control plane. Platform is a separate future bounded context.");
    }

    [Fact]
    public void TenantsController_must_not_expose_saas_control_plane_routes()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var file = Path.Combine(backendRoot, "ERP.API", "Controllers", "TenantsController.cs");
        if (!File.Exists(file))
            return;

        var text = File.ReadAllText(file);
        var forbidden = new[]
        {
            "HttpPatch(\"{id:guid}/subscription\")",
            "HttpPatch(\"{id:guid}/global-parameters\")",
            "[DeprecatedApi(",
        };

        var violations = forbidden.Where(marker => text.Contains(marker, StringComparison.Ordinal)).ToList();
        violations.Should().BeEmpty(
            "TenantsController is ERP runtime only. Subscription/billing control lives in future ZH.Platform.");
    }

    [Fact]
    public void ERP_domain_must_not_reference_saas_namespaces()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var domainDir = Path.Combine(backendRoot, "ERP.Domain");
        if (!Directory.Exists(domainDir))
            return;

        var forbiddenTerms = new[] { "Subscription", "CommercialPlan", "BillingCycle", "SaasBilling" };
        var violations = new List<string>();

        foreach (var csFile in Directory.EnumerateFiles(domainDir, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(backendRoot, csFile).Replace('\\', '/');
            var text = File.ReadAllText(csFile);
            foreach (var term in forbiddenTerms)
            {
                if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                    violations.Add($"{rel}: contains '{term}'");
            }
        }

        violations.Should().BeEmpty("ERP.Domain must be free of SaaS billing/subscription concepts");
    }

    private static string ResolveBackendSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "ERP.API")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró backend/src (ERP.API).");
    }
}
