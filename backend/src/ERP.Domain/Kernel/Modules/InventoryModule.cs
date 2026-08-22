using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MENU-MODULE-REORG-01: reorganizado en Operación/Configuración/Reportes — el catálogo de
// productos se movió a ProductsModule (módulo propio "Productos y servicios").
[Module("inventory", Icon = "🏭", SortOrder = 20)]
public static class InventoryModule
{
    // ── Inventario (MENU-FINAL-STRUCTURE-01: subgrupo renombrado de "Operación" al mismo
    // nombre del módulo) ─────────────────────────────────────────────
    [NavItem(
        "Inventario",
        LabelKey = "app.nav.item.inventory.operation",
        SortOrder = 10,
        Id = "e2000000-0000-4000-9000-000000000010",
        PermissionsAnyCsv = InventoryPermissions.WarehousesView
            + ","
            + InventoryPermissions.StockView
            + ","
            + InventoryPermissions.StockManage
            + ","
            + InventoryPermissions.AdjustmentsView
    )]
    public const string OperationGroup = "/inventory/operation-group";

    [NavItem(
        "Bodegas",
        Permission = InventoryPermissions.WarehousesView,
        LabelKey = "app.nav.item.inventory.warehouses",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000002",
        ParentId = "e2000000-0000-4000-9000-000000000010"
    )]
    public const string Warehouses = "/inventory/warehouses";

    // MENU-FINAL-STRUCTURE-01: renombrado de negocio "Historial de Existencias" (antes
    // "Kardex / Movimientos de Inventario") — mismo Id/ruta/permiso, misma pantalla.
    [NavItem(
        "Historial de Existencias",
        Permission = InventoryPermissions.StockView,
        LabelKey = "app.nav.item.inventory.kardex",
        SortOrder = 20,
        Id = "a1000000-0000-4000-9000-000000000040",
        ParentId = "e2000000-0000-4000-9000-000000000010"
    )]
    public const string Kardex = "/inventory/kardex";

    [NavItem(
        "Transferencias entre bodegas",
        Permission = InventoryPermissions.StockManage,
        LabelKey = "app.nav.item.inventory.transfers",
        SortOrder = 30,
        ParentId = "e2000000-0000-4000-9000-000000000010"
    )]
    public const string Transfers = "/inventory/transfers";

    // INVENTORY-ADJUSTMENTS-02: ajustes de inventario (Ingreso/Egreso) con líneas por ítem y
    // motivo administrable — mismo grupo "Inventario" que Bodegas/Historial/Transferencias.
    [NavItem(
        "Ajustes de inventario",
        Permission = InventoryPermissions.AdjustmentsView,
        LabelKey = "app.nav.item.inventory.adjustments",
        SortOrder = 40,
        ParentId = "e2000000-0000-4000-9000-000000000010"
    )]
    public const string Adjustments = "/inventory/adjustments";

    // ── Configuración ────────────────────────────────────────────────
    [NavItem(
        "Configuración",
        LabelKey = "app.nav.item.inventory.configuration",
        SortOrder = 20,
        Id = "e2000000-0000-4000-9000-000000000020",
        PermissionsAnyCsv = OperationalPreferencesPermissions.View
            + ","
            + InventoryPermissions.AdjustmentReasonsView
    )]
    public const string ConfigurationGroup = "/inventory/configuration-group";

    // Enlace contextual al tab "inventory" de la pantalla única de Preferencias Operativas
    // (/settings/operations) — no duplica la pantalla, solo la referencia con deep-link.
    [NavItem(
        "Preferencias de Inventario",
        Permission = OperationalPreferencesPermissions.View,
        LabelKey = "app.nav.item.inventory.preferences",
        SortOrder = 10,
        Id = "e2000000-0000-4000-9000-000000000021",
        ParentId = "e2000000-0000-4000-9000-000000000020"
    )]
    public const string Preferences = "/settings/operations?tab=inventory";

    // INVENTORY-ADJUSTMENTS-02: catálogo administrable de motivos de ajuste de inventario.
    [NavItem(
        "Motivos de ajuste",
        Permission = InventoryPermissions.AdjustmentReasonsView,
        LabelKey = "app.nav.item.inventory.adjustment-reasons",
        SortOrder = 20,
        ParentId = "e2000000-0000-4000-9000-000000000020"
    )]
    public const string AdjustmentReasons = "/inventory/adjustment-reasons";

    // ── Reportes ─────────────────────────────────────────────────────
    [NavItem(
        "Reportes",
        LabelKey = "app.nav.item.inventory.reports",
        SortOrder = 30,
        Id = "e2000000-0000-4000-9000-000000000030",
        PermissionsAnyCsv = InventoryPermissions.StockView
    )]
    public const string ReportsGroup = "/inventory/reports-group";

    // Movido desde ReportsModule (antes /reportes/stock en el grupo "reports" separado) —
    // mismo Id/ruta/permiso, ahora dentro de Inventario → Reportes.
    [NavItem(
        "Reporte de Inventario",
        Permission = InventoryPermissions.StockView,
        LabelKey = "app.nav.item.reportes.stock",
        SortOrder = 10,
        Id = "f7000000-0000-4000-9000-000000000002",
        ParentId = "e2000000-0000-4000-9000-000000000030"
    )]
    public const string StockReport = "/reportes/stock";
}
