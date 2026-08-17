using ERP.Domain.Kernel;
using FluentAssertions;

namespace ERP.Domain.Tests.Kernel;

/// <summary>
/// Invariantes del Platform Kernel: <see cref="KernelRegistry"/> es la única fuente de
/// verdad de permisos/navegación/módulos. Estos tests garantizan que la derivación por
/// reflexión produce un catálogo consistente (sin duplicados, sin huérfanos, sin
/// fragmentos legacy).
/// </summary>
public sealed class KernelRegistryTests
{
    // "sales", "purchases", "finance" y "cash" son vocabulario de negocio vigente y sancionado
    // en ERP_CORE_FREEZE.md (Nivel 1) — Sales/Accounting como módulos ERP, Purchases y Finance
    // (Condiciones de Crédito) ya implementados y activos, y CajaModule usa rutas reales
    // "/cash", "/cash/registers". No son fragmentos legacy: solo se bloquean aquí los nombres
    // realmente retirados y sin ningún uso actual en el Kernel ("purchasing" → sucedido por
    // "purchases", "expenses" sin uso, "products."/"inventory.products" → sucedido por "items.").
    private static readonly string[] LegacyFragments =
    [
        "purchasing",
        "expenses",
        "inventory.products",
        "products.",
    ];

    [Fact]
    public void Permissions_have_no_duplicate_values()
    {
        var permissions = KernelRegistry.Permissions;

        permissions.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Navigation_permission_keys_exist_in_Permissions_registry()
    {
        var allowed = new HashSet<string>(KernelRegistry.Permissions, StringComparer.Ordinal);

        var orphanKeys = KernelRegistry
            .Navigation.Where(n => n.PermissionKey is not null)
            .Select(n => n.PermissionKey!)
            .Where(key => !allowed.Contains(key))
            .Distinct()
            .ToList();

        orphanKeys
            .Should()
            .BeEmpty(
                "todo permiso referenciado por navegación debe existir en KernelRegistry.Permissions"
            );
    }

    [Fact]
    public void Navigation_items_have_unique_ids_and_route_paths()
    {
        var navigation = KernelRegistry.Navigation;

        navigation.Select(n => n.Id).Should().OnlyHaveUniqueItems();
        navigation.Select(n => (n.GroupId, n.RoutePath)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Modules_settings_group_preserves_legacy_guid()
    {
        var settings = KernelRegistry.Modules.Single(m => m.Code == "settings");

        settings.GroupId.Should().Be(Guid.Parse("f2d0ca10-0000-4000-8000-000000000008"));
    }

    [Fact]
    public void Navigation_items_preserve_legacy_guids_for_company_and_companies()
    {
        var byRoute = KernelRegistry.Navigation.ToDictionary(n => n.RoutePath, n => n.Id);

        byRoute["/settings/company"]
            .Should()
            .Be(Guid.Parse("00000000-0000-4000-8000-000000000101"));
        byRoute["/companies"].Should().Be(Guid.Parse("00000000-0000-4000-8000-000000000104"));
    }

    [Fact]
    public void Navigation_contains_sales_and_purchase_returns_list_entries_only()
    {
        var navigation = KernelRegistry.Navigation;

        var salesReturns = navigation.SingleOrDefault(n => n.RoutePath == "/sales/returns");
        salesReturns.Should().NotBeNull("el listado de devoluciones de venta debe estar en el menú");
        salesReturns!.ParentItemId.Should().Be(Guid.Parse("e4000000-0000-4000-9000-000000000001"));
        salesReturns.PermissionKey.Should().Be(ERP.Domain.Kernel.Permissions.SalesPermissions.View);

        var purchaseReturns = navigation.SingleOrDefault(n => n.RoutePath == "/purchases/returns");
        purchaseReturns.Should().NotBeNull("el listado de devoluciones de compra debe estar en el menú");
        purchaseReturns!.ParentItemId.Should().Be(Guid.Parse("e3000000-0000-4000-9000-000000000001"));
        purchaseReturns.PermissionKey.Should().Be(ERP.Domain.Kernel.Permissions.PurchasePermissions.View);

        navigation.Should().NotContain(n => n.RoutePath == "/sales/returns/new");
        navigation.Should().NotContain(n => n.RoutePath.StartsWith("/sales/returns/"));
        navigation.Should().NotContain(n => n.RoutePath == "/purchases/returns/new");
        navigation.Should().NotContain(n => n.RoutePath.StartsWith("/purchases/returns/"));
        navigation.Should().NotContain(n => n.RoutePath.StartsWith("/purchases/credit-notes"));
    }

    [Fact]
    public void Modules_contains_finance_group()
    {
        var finance = KernelRegistry.Modules.SingleOrDefault(m => m.Code == "finance");

        finance.Should().NotBeNull("el grupo Finanzas debe estar registrado en el Kernel");
    }

    [Fact]
    public void Navigation_contains_finance_receivables_payables_and_supplier_credits()
    {
        var navigation = KernelRegistry.Navigation;
        var financePermission = ERP.Domain.Kernel.Permissions.FinancePermissions.View;

        var receivables = navigation.SingleOrDefault(n => n.RoutePath == "/finance/receivables");
        receivables.Should().NotBeNull("cuentas por cobrar debe estar en el menú");
        receivables!.PermissionKey.Should().Be(financePermission);
        receivables.SortOrder.Should().Be(10);

        var payables = navigation.SingleOrDefault(n => n.RoutePath == "/finance/payables");
        payables.Should().NotBeNull("cuentas por pagar debe estar en el menú");
        payables!.PermissionKey.Should().Be(financePermission);
        payables.SortOrder.Should().Be(20);

        var supplierCredits = navigation.SingleOrDefault(n =>
            n.RoutePath == "/finance/supplier-credits"
        );
        supplierCredits.Should().NotBeNull("créditos de proveedor debe estar en el menú");
        supplierCredits!.PermissionKey.Should().Be(financePermission);
        supplierCredits.SortOrder.Should().Be(30);

        navigation.Should().NotContain(n => n.RoutePath == "/finance/supplier-credits/:id");
        navigation
            .Should()
            .NotContain(n => n.RoutePath.StartsWith("/finance/supplier-credits/", StringComparison.Ordinal));

        var creditTerms = navigation.Single(n => n.RoutePath == "/finance/credit-terms");
        creditTerms.GroupCode.Should().Be("masterdata", "credit-terms no debe moverse en esta tarea");
        creditTerms.ParentItemId.Should().Be(Guid.Parse("e1000000-0000-4000-9000-000000000001"));
    }

    [Fact]
    public void Modules_contains_reports_group()
    {
        var reports = KernelRegistry.Modules.SingleOrDefault(m => m.Code == "reports");

        reports.Should().NotBeNull("el grupo Reportes debe estar registrado en el Kernel");
    }

    [Fact]
    public void Navigation_contains_sales_stock_and_purchases_reports_with_domain_permissions()
    {
        var navigation = KernelRegistry.Navigation;

        var salesReport = navigation.SingleOrDefault(n => n.RoutePath == "/reportes/ventas");
        salesReport.Should().NotBeNull("el reporte de ventas debe estar en el menú");
        salesReport!.PermissionKey.Should().Be(ERP.Domain.Kernel.Permissions.SalesPermissions.View);
        salesReport.SortOrder.Should().Be(10);

        var stockReport = navigation.SingleOrDefault(n => n.RoutePath == "/reportes/stock");
        stockReport.Should().NotBeNull("el reporte de stock debe estar en el menú");
        stockReport!.PermissionKey.Should()
            .Be(ERP.Domain.Kernel.Permissions.InventoryPermissions.StockView);
        stockReport.SortOrder.Should().Be(20);

        var purchasesReport = navigation.SingleOrDefault(n => n.RoutePath == "/reportes/compras");
        purchasesReport.Should().NotBeNull("el reporte de compras debe estar en el menú");
        purchasesReport!.PermissionKey.Should()
            .Be(ERP.Domain.Kernel.Permissions.PurchasePermissions.View);
        purchasesReport.SortOrder.Should().Be(30);

        navigation.Should().NotContain(n => n.RoutePath.StartsWith("/reportes/", StringComparison.Ordinal)
            && n.RoutePath != "/reportes/ventas"
            && n.RoutePath != "/reportes/stock"
            && n.RoutePath != "/reportes/compras");
    }

    [Fact]
    public void Navigation_reports_group_does_not_affect_finance_module()
    {
        var finance = KernelRegistry.Modules.Single(m => m.Code == "finance");
        finance.Icon.Should().Be("💳");
        finance.SortOrder.Should().Be(46);

        var financeItems = new[]
        {
            "/finance/receivables",
            "/finance/payables",
            "/finance/supplier-credits",
        };

        foreach (var route in financeItems)
        {
            KernelRegistry
                .Navigation.Single(n => n.RoutePath == route)
                .GroupCode.Should()
                .Be("finance", $"'{route}' debe seguir perteneciendo al grupo finance");
        }
    }

    [Fact]
    public void Navigation_contains_settings_financial_destinations_and_leaves_finance_and_reports_untouched()
    {
        var navigation = KernelRegistry.Navigation;

        var financialDestinations = navigation.SingleOrDefault(n =>
            n.RoutePath == "/settings/financial-destinations"
        );
        financialDestinations.Should().NotBeNull("destinos financieros debe estar en el menú");
        financialDestinations!.GroupCode.Should().Be("settings");
        financialDestinations
            .PermissionKey.Should()
            .Be(ERP.Domain.Kernel.Permissions.SettingsPermissions.FinancialDestinationsView);

        var finance = KernelRegistry.Modules.Single(m => m.Code == "finance");
        finance.Icon.Should().Be("💳");
        finance.SortOrder.Should().Be(46);

        var reports = KernelRegistry.Modules.Single(m => m.Code == "reports");
        reports.Icon.Should().Be("📊");
        reports.SortOrder.Should().Be(55);

        navigation
            .Where(n => n.GroupCode is "finance" or "reports")
            .Should()
            .HaveCount(6, "las 3 rutas de finance y las 3 de reports deben permanecer sin cambios");
    }

    [Fact]
    public void Navigation_contains_admin_access_sessions_and_leaves_other_modules_untouched()
    {
        var navigation = KernelRegistry.Navigation;

        var accessSessions = navigation.SingleOrDefault(n =>
            n.RoutePath == "/admin/access/sessions"
        );
        accessSessions.Should().NotBeNull("sesiones de usuario debe estar en el menú Admin");
        accessSessions!.GroupCode.Should().Be("admin");
        accessSessions
            .PermissionKey.Should()
            .Be(ERP.Domain.Kernel.Permissions.AccessPermissions.SessionsView);
        accessSessions.SortOrder.Should().Be(45);

        navigation.Should().NotContain(n => n.RoutePath == "/rrhh");

        var finance = KernelRegistry.Modules.Single(m => m.Code == "finance");
        finance.Icon.Should().Be("💳");
        finance.SortOrder.Should().Be(46);

        var reports = KernelRegistry.Modules.Single(m => m.Code == "reports");
        reports.Icon.Should().Be("📊");
        reports.SortOrder.Should().Be(55);

        var settingsFinancialDestinations = navigation.Single(n =>
            n.RoutePath == "/settings/financial-destinations"
        );
        settingsFinancialDestinations.GroupCode.Should().Be("settings");
        settingsFinancialDestinations.SortOrder.Should().Be(60);
    }

    [Fact]
    public void Permissions_and_routes_have_no_legacy_module_fragments()
    {
        var keys = KernelRegistry
            .Permissions.Concat(KernelRegistry.Navigation.Select(n => n.RoutePath))
            .Concat(KernelRegistry.Modules.Select(m => m.Code));

        foreach (var key in keys)
        {
            foreach (var fragment in LegacyFragments)
            {
                key.Should()
                    .NotContain(
                        fragment,
                        $"'{key}' no debe contener el fragmento legacy '{fragment}'"
                    );
            }
        }
    }
}
