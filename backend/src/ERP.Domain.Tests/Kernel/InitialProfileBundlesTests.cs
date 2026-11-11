using ERP.Domain.Kernel;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;

namespace ERP.Domain.Tests.Kernel;

/// <summary>
/// IAM-V1-F3 — los perfiles iniciales Facturador y Contador solo usan claves reales y asignables
/// del Kernel, sin duplicados y sin permisos administrativos, de caja ni de cobros.
/// </summary>
public sealed class InitialProfileBundlesTests
{
    private static readonly string[] Forbidden =
    [
        "finance.create",
        "accounting.manual.reverse",
        "accounting.manual.discard",
        "accounting.periods.create",
        "accounting.periods.close",
        "accounting.postings.reprocess",
        "accounting.create",
        "accounting.update",
    ];

    public static IEnumerable<object[]> Bundles() =>
        InitialProfileBundles.All.Select(b => new object[] { b });

    [Fact]
    public void Defines_exactly_Facturador_and_Contador()
    {
        InitialProfileBundles.All.Select(b => b.Name).Should().Equal("Facturador", "Contador");
    }

    [Theory]
    [MemberData(nameof(Bundles))]
    public void Every_key_is_an_existing_assignable_permission(InitialProfileBundle bundle)
    {
        bundle.Permissions.Except(KernelRegistry.Permissions).Should().BeEmpty();
        bundle.Permissions.Where(k => !KernelRegistry.AssignablePermissionKeys.Contains(k))
            .Should().BeEmpty("el administrador debe poder editar el perfil sin claves rechazadas");
    }

    [Theory]
    [MemberData(nameof(Bundles))]
    public void Has_no_duplicates_and_no_excluded_or_privileged_keys(InitialProfileBundle bundle)
    {
        bundle.Permissions.Should().OnlyHaveUniqueItems();
        bundle.Permissions.Should().NotIntersectWith(Forbidden);
        bundle.Permissions.Should().NotContain(k =>
            k.StartsWith("caja.") || k.StartsWith("access.") || k.EndsWith(".manage") || k.EndsWith(".delete"));
    }
}
