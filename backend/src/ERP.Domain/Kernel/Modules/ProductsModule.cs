using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MENU-MODULE-REORG-01: "Productos y servicios" promovido a módulo propio (antes un
// contenedor dentro de Inventario) — el catálogo de productos/atributos es un dominio de
// negocio distinto de la operación de bodegas/kardex/transferencias. Mismos Ids/rutas/
// permisos que tenían en InventoryModule — sin cambios de API ni de lógica de negocio.
[Module("products", Icon = "📦", SortOrder = 15)]
public static class ProductsModule
{
    // NAV-HIERARCHY-UNIFY-01: contenedor "Gestión de ítems" — ningún ítem plano bajo el módulo.
    [NavItem(
        "Gestión de ítems",
        LabelKey = "app.nav.item.products.managementGroup",
        SortOrder = 10,
        Id = "911bc892-ff7a-43d0-b0ff-ff75a747adbe",
        PermissionsAnyCsv = InventoryPermissions.ItemsView + "," + CatalogPermissions.Manage
    )]
    public const string ManagementGroup = "/inventory/items/management-group";

    [NavItem(
        "Productos",
        Permission = InventoryPermissions.ItemsView,
        LabelKey = "app.nav.item.inventory.items",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000001",
        ParentId = "911bc892-ff7a-43d0-b0ff-ff75a747adbe",
        RelatedActionPermissionsCsv = InventoryPermissions.ItemsCreate + ","
            + InventoryPermissions.ItemsEdit
    )]
    public const string Items = "/inventory/items";

    [NavItem(
        "Tipos de Producto",
        Permission = InventoryPermissions.ItemsView,
        LabelKey = "app.nav.item.inventory.itemTypes",
        SortOrder = 20,
        Id = "a1000000-0000-4000-9000-000000000039",
        ParentId = "911bc892-ff7a-43d0-b0ff-ff75a747adbe"
    )]
    public const string ItemTypes = "/inventory/item-types";

    [NavItem(
        "Categorías de Productos",
        Permission = CatalogPermissions.Manage,
        LabelKey = "app.nav.item.catalog.tree",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-000000000038",
        ParentId = "911bc892-ff7a-43d0-b0ff-ff75a747adbe"
    )]
    public const string CatalogTree = "/catalog/tree";

    [NavItem(
        "Marcas",
        Permission = CatalogPermissions.Manage,
        LabelKey = "app.nav.item.catalog.brands",
        SortOrder = 40,
        Id = "a1000000-0000-4000-9000-000000000031",
        ParentId = "911bc892-ff7a-43d0-b0ff-ff75a747adbe"
    )]
    public const string Brands = "/catalog/brands";

    [NavItem(
        "Atributos de Productos",
        Permission = CatalogPermissions.Manage,
        LabelKey = "app.nav.item.catalog.attributeGroups",
        SortOrder = 50,
        Id = "a1000000-0000-4000-9000-000000000035",
        ParentId = "911bc892-ff7a-43d0-b0ff-ff75a747adbe"
    )]
    public const string AttributeGroups = "/catalog/attribute-groups";

    // No listado explícitamente en el modelo de negocio de MENU-MODULE-REORG-01 ("Atributos
    // de productos" agrupa conceptualmente ambas pantallas) — se mantiene visible para no
    // perder acceso a la pantalla real de definiciones (distinta de los grupos de atributos).
    [NavItem(
        "Definiciones de Atributos",
        Permission = CatalogPermissions.Manage,
        LabelKey = "app.nav.item.catalog.attributeDefinitions",
        SortOrder = 60,
        Id = "a1000000-0000-4000-9000-000000000036",
        ParentId = "911bc892-ff7a-43d0-b0ff-ff75a747adbe"
    )]
    public const string AttributeDefinitions = "/catalog/attribute-definitions";

    // NAV-HIERARCHY-UNIFY-01: contenedor "Precios" — categoría propia, hermana de Gestión de
    // ítems.
    [NavItem(
        "Precios",
        LabelKey = "app.nav.item.products.pricingGroup",
        SortOrder = 70,
        Id = "c79860c8-af6b-4c93-9411-12721734bfad",
        PermissionsAnyCsv = PricingPermissions.View
    )]
    public const string PricingGroup = "/pricing/group";

    // NAVIGATION-OPERATING-CYCLES-03: movido desde MasterDataModule — aplica por igual a precios
    // de venta a clientes y costos de proveedor, pero el catálogo de precios en sí es un dato de
    // producto. Mismo Id/ruta/permiso.
    [NavItem(
        "Listas de Precios",
        Permission = PricingPermissions.View,
        LabelKey = "app.nav.item.pricing.priceLists",
        SortOrder = 70,
        Id = "b1000000-0000-4000-9000-000000000001",
        ParentId = "c79860c8-af6b-4c93-9411-12721734bfad"
    )]
    public const string PriceLists = "/pricing";
}
