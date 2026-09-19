using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MAPA-MENU-ERP-SSOT-01: nuevo módulo de nivel superior "Tesorería" — concentra Caja (movida
// desde SalesModule, donde vivía fusionada desde CajaModule por NAVIGATION-OPERATING-CYCLES-03) y
// Bancos. El árbol objetivo agrupa todo lo financiero/de tesorería bajo un tile propio, separado
// de Ventas y de Configuración. Mismos Ids que ya tenían en sus módulos de origen — solo cambia a
// qué [Module] pertenecen.
//
// Rutas nuevas por política de URL del ticket (coherentes con el módulo, antes vivían bajo
// /cash): /treasury/cash (antes /cash), /treasury/cash/registers (antes /cash/registers). Las
// rutas antiguas quedan como redirect en el frontend (catalogRoutes.tsx) — no se duplica ninguna
// pantalla ni endpoint.
[Module("treasury", Icon = "🏦", SortOrder = 80)]
public static class TreasuryModule
{
    [NavItem(
        "Caja",
        LabelKey = "app.nav.item.caja.operation",
        SortOrder = 10,
        Id = "f5000000-0000-4000-9000-000000000010",
        PermissionsAnyCsv = CajaPermissions.View
    )]
    public const string CajaGroup = "/treasury/cash/group";

    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: Open/Close/Record (abrir/cerrar turno, registrar
    // movimiento — CajaPage.tsx/useCajaPage.ts → CashSessionController) son acciones reales
    // distintas de View y no estaban en el catálogo asignable.
    [NavItem(
        "Turno de Caja",
        Permission = CajaPermissions.View,
        LabelKey = "app.nav.item.caja.sessions",
        SortOrder = 10,
        Id = "f5000000-0000-4000-9000-000000000001",
        ParentId = "f5000000-0000-4000-9000-000000000010",
        RelatedActionPermissionsCsv = CajaPermissions.Open + "," + CajaPermissions.Close + ","
            + CajaPermissions.Record
    )]
    public const string CajaSessions = "/treasury/cash";

    // TREASURY-CASH-MOVEMENT-REASONS-ADMIN-03: catálogo administrable de motivos de movimiento
    // manual de caja (CashMovementReason, SSOT dinámico) — hermano de "Turno de Caja", no bajo
    // "Configuración" porque no es una preferencia sino un catálogo con su propio CRUD.
    [NavItem(
        "Motivos de movimientos",
        Permission = CajaPermissions.View,
        LabelKey = "app.nav.item.caja.movementReasons",
        SortOrder = 15,
        Id = "f5000000-0000-4000-9000-000000000011",
        ParentId = "f5000000-0000-4000-9000-000000000010",
        RelatedActionPermissionsCsv = CajaPermissions.Manage
    )]
    public const string CashMovementReasons = "/treasury/cash/movement-reasons";

    [NavItem(
        "Configuración",
        LabelKey = "app.nav.item.caja.configuration",
        SortOrder = 20,
        Id = "f5000000-0000-4000-9000-000000000030",
        ParentId = "f5000000-0000-4000-9000-000000000010",
        PermissionsAnyCsv = CajaPermissions.View + "," + OperationalPreferencesPermissions.View
    )]
    public const string CashConfigurationGroup = "/treasury/cash/configuration-group";

    // Cajas registradoras (fusionado desde CajaModule.ConfigurationGroup originalmente) —
    // permission alineado con el GET/listado real de CashRegisterController (perm:caja.view):
    // create/update/enable/disable siguen protegidos por CajaPermissions.Manage a nivel de API.
    [NavItem(
        "Cajas registradoras",
        Permission = CajaPermissions.View,
        LabelKey = "app.nav.item.caja.registers",
        SortOrder = 10,
        Id = "f5000000-0000-4000-9000-000000000002",
        ParentId = "f5000000-0000-4000-9000-000000000030",
        RelatedActionPermissionsCsv = CajaPermissions.Manage
    )]
    public const string CajaRegisters = "/treasury/cash/registers";

    // Enlace contextual al tab "cash" de la pantalla única de Preferencias Operativas
    // (/settings/operations) — no duplica la pantalla, solo la referencia con deep-link.
    [NavItem(
        "Preferencias de Caja",
        Permission = OperationalPreferencesPermissions.View,
        LabelKey = "app.nav.item.caja.preferences",
        SortOrder = 20,
        Id = "f5000000-0000-4000-9000-000000000021",
        ParentId = "f5000000-0000-4000-9000-000000000030"
    )]
    public const string CajaPreferences = "/settings/operations?tab=cash";

    // FINANCIAL-DESTINATION-TO-BANK-ACCOUNT-MIGRATION-01: "Destinos financieros" retirado —
    // legacy treasury destination fue eliminado como intermediario; CompanyBankAccount es ahora el
    // único SSOT de cuentas bancarias. El grupo "Bancos" queda con un solo hijo.
    [NavItem(
        "Bancos",
        LabelKey = "app.nav.item.treasury.banksGroup",
        SortOrder = 30,
        Id = "f5000000-0000-4000-9000-000000000040",
        PermissionsAnyCsv = TreasuryPermissions.BankAccountsView
    )]
    public const string BanksGroup = "/treasury/banks/group";

    // TREASURY-BANK-ACCOUNTS-01: cuentas bancarias de empresa (CompanyBankAccount), catálogo
    // maestro Bank existente (BANK-CATALOG-01) — CRUD básico + activar/desactivar.
    [NavItem(
        "Cuentas bancarias",
        Permission = TreasuryPermissions.BankAccountsView,
        LabelKey = "app.nav.item.treasury.bankAccounts",
        SortOrder = 20,
        Id = "f5000000-0000-4000-9000-000000000042",
        ParentId = "f5000000-0000-4000-9000-000000000040",
        RelatedActionPermissionsCsv = TreasuryPermissions.BankAccountsCreate + ","
            + TreasuryPermissions.BankAccountsUpdate + ","
            + TreasuryPermissions.BankAccountsManage
    )]
    public const string BankAccounts = "/treasury/banks/accounts";
}
