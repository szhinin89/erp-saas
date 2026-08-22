using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MENU-MODULE-REORG-01: reorganizado en Operación/Configuración — sin Reportes: no existe
// pantalla de "Reporte de Caja" en el sistema (regla explícita: no crear entradas falsas).
[Module("caja", Icon = "💵", SortOrder = 45)]
public static class CajaModule
{
    // ── Operación ────────────────────────────────────────────────────
    [NavItem(
        "Operación",
        LabelKey = "app.nav.item.caja.operation",
        SortOrder = 10,
        Id = "f5000000-0000-4000-9000-000000000010",
        PermissionsAnyCsv = CajaPermissions.View
    )]
    public const string OperationGroup = "/cash/operation-group";

    [NavItem(
        "Turno de Caja",
        Permission = CajaPermissions.View,
        LabelKey = "app.nav.item.caja.sessions",
        SortOrder = 10,
        Id = "f5000000-0000-4000-9000-000000000001",
        ParentId = "f5000000-0000-4000-9000-000000000010"
    )]
    public const string Sessions = "/cash";

    // ── Configuración ────────────────────────────────────────────────
    // Permission alineado con el GET/listado real de CashRegisterController (perm:caja.view):
    // create/update/enable/disable siguen protegidos por CajaPermissions.Manage a nivel de API.
    [NavItem(
        "Configuración",
        LabelKey = "app.nav.item.caja.configuration",
        SortOrder = 20,
        Id = "f5000000-0000-4000-9000-000000000020",
        PermissionsAnyCsv = CajaPermissions.View + "," + OperationalPreferencesPermissions.View
    )]
    public const string ConfigurationGroup = "/cash/configuration-group";

    [NavItem(
        "Cajas registradoras",
        Permission = CajaPermissions.View,
        LabelKey = "app.nav.item.caja.registers",
        SortOrder = 10,
        Id = "f5000000-0000-4000-9000-000000000002",
        ParentId = "f5000000-0000-4000-9000-000000000020"
    )]
    public const string Registers = "/cash/registers";

    // Enlace contextual al tab "cash" de la pantalla única de Preferencias Operativas
    // (/settings/operations) — no duplica la pantalla, solo la referencia con deep-link.
    [NavItem(
        "Preferencias de Caja",
        Permission = OperationalPreferencesPermissions.View,
        LabelKey = "app.nav.item.caja.preferences",
        SortOrder = 20,
        Id = "f5000000-0000-4000-9000-000000000021",
        ParentId = "f5000000-0000-4000-9000-000000000020"
    )]
    public const string Preferences = "/settings/operations?tab=cash";
}
