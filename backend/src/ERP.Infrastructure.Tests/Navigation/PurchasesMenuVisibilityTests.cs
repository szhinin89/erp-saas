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

public sealed class PurchasesMenuVisibilityTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Purchase_operation_group_is_visible_only_with_its_children(
        bool canViewPurchases,
        bool canViewFinance
    )
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (canViewPurchases)
            allowed.Add(PurchasePermissions.View);
        if (canViewFinance)
            allowed.Add(FinancePermissions.View);

        var items = KernelRegistry.Navigation
            .Where(n => n.GroupCode == "purchases")
            .Select(n => UiNavItem.Create(
                n.Id, n.GroupId, n.RoutePath, n.LabelKey, n.SortOrder, n.GroupCode,
                n.PermissionKey, n.PermissionKeysAnyJson, rolesCsv: null,
                parentItemId: n.ParentItemId
            ))
            .ToList();

        // Exercise the real permission filter without a DbContext or a copy of its logic.
        var buildTree = typeof(NavigationBuilder).GetMethod(
            "BuildItemTree", BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var menu = (IReadOnlyList<NavMenuItemDto>)buildTree.Invoke(
            null, new object?[] { items, null, SecurityRoles.User, allowed, false }
        )!;

        if (!canViewPurchases)
        {
            menu.Should().BeEmpty("finance.view alone grants no purchase screen");
            return;
        }

        var operation = menu.Should().ContainSingle(n =>
            n.RoutePath == PurchasesModule.PurchasesGroup).Which;
        operation.Id.Should().Be(Guid.Parse("e3000000-0000-4000-9000-000000000010"));
        operation.Children.Select(n => n.RoutePath).Should().Equal(
            PurchasesModule.Invoices, PurchasesModule.Reception, PurchasesModule.CreditNotes
        );
        menu.Should().ContainSingle(n => n.RoutePath == PurchasesModule.ReportsGroup)
            .Which.Children.Should().ContainSingle(n => n.RoutePath == PurchasesModule.PurchasesReport);
    }
}
