using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// NAVIGATION-OPERATING-CYCLES-03: nuevo módulo — concentra el ciclo cliente (antes disperso
// entre "masterdata" y "sales"). Mismos Ids/rutas/permisos que tenían en sus módulos de origen.
[Module("customers", Icon = "👥", SortOrder = 10)]
public static class CustomersModule
{
    // NAV-HIERARCHY-UNIFY-01: contenedor "Gestión de clientes" — todo ítem de primer nivel del
    // módulo debe ser una categoría (Nivel 2); Clientes pasa a ser su único hijo.
    [NavItem(
        "Gestión de clientes",
        LabelKey = "app.nav.item.customers.managementGroup",
        SortOrder = 5,
        Id = "8f31a57d-ed70-4e09-8031-393375bf40a5",
        PermissionsAnyCsv = MasterDataPermissions.BusinessPartnersView
    )]
    public const string ManagementGroup = "/masterdata/customers/management-group";

    // Movido desde MasterDataModule — mismo Id/ruta/permiso.
    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: Create/Update/Disable/ConfigureCompany
    // (useMasterDataCustomersPage.ts canCreate/canUpdate/canDisable/canConfigure,
    // MasterDataBusinessPartnerDetailPage.tsx canUpdate/canDisable) son acciones reales de esta
    // pantalla y no estaban en el catálogo asignable — ningún perfil no-Admin podía recibirlas.
    [NavItem(
        "Clientes",
        Permission = MasterDataPermissions.BusinessPartnersView,
        LabelKey = "app.nav.item.masterdata.customers",
        SortOrder = 5,
        Id = "a1000000-0000-4000-9000-000000000101",
        ParentId = "8f31a57d-ed70-4e09-8031-393375bf40a5",
        RelatedActionPermissionsCsv = MasterDataPermissions.BusinessPartnersCreate + ","
            + MasterDataPermissions.BusinessPartnersUpdate + ","
            + MasterDataPermissions.BusinessPartnersDisable + ","
            + MasterDataPermissions.BusinessPartnersConfigureCompany
    )]
    public const string Customers = "/masterdata/customers";

    // Movido desde SalesModule (antes hijo del contenedor "Ventas → Operación") — mismo Id/ruta/
    // permiso; ahora ítem plano de Clientes, no contenedor.
    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: se documentó como "solo lectura" en
    // NAVIGATION-OPERATING-CYCLES-03, pero AccountsReceivablePage.tsx sí tiene una acción de
    // escritura real — "Registrar cobro" (RegisterCollectionModal.tsx →
    // FinancePaymentsController.RegisterCollection, perm:finance.create) — que no estaba en el
    // catálogo asignable. Se corrige, la pantalla no es de solo lectura.
    // NAV-HIERARCHY-UNIFY-01: contenedor "Cuentas por cobrar" — mismo criterio que Gestión de
    // clientes, ningún ítem plano bajo el módulo.
    [NavItem(
        "Cuentas por cobrar",
        LabelKey = "app.nav.item.customers.receivablesGroup",
        SortOrder = 10,
        Id = "aea32545-8fbc-4c99-9495-6d2873282dd9",
        PermissionsAnyCsv = SalesPermissions.View
    )]
    public const string ReceivablesGroup = "/finance/receivables/group";

    [NavItem(
        "Cuentas por cobrar",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.finance.receivables",
        SortOrder = 10,
        Id = "f6000000-0000-4000-9000-000000000001",
        ParentId = "aea32545-8fbc-4c99-9495-6d2873282dd9",
        RelatedActionPermissionsCsv = FinancePermissions.Create
    )]
    public const string Receivables = "/finance/receivables";
}
