using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("inventory", Icon = "📦", SortOrder = 20)]
public static class InventoryModule
{
    // ── Contenedor: Productos y servicios ────────────────────────────
    // MENU-UX-RENAME-01: label de negocio "Productos y servicios" (antes "Catálogo" vía
    // LabelKey) — solo texto visible, mismo Id/ruta/permisos.
    [NavItem(
        "Productos y servicios",
        LabelKey = "app.nav.item.inventory.catalog",
        SortOrder = 10,
        Id = "e2000000-0000-4000-9000-000000000001",
        PermissionsAnyCsv = InventoryPermissions.ItemsView + "," + CatalogPermissions.Manage
    )]
    public const string ProductsGroup = "/inventory/products-group";

    // MENU-UX-RENAME-01: label de negocio "Productos" (antes "Ítems") — solo texto visible.
    [NavItem(
        "Productos",
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

    // MENU-UX-RENAME-01: label de negocio "Categorías de productos" (antes "Árbol de catálogo").
    [NavItem(
        "Categorías de productos",
        Permission = CatalogPermissions.Manage,
        LabelKey = "app.nav.item.catalog.tree",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-000000000038",
        ParentId = "e2000000-0000-4000-9000-000000000001"
    )]
    public const string CatalogTree = "/catalog/tree";

    // MENU-UX-RENAME-01: label de negocio "Atributos de productos" (antes "Grupo de atributos") —
    // la pantalla gestiona definiciones de atributos reutilizables (ej. Color, Talla), no genera
    // variantes por sí misma (eso lo hace VariantsSection en el detalle del ítem).
    [NavItem(
        "Atributos de productos",
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
    // MENU-UX-RENAME-01: label unificado "Kardex / Movimientos de Inventario" (menú y título
    // de pantalla ahora coinciden — antes el menú decía "Kardex" y la pantalla "Centro de
    // Investigación de Inventario").
    [NavItem(
        "Kardex / Movimientos de Inventario",
        Permission = InventoryPermissions.StockView,
        LabelKey = "app.nav.item.inventory.kardex",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-000000000040"
    )]
    public const string Kardex = "/inventory/kardex";

    // ── Transferencias entre bodegas: acceso operativo directo (sin contenedor),
    // mismo patrón que Kardex — después de Kardex en el menú (P1-INVENTORY-WAREHOUSE-TRANSFER-UI-01).
    // Permission = StockManage (no StockView): sin permiso de gestión el usuario no puede crear
    // ni confirmar nada en esta pantalla — no existe un modo de solo lectura que justifique
    // mostrarla con StockView únicamente.
    [NavItem(
        "Transferencias entre bodegas",
        Permission = InventoryPermissions.StockManage,
        LabelKey = "app.nav.item.inventory.transfers",
        SortOrder = 40
    )]
    public const string Transfers = "/inventory/transfers";
}
