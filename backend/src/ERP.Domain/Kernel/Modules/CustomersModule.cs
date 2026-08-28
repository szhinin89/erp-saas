using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// NAVIGATION-OPERATING-CYCLES-03: nuevo módulo — concentra el ciclo cliente (antes disperso
// entre "masterdata" y "sales"). Mismos Ids/rutas/permisos que tenían en sus módulos de origen.
[Module("customers", Icon = "👥", SortOrder = 10)]
public static class CustomersModule
{
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
    [NavItem(
        "Cuentas por cobrar",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.finance.receivables",
        SortOrder = 10,
        Id = "f6000000-0000-4000-9000-000000000001",
        RelatedActionPermissionsCsv = FinancePermissions.Create
    )]
    public const string Receivables = "/finance/receivables";
}
