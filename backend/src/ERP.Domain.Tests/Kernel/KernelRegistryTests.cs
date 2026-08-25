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
    public void Navigation_companies_and_company_are_grouped_under_a_single_empresas_container_without_losing_permissions()
    {
        // MENU-FINAL-STRUCTURE-01: "Mis empresas" (multiempresa, CompaniesView) y "Datos de la
        // empresa" (empresa activa, CompanyView) son pantallas reales distintas con permisos
        // distintos — no se fusionaron en una sola entrada (se perdería funcionalidad o se
        // mezclarían permisos). En su lugar se agruparon bajo un contenedor "Empresas" — este
        // test prueba que ambas siguen existiendo, con su Id/ruta/permiso intactos.
        var navigation = KernelRegistry.Navigation;
        var empresasGroupId = Guid.Parse("00000000-0000-4000-8000-000000000105");

        var empresasContainer = navigation.Single(n => n.RoutePath == "/settings/companies-group");
        empresasContainer.GroupCode.Should().Be("settings");
        empresasContainer.ParentItemId.Should().BeNull();
        empresasContainer.Id.Should().Be(empresasGroupId);

        var companies = navigation.Single(n => n.RoutePath == "/companies");
        companies.Id.Should().Be(Guid.Parse("00000000-0000-4000-8000-000000000104"));
        companies.ParentItemId.Should().Be(empresasGroupId);
        companies.PermissionKey.Should().Be(ERP.Domain.Kernel.Permissions.SettingsPermissions.CompaniesView);

        var company = navigation.Single(n => n.RoutePath == "/settings/company");
        company.Id.Should().Be(Guid.Parse("00000000-0000-4000-8000-000000000101"));
        company.ParentItemId.Should().Be(empresasGroupId);
        company.PermissionKey.Should().Be(ERP.Domain.Kernel.Permissions.SettingsPermissions.CompanyView);
    }

    [Fact]
    public void Navigation_contains_sales_and_purchase_returns_list_entries_only()
    {
        var navigation = KernelRegistry.Navigation;

        // MENU-MODULE-REORG-01: reparentado bajo el contenedor "Operación" de cada módulo
        // (antes "Documentos") — mismo Id/ruta/permiso, solo cambia el ParentId.
        var salesReturns = navigation.SingleOrDefault(n => n.RoutePath == "/sales/returns");
        salesReturns.Should().NotBeNull("el listado de devoluciones de venta debe estar en el menú");
        salesReturns!.ParentItemId.Should().Be(Guid.Parse("e4000000-0000-4000-9000-000000000010"));
        salesReturns.PermissionKey.Should().Be(ERP.Domain.Kernel.Permissions.SalesPermissions.View);

        var purchaseReturns = navigation.SingleOrDefault(n => n.RoutePath == "/purchases/returns");
        purchaseReturns.Should().NotBeNull("el listado de devoluciones de compra debe estar en el menú");
        purchaseReturns!.ParentItemId.Should().Be(Guid.Parse("e3000000-0000-4000-9000-000000000010"));
        purchaseReturns.PermissionKey.Should().Be(ERP.Domain.Kernel.Permissions.PurchasePermissions.View);

        navigation.Should().NotContain(n => n.RoutePath == "/sales/returns/new");
        navigation.Should().NotContain(n => n.RoutePath.StartsWith("/sales/returns/"));
        navigation.Should().NotContain(n => n.RoutePath == "/purchases/returns/new");
        navigation.Should().NotContain(n => n.RoutePath.StartsWith("/purchases/returns/"));
        navigation.Should().NotContain(n => n.RoutePath.StartsWith("/purchases/credit-notes"));
    }

    [Fact]
    public void Modules_no_longer_contains_finance_or_reports_groups()
    {
        // MENU-MODULE-REORG-01: "finance" y "reports" se disolvieron — sus ítems se movieron a
        // Ventas/Compras/Inventario (ver Navigation_contains_finance_receivables_payables_and_
        // supplier_credits_moved_into_sales_and_purchases y
        // Navigation_contains_sales_stock_and_purchases_reports_moved_into_their_modules).
        var modules = KernelRegistry.Modules;

        modules.Should().NotContain(m => m.Code == "finance");
        modules.Should().NotContain(m => m.Code == "reports");
    }

    [Fact]
    public void Navigation_contains_finance_receivables_payables_and_supplier_credits_moved_into_sales_and_purchases()
    {
        var navigation = KernelRegistry.Navigation;
        var financePermission = ERP.Domain.Kernel.Permissions.FinancePermissions.View;

        // MENU-MODULE-REORG-01: movidas a Ventas → Operación / Compras → Operación (mismos
        // Ids/rutas). Permission alineado con el permiso real que exige la API que consume cada
        // pantalla (SalesReceivablesController / PurchasePayablesController), no
        // FinancePermissions.View.
        var receivables = navigation.SingleOrDefault(n => n.RoutePath == "/finance/receivables");
        receivables.Should().NotBeNull("cuentas por cobrar debe estar en el menú");
        receivables!.PermissionKey.Should().Be(ERP.Domain.Kernel.Permissions.SalesPermissions.View);
        receivables.GroupCode.Should().Be("sales");
        receivables.ParentItemId.Should().Be(Guid.Parse("e4000000-0000-4000-9000-000000000010"));

        var payables = navigation.SingleOrDefault(n => n.RoutePath == "/finance/payables");
        payables.Should().NotBeNull("cuentas por pagar debe estar en el menú");
        payables!.PermissionKey.Should().Be(ERP.Domain.Kernel.Permissions.PurchasePermissions.View);
        payables.GroupCode.Should().Be("purchases");
        payables.ParentItemId.Should().Be(Guid.Parse("e3000000-0000-4000-9000-000000000010"));

        var supplierCredits = navigation.SingleOrDefault(n =>
            n.RoutePath == "/finance/supplier-credits"
        );
        supplierCredits.Should().NotBeNull("créditos de proveedor debe estar en el menú");
        supplierCredits!.PermissionKey.Should().Be(financePermission);
        supplierCredits.GroupCode.Should().Be("purchases");
        supplierCredits.ParentItemId.Should().Be(Guid.Parse("e3000000-0000-4000-9000-000000000010"));

        navigation.Should().NotContain(n => n.RoutePath == "/finance/supplier-credits/:id");
        navigation
            .Should()
            .NotContain(n => n.RoutePath.StartsWith("/finance/supplier-credits/", StringComparison.Ordinal));

        // MENU-MODULE-REORG-01: "Clientes y proveedores" se aplanó — credit-terms ya no cuelga
        // de un contenedor "Clientes" (retirado), es un ítem directo del módulo.
        var creditTerms = navigation.Single(n => n.RoutePath == "/finance/credit-terms");
        creditTerms.GroupCode.Should().Be("masterdata", "credit-terms no debe moverse a otro módulo");
        creditTerms.ParentItemId.Should().BeNull();
    }

    [Fact]
    public void Navigation_contains_sales_stock_and_purchases_reports_moved_into_their_modules()
    {
        // MENU-MODULE-REORG-01: los 3 reportes salieron del módulo "reports" (retirado) y ahora
        // viven dentro de "Reportes" en su propio módulo de negocio — mismas rutas/permisos.
        var navigation = KernelRegistry.Navigation;

        var salesReport = navigation.SingleOrDefault(n => n.RoutePath == "/reportes/ventas");
        salesReport.Should().NotBeNull("el reporte de ventas debe estar en el menú");
        salesReport!.PermissionKey.Should().Be(ERP.Domain.Kernel.Permissions.SalesPermissions.View);
        salesReport.GroupCode.Should().Be("sales");
        salesReport.ParentItemId.Should().Be(Guid.Parse("e4000000-0000-4000-9000-000000000030"));

        var stockReport = navigation.SingleOrDefault(n => n.RoutePath == "/reportes/stock");
        stockReport.Should().NotBeNull("el reporte de stock debe estar en el menú");
        stockReport!.PermissionKey.Should()
            .Be(ERP.Domain.Kernel.Permissions.InventoryPermissions.StockView);
        stockReport.GroupCode.Should().Be("inventory");
        stockReport.ParentItemId.Should().Be(Guid.Parse("e2000000-0000-4000-9000-000000000030"));

        var purchasesReport = navigation.SingleOrDefault(n => n.RoutePath == "/reportes/compras");
        purchasesReport.Should().NotBeNull("el reporte de compras debe estar en el menú");
        purchasesReport!.PermissionKey.Should()
            .Be(ERP.Domain.Kernel.Permissions.PurchasePermissions.View);
        purchasesReport.GroupCode.Should().Be("purchases");
        purchasesReport.ParentItemId.Should().Be(Guid.Parse("e3000000-0000-4000-9000-000000000030"));

        navigation.Should().NotContain(n => n.RoutePath.StartsWith("/reportes/", StringComparison.Ordinal)
            && n.RoutePath != "/reportes/ventas"
            && n.RoutePath != "/reportes/stock"
            && n.RoutePath != "/reportes/compras");
    }

    [Fact]
    public void Navigation_products_module_contains_the_former_inventory_catalog_items()
    {
        // MENU-MODULE-REORG-01: "Productos y servicios" es ahora un módulo propio (antes un
        // contenedor dentro de "inventory") — mismos Ids/rutas/permisos, sin contenedor padre.
        var navigation = KernelRegistry.Navigation;

        var productsRoutes = new[]
        {
            "/inventory/items",
            "/inventory/item-types",
            "/catalog/tree",
            "/catalog/brands",
            "/catalog/attribute-groups",
            "/catalog/attribute-definitions",
        };

        foreach (var route in productsRoutes)
        {
            var item = navigation.Single(n => n.RoutePath == route);
            item.GroupCode.Should().Be("products", $"'{route}' debe pertenecer al módulo products");
            item.ParentItemId.Should().BeNull($"'{route}' no debe tener contenedor padre");
        }

        var productsModule = KernelRegistry.Modules.Single(m => m.Code == "products");
        productsModule.Icon.Should().Be("📦");
    }

    [Fact]
    public void Navigation_sales_purchases_inventory_and_caja_expose_operation_configuration_and_reports_containers()
    {
        // MENU-MODULE-REORG-01: cada módulo agrupa sus pantallas bajo Operación/Configuración/
        // Reportes (Caja no tiene Reportes: no existe pantalla de "Reporte de Caja").
        var navigation = KernelRegistry.Navigation;

        foreach (var (groupCode, expectedCount) in new[]
        {
            ("sales", 3),
            ("purchases", 3),
            ("inventory", 3),
            ("caja", 2),
        })
        {
            var containers = navigation
                .Where(n => n.GroupCode == groupCode && n.ParentItemId is null)
                .ToList();

            containers.Should().HaveCount(
                expectedCount,
                $"'{groupCode}' debe exponer exactamente {expectedCount} contenedores de primer nivel"
            );
        }
    }

    [Fact]
    public void Navigation_preferences_deep_links_point_to_the_single_operational_preferences_screen()
    {
        // MENU-MODULE-REORG-01: enlaces contextuales (query param ?tab=) a la ÚNICA pantalla real
        // de Preferencias Operativas — no crean pantallas nuevas ni duplican la existente.
        var navigation = KernelRegistry.Navigation;

        var deepLinks = new[]
        {
            "/settings/operations?tab=salesPos",
            "/settings/operations?tab=purchases",
            "/settings/operations?tab=inventory",
            "/settings/operations?tab=cash",
        };

        foreach (var route in deepLinks)
        {
            var item = navigation.Single(n => n.RoutePath == route);
            item.PermissionKey.Should()
                .Be(ERP.Domain.Kernel.Permissions.OperationalPreferencesPermissions.View);
        }

        navigation.Should().ContainSingle(n => n.RoutePath == "/settings/operations");
    }

    [Fact]
    public void Navigation_contains_settings_financial_destinations()
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
        // ADMIN-SESSIONS-ACTIVITY-POLISH-01: reordenado antes de Actividad (SortOrder 45 → 40).
        accessSessions.SortOrder.Should().Be(40);

        navigation.Should().NotContain(n => n.RoutePath == "/rrhh");

        // MENU-MODULE-REORG-01: "Destinos financieros" se movió de SortOrder 60 → 50 al
        // insertar "Facturación Electrónica" en Configuración general.
        var settingsFinancialDestinations = navigation.Single(n =>
            n.RoutePath == "/settings/financial-destinations"
        );
        settingsFinancialDestinations.GroupCode.Should().Be("settings");
        settingsFinancialDestinations.SortOrder.Should().Be(50);
    }

    [Fact]
    public void Navigation_contains_settings_communications_email_with_communications_view_permission()
    {
        var navigation = KernelRegistry.Navigation;

        var communicationsEmail = navigation.SingleOrDefault(n =>
            n.RoutePath == "/settings/communications/email"
        );
        communicationsEmail.Should().NotBeNull("correo SMTP debe estar en el menú de Configuración (COMMUNICATIONS-SETTINGS-UI-01B)");
        communicationsEmail!.GroupCode.Should().Be("settings");
        communicationsEmail
            .PermissionKey.Should()
            .Be(ERP.Domain.Kernel.Permissions.CommunicationsPermissions.View);
        communicationsEmail.SortOrder.Should().Be(70);
    }

    [Fact]
    public void Navigation_contains_settings_operational_preferences_with_settings_operations_view_permission()
    {
        var navigation = KernelRegistry.Navigation;

        var operationalPreferences = navigation.SingleOrDefault(n =>
            n.RoutePath == "/settings/operations"
        );
        operationalPreferences.Should().NotBeNull(
            "preferencias operativas debe estar en el menú de Configuración (NAV-CONFIG-FIX-01)"
        );
        operationalPreferences!.GroupCode.Should().Be("settings");
        operationalPreferences
            .PermissionKey.Should()
            .Be(ERP.Domain.Kernel.Permissions.OperationalPreferencesPermissions.View);
        operationalPreferences.SortOrder.Should().Be(80);
    }

    [Fact]
    public void Navigation_admin_users_and_roles_use_the_permission_that_actually_gates_the_screen()
    {
        // ADMIN-PERM-ALIGN-01: el ítem de menú debe exigir el mismo permiso que la pantalla/API
        // real, nunca AdminPermissions.UsersView/RolesView (legacy, sin efecto — ver
        // AdminPermissions.cs), para que "aparece en el menú" y "puede entrar" coincidan siempre.
        var navigation = KernelRegistry.Navigation;

        var users = navigation.Single(n => n.RoutePath == "/access/users");
        users.PermissionKey.Should()
            .Be(ERP.Domain.Kernel.Permissions.AccessPermissions.MembershipsView,
                "el permiso del menú debe coincidir con el que exige CompanyUserMembershipsController");

        var roles = navigation.Single(n => n.RoutePath == "/admin/roles");
        roles.PermissionKey.Should()
            .Be(ERP.Domain.Kernel.Permissions.AccessPermissions.ProfilesView,
                "el permiso del menú debe coincidir con el que exigen las mutaciones de AccessProfilesController");
    }

    [Fact]
    public void Navigation_admin_security_requires_delegation_view_permission_not_a_bare_role_check()
    {
        // ADMIN-SECURITY-SPLIT-01: antes este NavItem no tenía Permission (visible a cualquier
        // usuario autenticado) mientras la pantalla/API solo estaban protegidas por
        // [Authorize(Roles = "Admin")]. Ahora exige admin.delegation.view, igual que el resto del
        // grupo Administración.
        var navigation = KernelRegistry.Navigation;

        var delegation = navigation.Single(n => n.RoutePath == "/admin/security");
        delegation.GroupCode.Should().Be("admin");
        delegation.PermissionKey.Should()
            .Be(ERP.Domain.Kernel.Permissions.AdminPermissions.DelegationView);
        delegation.SortOrder.Should().Be(30);
    }

    [Fact]
    public void Navigation_companies_moved_from_admin_to_settings_group_with_same_permission()
    {
        // ADMIN-COMPANIES-REGROUP-01: Empresas administra datos de empresa/fiscales/branding —
        // conceptualmente Configuración, no Administración (usuarios/perfiles/delegación/
        // sesiones/actividad). Mismo Id/ruta/permiso que tenía en AdminModule.
        var navigation = KernelRegistry.Navigation;

        var companies = navigation.Single(n => n.RoutePath == "/companies");
        companies.GroupCode.Should().Be("settings");
        companies.PermissionKey.Should()
            .Be(ERP.Domain.Kernel.Permissions.SettingsPermissions.CompaniesView);
        companies.Id.Should().Be(Guid.Parse("00000000-0000-4000-8000-000000000104"));

        navigation
            .Where(n => n.GroupCode == "admin")
            .Should()
            .NotContain(n => n.RoutePath == "/companies");
    }

    [Fact]
    public void Navigation_admin_group_follows_the_requested_menu_order()
    {
        // ADMIN-SESSIONS-ACTIVITY-POLISH-01: Acceso usuarios, Perfiles, Delegación de
        // administración, Sesiones de usuario, Actividad — en ese orden.
        var expectedRouteOrder = new[]
        {
            "/access/users",
            "/admin/roles",
            "/admin/security",
            "/admin/access/sessions",
            "/admin/activity",
        };

        var actualRouteOrder = KernelRegistry
            .Navigation.Where(n => n.GroupCode == "admin")
            .OrderBy(n => n.SortOrder)
            .Select(n => n.RoutePath)
            .ToArray();

        actualRouteOrder.Should().Equal(expectedRouteOrder);
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

    [Fact]
    public void Navigation_contains_accounting_journal_entries_chart_of_accounts_and_reports_as_independent_items()
    {
        // ACCOUNTING-MODULE-MENU-STRUCTURE-FIX-10D: reemplaza el ítem único "Contabilidad" (hub
        // de tarjetas, ACCOUNTING-NAV-VISIBILITY-FIX-10C) por tres ítems planos independientes —
        // mismo criterio que ProductsModule. El hub /accounting sigue existiendo como landing
        // opcional, pero ya no es la única navegación del módulo: el menú expone las 3 pantallas
        // reales directamente, cada una con su propia ruta ya implementada.
        var navigation = KernelRegistry.Navigation;
        var accountingPermission = ERP.Domain.Kernel.Permissions.AccountingPermissions.View;

        var journalEntries = navigation.SingleOrDefault(n =>
            n.RoutePath == "/accounting/journal-entries"
        );
        journalEntries.Should().NotBeNull("Asientos contables debe estar en el menú principal");
        journalEntries!.GroupCode.Should().Be("accounting");
        journalEntries.PermissionKey.Should().Be(accountingPermission);

        var chartOfAccounts = navigation.SingleOrDefault(n =>
            n.RoutePath == "/accounting/chart-of-accounts"
        );
        chartOfAccounts.Should().NotBeNull("Plan de cuentas debe estar en el menú principal");
        chartOfAccounts!.GroupCode.Should().Be("accounting");
        chartOfAccounts.PermissionKey.Should().Be(accountingPermission);

        var reports = navigation.SingleOrDefault(n => n.RoutePath == "/accounting/reports");
        reports.Should().NotBeNull("Reportes debe estar en el menú principal");
        reports!.GroupCode.Should().Be("accounting");
        reports.PermissionKey.Should().Be(accountingPermission);

        // El hub de tarjetas ya no se registra como ítem de menú — sigue existiendo como
        // página React (AccountingHubPage.tsx, alcanzable por URL directa), pero no aparece en
        // el menú lateral: la navegación real ahora entra por los 3 ítems de arriba.
        navigation.Should().NotContain(n => n.RoutePath == "/accounting");
    }

    [Fact]
    public void Modules_contains_accounting_module()
    {
        var modules = KernelRegistry.Modules;

        modules.Should().Contain(m => m.Code == "accounting");
    }
}
