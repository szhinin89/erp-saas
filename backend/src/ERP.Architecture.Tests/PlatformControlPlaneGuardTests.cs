using System.Text.RegularExpressions;
using FluentAssertions;

namespace ERP.Architecture.Tests;

/// <summary>
/// Platform Control Plane — backend API surface enforcement.
/// Fails if legacy <c>/api/superadmin</c> routes or SuperAdmin legacy controllers reappear.
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
            {
                violations.Add($"{rel}: legacy SuperAdmin controller file name");
            }

            foreach (Match match in RouteAttributeRegex.Matches(text))
            {
                var route = match.Groups[1].Value;
                if (route.Contains("superadmin", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{rel}: [Route(\"{route}\")]");
                }
            }

            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith("///", StringComparison.Ordinal) ||
                    trimmed.StartsWith('*'))
                {
                    continue;
                }

                if (line.Contains("SuperAdminController", StringComparison.Ordinal) ||
                    line.Contains("SuperAdminService", StringComparison.Ordinal) ||
                    line.Contains("superadmin-login", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("/api/superadmin/", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{rel}: {trimmed[..Math.Min(trimmed.Length, 120)]}");
                }
            }
        }

        violations.Should().BeEmpty("legacy SuperAdmin API surface must not exist; use /api/platform/*");
    }

    [Fact]
    public void Platform_controllers_must_use_api_platform_prefix()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var platformDir = Path.Combine(backendRoot, "ERP.API", "Controllers", "Platform");
        if (!Directory.Exists(platformDir))
            return;

        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(platformDir, "*Controller.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(backendRoot, file).Replace('\\', '/');
            var text = File.ReadAllText(file);
            var hasPlatformRoute = RouteAttributeRegex.Matches(text)
                .Select(m => m.Groups[1].Value)
                .Any(r => r.StartsWith("api/platform", StringComparison.OrdinalIgnoreCase));

            if (!hasPlatformRoute)
                violations.Add($"{rel}: missing [Route(\"api/platform/...\")]");
        }

        violations.Should().BeEmpty("Platform controllers must live under api/platform/*");
    }

    [Fact]
    public void SubscribersController_must_not_expose_platform_control_plane_routes()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var file = Path.Combine(backendRoot, "ERP.API", "Controllers", "SubscribersController.cs");
        File.Exists(file).Should().BeTrue();

        var text = File.ReadAllText(file);
        var forbidden = new[]
        {
            "HttpPost",
            "HttpPatch(\"{id:guid}/global-parameters\")",
            "HttpPatch(\"{id:guid}/subscription\")",
            "[DeprecatedApi(",
        };

        var violations = forbidden.Where(marker => text.Contains(marker, StringComparison.Ordinal)).ToList();
        violations.Should().BeEmpty(
            "SubscribersController is ERP runtime only; platform control plane lives under /api/platform/subscribers");
    }

    [Fact]
    public void Platform_frontend_must_not_define_duplicate_api_wrappers()
    {
        var frontendRoot = Path.Combine(ResolveBackendSrcRoot(), "..", "..", "frontend", "src");
        var forbidden = new[]
        {
            Path.Combine(frontendRoot, "modules", "platform", "api", "subscriberService.ts"),
            Path.Combine(frontendRoot, "modules", "platform", "api", "menuService.ts"),
        };

        var violations = forbidden.Where(File.Exists).Select(f => Path.GetFileName(f)).ToList();
        violations.Should().BeEmpty("Platform Control Plane must use a single platformService.ts client");
    }

    [Fact]
    public void Runtime_subscriber_entitlements_endpoint_must_remain_on_api_subscribers()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var file = Path.Combine(backendRoot, "ERP.API", "Controllers", "SaasEntitlementsController.cs");
        if (!File.Exists(file))
            return;

        var text = File.ReadAllText(file);
        text.Should().Contain("api/subscribers/entitlements",
            "GET /api/subscribers/entitlements/me is ERP runtime — must not move to platform");
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
