using System.Reflection;
using ERP.Application.Navigation.DTOs;
using ERP.Domain.Kernel;
using ERP.Domain.Kernel.Modules;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Navigation.Entities;
using ERP.Infrastructure.Persistence.Navigation;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Navigation;

/// <summary>
/// NAV-COVERAGE-H2: "Datos de la empresa" se muestra con CompanyView o CompaniesView (capacidades
/// independientes); cada pestaña aplica después su propio permiso en el frontend.
/// </summary>
public sealed class CompanySettingsMenuVisibilityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Company_settings_entry_is_visible_with_either_read_permission(
        bool canViewCompanySettings,
        bool canViewCompanies
    )
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (canViewCompanySettings)
            allowed.Add(SettingsPermissions.CompanyView);
        if (canViewCompanies)
            allowed.Add(SettingsPermissions.CompaniesView);

        var items = KernelRegistry
            .Navigation.Where(n => n.GroupCode == "settings")
            .Select(n =>
                UiNavItem.Create(
                    n.Id, n.GroupId, n.RoutePath, n.LabelKey, n.SortOrder, n.GroupCode,
                    n.PermissionKey, n.PermissionKeysAnyJson, rolesCsv: null,
                    parentItemId: n.ParentItemId
                )
            )
            .ToList();

        // Filtro real de NavigationBuilder, sin DbContext ni copia de su lógica.
        var buildTree = typeof(NavigationBuilder).GetMethod(
            "BuildItemTree", BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var menu = (IReadOnlyList<NavMenuItemDto>)buildTree.Invoke(
            null, new object?[] { items, null, SecurityRoles.User, allowed, false }
        )!;

        if (!canViewCompanySettings && !canViewCompanies)
        {
            menu.Should().BeEmpty("without either read permission no Empresa screen is granted");
            return;
        }

        var enterprise = menu.Should().ContainSingle(n =>
            n.RoutePath == SettingsModule.EnterpriseGroup).Which;
        enterprise.Children.Select(n => n.RoutePath).Should().Equal(SettingsModule.Company);
    }

    [Fact]
    public void Company_settings_entry_keeps_company_view_as_assignable_permission()
    {
        var company = KernelRegistry.Navigation.Single(n => n.RoutePath == SettingsModule.Company);

        company.PermissionKey.Should().Be(SettingsPermissions.CompanyView);
        System.Text.Json.JsonSerializer.Deserialize<string[]>(company.PermissionKeysAnyJson!)
            .Should().BeEquivalentTo(SettingsPermissions.CompanyView, SettingsPermissions.CompaniesView);
        KernelRegistry.AssignablePermissionKeys.Should()
            .Contain(new[] { SettingsPermissions.CompanyView, SettingsPermissions.CompaniesView });
    }
}
