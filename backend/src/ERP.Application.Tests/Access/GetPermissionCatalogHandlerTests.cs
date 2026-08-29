using ERP.Application.Access.UseCases.Permissions;
using ERP.Domain.Kernel;
using FluentAssertions;

namespace ERP.Application.Tests.Access;

/// <summary>
/// ADMIN-PERMISSIONS-SSOT-KERNEL-02 — el handler no tiene dependencias (opera 100% en memoria
/// sobre KernelRegistry), así que estos tests no mockean nada: verifican el resultado real contra
/// el Kernel real, incluyendo la garantía de extensibilidad (completitud 1:1 con Navigation).
/// </summary>
public sealed class GetPermissionCatalogHandlerTests
{
    private readonly GetPermissionCatalogHandler _handler = new();

    [Fact]
    public async Task Catalog_groups_are_ordered_like_the_menu_and_match_module_sort_order()
    {
        var result = await _handler.Handle(new GetPermissionCatalogQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var groups = result.Value!.Groups;

        groups.Should().NotBeEmpty();
        groups.Select(g => g.SortOrder).Should().BeInAscendingOrder();
        groups.Select(g => g.Code).Should().Contain(new[]
        {
            "customers", "suppliers", "products", "inventory", "sales", "accounting", "settings", "admin",
        });
    }

    [Fact]
    public async Task Catalog_is_1to1_complete_with_every_navitem_that_has_a_real_permission()
    {
        var result = await _handler.Handle(new GetPermissionCatalogQuery(), CancellationToken.None);
        var catalogItemIds = result.Value!.Groups.SelectMany(g => g.Categories.SelectMany(c => c.Items)).Select(i => i.Id).ToHashSet();

        var expectedIds = KernelRegistry
            .Navigation.Where(n => n.PermissionKey is not null)
            .Select(n => n.Id)
            .ToHashSet();

        catalogItemIds.Should().BeEquivalentTo(expectedIds);
    }

    [Fact]
    public async Task Every_item_generates_a_view_action_using_the_navitems_own_permission()
    {
        var result = await _handler.Handle(new GetPermissionCatalogQuery(), CancellationToken.None);

        foreach (var group in result.Value!.Groups)
        foreach (var category in group.Categories)
        foreach (var item in category.Items)
        {
            item.Actions.Should().NotBeEmpty();
            item.Actions[0].Code.Should().Be(item.Permission);
            item.Actions[0].Label.Should().Be("Ver / Acceder");
        }
    }

    [Fact]
    public async Task Supplier_payments_screen_exposes_create_and_reverse_as_extra_actions()
    {
        var result = await _handler.Handle(new GetPermissionCatalogQuery(), CancellationToken.None);

        var item = result
            .Value!.Groups.SelectMany(g => g.Categories.SelectMany(c => c.Items))
            .Single(i => i.Route == "/supplier-payments");

        item.Actions.Select(a => a.Code).Should().BeEquivalentTo(new[]
        {
            "supplier-payments.view", "supplier-payments.create", "supplier-payments.reverse",
        });
    }

    [Fact]
    public async Task No_item_contains_duplicate_action_codes()
    {
        var result = await _handler.Handle(new GetPermissionCatalogQuery(), CancellationToken.None);

        foreach (var group in result.Value!.Groups)
        foreach (var category in group.Categories)
        foreach (var item in category.Items)
            item.Actions.Select(a => a.Code).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Catalog_never_exposes_the_legacy_dead_permissions()
    {
        var result = await _handler.Handle(new GetPermissionCatalogQuery(), CancellationToken.None);
        var allCodes = result
            .Value!.Groups.SelectMany(g => g.Categories.SelectMany(c => c.Items))
            .SelectMany(i => i.Actions)
            .Select(a => a.Code)
            .ToHashSet();

        allCodes.Should().NotContain("admin.roles.view");
        allCodes.Should().NotContain("admin.users.view");
        allCodes.Should().NotContain("logistics.carriers.view");
        allCodes.Should().NotContain("finance.delete");
    }

    [Fact]
    public async Task Catalog_only_contains_permission_keys_that_exist_in_the_kernel_registry()
    {
        var allPermissions = new HashSet<string>(KernelRegistry.Permissions, StringComparer.Ordinal);
        var result = await _handler.Handle(new GetPermissionCatalogQuery(), CancellationToken.None);

        var allCodes = result
            .Value!.Groups.SelectMany(g => g.Categories.SelectMany(c => c.Items))
            .SelectMany(i => i.Actions)
            .Select(a => a.Code);

        allCodes.Should().OnlyContain(code => allPermissions.Contains(code));
    }
}
