using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("inventory", Icon = "📦", SortOrder = 20)]
public static class InventoryModule
{
    // ── Contenedor: Productos ──────────────────────────────────────
    [NavItem(
        "Productos",
        LabelKey = "app.nav.item.inventory.catalog",
        SortOrder = 10,
        Id = "e2000000-0000-4000-9000-000000000001",
        PermissionsAnyCsv = InventoryPermissions.ItemsView + "," + CatalogPermissions.Manage
    )]
    public const string ProductsGroup = "/inventory/products-group";

    [NavItem(
        "Items",
        Permission = InventoryPermissions.ItemsView,
        LabelKey = "app.nav.item.inventory.items",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000001",
        ParentId = "e2000000-0000-4000-9000-000000000001"
    )]
    public const string Items = "/inventory/items";

    [NavItem(
        "Tipos de Ítem",
        Permission = InventoryPermissions.ItemsView,
        LabelKey = "app.nav.item.inventory.itemTypes",
        SortOrder = 15,
        Id = "a1000000-0000-4000-9000-000000000039",
        ParentId = "e2000000-0000-4000-9000-000000000001"
    )]
    public const string ItemTypes = "/inventory/item-types";

    [NavItem(
        "Brands",
        Permission = CatalogPermissions.Manage,
        LabelKey = "app.nav.item.catalog.brands",
        SortOrder = 20,
        Id = "a1000000-0000-4000-9000-000000000031",
        ParentId = "e2000000-0000-4000-9000-000000000001"
    )]
    public const string Brands = "/catalog/brands";

    [NavItem(
        "Catalog Tree",
        Permission = CatalogPermissions.Manage,
        LabelKey = "app.nav.item.catalog.tree",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-000000000038",
        ParentId = "e2000000-0000-4000-9000-000000000001"
    )]
    public const string CatalogTree = "/catalog/tree";

    [NavItem(
        "Attribute Groups",
        Permission = CatalogPermissions.Manage,
        LabelKey = "app.nav.item.catalog.attributeGroups",
        SortOrder = 40,
        Id = "a1000000-0000-4000-9000-000000000035",
        ParentId = "e2000000-0000-4000-9000-000000000001"
    )]
    public const string AttributeGroups = "/catalog/attribute-groups";

    [NavItem(
        "Attribute Definitions",
        Permission = CatalogPermissions.Manage,
        LabelKey = "app.nav.item.catalog.attributeDefinitions",
        SortOrder = 50,
        Id = "a1000000-0000-4000-9000-000000000036",
        ParentId = "e2000000-0000-4000-9000-000000000001"
    )]
    public const string AttributeDefinitions = "/catalog/attribute-definitions";

    // ── Contenedor: Almacenes ──────────────────────────────────────
    [NavItem(
        "Almacenes",
        LabelKey = "app.nav.item.inventory.warehouses",
        SortOrder = 20,
        Id = "e2000000-0000-4000-9000-000000000002",
        PermissionsAnyCsv = InventoryPermissions.WarehousesView
    )]
    public const string WarehousesGroup = "/inventory/warehouses-group";

    [NavItem(
        "Warehouses",
        Permission = InventoryPermissions.WarehousesView,
        LabelKey = "app.nav.item.inventory.warehouses",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000002",
        ParentId = "e2000000-0000-4000-9000-000000000002"
    )]
    public const string Warehouses = "/inventory/warehouses";

    // ── Kardex: acceso operativo directo (sin contenedor) ───────────
    [NavItem(
        "Kardex",
        Permission = InventoryPermissions.StockView,
        LabelKey = "app.nav.item.inventory.kardex",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-000000000040"
    )]
    public const string Kardex = "/inventory/kardex";
}
