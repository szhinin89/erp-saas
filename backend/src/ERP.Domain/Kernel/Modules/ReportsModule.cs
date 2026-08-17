using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// Code = "reports" (no "reportes"): el frontend ya inyecta un grupo sintético
// "Reportes" (frontend/src/nav/navConfig.ts ensureReportsGroup) cuando no
// encuentra un grupo con id "reports" en el sessionMenuDto — el id del grupo
// mapeado es el Code del backend (mapSessionMenuToNavGroups: `id: g.code`).
// Usar "reports" hace que esa guarda detecte el grupo real y no duplique la
// sección al inyectar el fallback sintético.
[Module("reports", Icon = "📊", SortOrder = 55)]
public static class ReportsModule
{
    [NavItem(
        "Ventas",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.reportes.ventas",
        SortOrder = 10,
        Id = "f7000000-0000-4000-9000-000000000001"
    )]
    public const string Sales = "/reportes/ventas";

    [NavItem(
        "Stock",
        Permission = InventoryPermissions.StockView,
        LabelKey = "app.nav.item.reportes.stock",
        SortOrder = 20,
        Id = "f7000000-0000-4000-9000-000000000002"
    )]
    public const string Stock = "/reportes/stock";

    [NavItem(
        "Compras",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.reportes.compras",
        SortOrder = 30,
        Id = "f7000000-0000-4000-9000-000000000003"
    )]
    public const string Purchases = "/reportes/compras";
}
