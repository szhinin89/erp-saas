using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// NAVIGATION-OPERATING-CYCLES-03: nuevo módulo — concentra el ciclo cliente (antes disperso
// entre "masterdata" y "sales"). Mismos Ids/rutas/permisos que tenían en sus módulos de origen.
[Module("customers", Icon = "👥", SortOrder = 10)]
public static class CustomersModule
{
    // Movido desde MasterDataModule — mismo Id/ruta/permiso.
    [NavItem(
        "Clientes",
        Permission = MasterDataPermissions.BusinessPartnersView,
        LabelKey = "app.nav.item.masterdata.customers",
        SortOrder = 5,
        Id = "a1000000-0000-4000-9000-000000000101"
    )]
    public const string Customers = "/masterdata/customers";

    // Movido desde SalesModule (antes hijo del contenedor "Ventas → Operación") — mismo Id/ruta/
    // permiso; ahora ítem plano de Clientes, no contenedor.
    [NavItem(
        "Cuentas por cobrar",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.finance.receivables",
        SortOrder = 10,
        Id = "f6000000-0000-4000-9000-000000000001"
    )]
    public const string Receivables = "/finance/receivables";
}
