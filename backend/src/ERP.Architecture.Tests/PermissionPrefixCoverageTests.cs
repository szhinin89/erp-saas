using System.Text.RegularExpressions;
using ERP.Application.Common;
using FluentAssertions;

namespace ERP.Architecture.Tests;

/// <summary>
/// P0 governance test: every permission prefix used in [Authorize(Policy = "perm:...")] attributes
/// in API controllers MUST be registered in SubscriberSubscriptionCatalog.PermissionPrefixToCanonicalModule
/// (or be a whitelisted subscriber-account permission).
///
/// A failing test means a new module added permissions to a controller without registering the prefix,
/// creating an ungoverned permission that bypasses plan entitlements.
/// </summary>
public sealed class PermissionPrefixCoverageTests
{
    // Regex captures the key only from [Authorize(Policy = "perm:...")] — not from AppFeature or comments.
    // Must match: Authorize(Policy = "perm:KEY") or Authorize(Policy = "perm:KEY",
    private static readonly Regex PermKeyRegex =
        new(@"Authorize\s*\(\s*Policy\s*=\s*""perm:([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void All_controller_permission_prefixes_must_be_registered_in_plan_map()
    {
        var srcRoot      = ResolveBackendSrcRoot();
        var controllersDir = Path.Combine(srcRoot, "ERP.API", "Controllers");
        Directory.Exists(controllersDir).Should().BeTrue("controllers directory must exist");

        // --- Collect all unique permission keys from [Authorize(Policy = "perm:...")] ---
        var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(controllersDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in PermKeyRegex.Matches(text))
            {
                var key = m.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(key))
                    allKeys.Add(key);
            }
        }

        allKeys.Should().NotBeEmpty("there should be permission-protected endpoints");

        // --- Classify each key ---
        var unmappedPrefixes = new List<string>();

        foreach (var key in allKeys.OrderBy(k => k))
        {
            // Subscriber-account permissions are whitelisted — always allowed by policy.
            if (SubscriberSubscriptionCatalog.IsSubscriberAccountPermission(key))
                continue;

            // Keys like "{permissions...}" from templates/comments — skip.
            if (key.StartsWith('{'))
                continue;

            if (!SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission(key, out _))
            {
                var dot    = key.IndexOf('.');
                var prefix = dot > 0 ? key[..dot] : key;
                unmappedPrefixes.Add($"Prefix '{prefix}' (from key '{key}')");
            }
        }

        unmappedPrefixes.Should().BeEmpty(
            "every permission prefix used in controllers must be registered in " +
            "SubscriberSubscriptionCatalog.PermissionPrefixToCanonicalModule so the plan " +
            "entitlement gate can govern it. Unmapped prefixes bypass plan controls.");
    }

    [Fact]
    public void All_Permissions_class_constants_must_have_mapped_prefix()
    {
        // Collect every public const string from Permissions.cs via reflection.
        var permType = typeof(Permissions);
        var nestedTypes = permType.GetNestedTypes(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        var unmapped = new List<string>();

        foreach (var nested in nestedTypes)
        {
            var fields = nested.GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.FlattenHierarchy);

            foreach (var field in fields.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string)))
            {
                var key = field.GetRawConstantValue() as string;
                if (string.IsNullOrEmpty(key)) continue;

                // Skip obsolete fields (marked with [Obsolete])
                var isObsolete = field.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length > 0
                              || nested.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length > 0;
                if (isObsolete) continue;

                // Skip saas whitelisted and account permissions.
                if (SubscriberSubscriptionCatalog.IsSubscriberAccountPermission(key)) continue;

                if (!SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission(key, out _))
                {
                    var dot    = key.IndexOf('.');
                    var prefix = dot > 0 ? key[..dot] : key;
                    unmapped.Add($"Permissions.{nested.Name}.{field.Name} = '{key}' (prefix '{prefix}')");
                }
            }
        }

        unmapped.Should().BeEmpty(
            "every permission key defined in Permissions.cs must have a registered prefix in " +
            "SubscriberSubscriptionCatalog.PermissionPrefixToCanonicalModule. " +
            "Unmapped permissions bypass commercial plan governance.");
    }

    [Fact]
    public void Ungoverned_permission_count_must_be_zero()
    {
        // Counts the total number of ungoverned permission keys across both
        // controllers AND Permissions.cs. Provides a single numeric assertion
        // that CI can track over time.

        var srcRoot       = ResolveBackendSrcRoot();
        var controllersDir = Path.Combine(srcRoot, "ERP.API", "Controllers");

        var controllerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(controllersDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in PermKeyRegex.Matches(text))
            {
                var key = m.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(key) && !key.StartsWith('{'))
                    controllerKeys.Add(key);
            }
        }

        var ungoverned = controllerKeys
            .Where(k => !SubscriberSubscriptionCatalog.IsSubscriberAccountPermission(k))
            .Where(k => !SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission(k, out _))
            .ToList();

        ungoverned.Should().HaveCount(0,
            $"ungoverned permissions found: {string.Join(", ", ungoverned)}");
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
