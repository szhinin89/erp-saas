using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MENU-MODULE-REORG-01: reorganizado en Operación/Configuración/Reportes. Reporte de Ventas
// (antes ReportsModule) se movió aquí — mismos Ids/rutas/permisos. Facturación Electrónica
// (config) se movió a Configuración general (SettingsModule) por ser transversal; el Monitor de
// Documentos Electrónicos se mantiene aquí como operación de Ventas (revisión diaria de
// comprobantes emitidos).
// NAVIGATION-OPERATING-CYCLES-03: Cuentas por cobrar se movió a CustomersModule (ciclo cliente,
// no ciclo venta). Caja se fusionó aquí desde CajaModule (antes grupo propio) — Ventas/POS y
// Caja son la misma operación de piso de venta; mismos Ids/rutas/permisos que tenía CajaModule.
[Module("sales", Icon = "💰", SortOrder = 40)]
public static class SalesModule
{
    // ── Ventas (MENU-FINAL-STRUCTURE-01: subgrupo renombrado de "Operación" al mismo
    // nombre del módulo) ─────────────────────────────────────────────
    [NavItem(
        "Ventas",
        LabelKey = "app.nav.item.sales.operation",
        SortOrder = 10,
        Id = "e4000000-0000-4000-9000-000000000010",
        PermissionsAnyCsv = SalesPermissions.View + "," + ElectronicDocumentsPermissions.View
    )]
    public const string OperationGroup = "/sales/operation-group";

    // ADMIN-PERMISSIONS-SSOT-KERNEL-02: Create/Update como acciones relacionadas — a diferencia de
    // CustomersModule.Receivables (mismo permiso base, pero solo lectura), esta pantalla sí crea/
    // edita ventas.
    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: Ride.View/Regenerate (ver/regenerar el RIDE de la
    // factura — useRideActions.ts, consumido desde SalesPage.tsx) tampoco estaban en el catálogo
    // asignable; se agregan aquí (pantalla donde realmente se usan) en vez de crear un NavItem
    // propio para un permiso transversal de borde HTTP sin pantalla propia.
    [NavItem(
        "Facturas de venta / Punto de venta",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.sales.invoices",
        SortOrder = 10,
        Id = "d1000000-0000-4000-9000-000000000001",
        ParentId = "e4000000-0000-4000-9000-000000000010",
        RelatedActionPermissionsCsv = SalesPermissions.Create + "," + SalesPermissions.Update
            + "," + RidePermissions.View + "," + RidePermissions.Regenerate
    )]
    public const string Invoices = "/sales";

    [NavItem(
        "Devoluciones de venta",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.sales.returns",
        SortOrder = 20,
        Id = "d1000000-0000-4000-9000-000000000003",
        ParentId = "e4000000-0000-4000-9000-000000000010"
    )]
    public const string Returns = "/sales/returns";

    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: Detail/Retry (ver detalle/reintentar un documento
    // varado — useElectronicDocumentsMonitor.ts) son acciones reales de esta pantalla, no
    // estaban en el catálogo asignable.
    [NavItem(
        "Electronic Documents Monitor",
        Permission = ElectronicDocumentsPermissions.View,
        LabelKey = "app.nav.item.electronicDocuments.monitor",
        SortOrder = 40,
        ParentId = "e4000000-0000-4000-9000-000000000010",
        RelatedActionPermissionsCsv = ElectronicDocumentsPermissions.Detail + ","
            + ElectronicDocumentsPermissions.Retry
    )]
    public const string ElectronicDocumentsMonitor = "/electronic-documents/monitor";

    // ── Caja (fusionado desde CajaModule, sin Reportes: no existe pantalla de "Reporte de
    // Caja" en el sistema — regla explícita: no crear entradas falsas) ────────────────
    [NavItem(
        "Caja",
        LabelKey = "app.nav.item.caja.operation",
        SortOrder = 15,
        Id = "f5000000-0000-4000-9000-000000000010",
        PermissionsAnyCsv = CajaPermissions.View
    )]
    public const string CajaGroup = "/cash/operation-group";

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
    public const string CajaSessions = "/cash";

    // ── Configuración ────────────────────────────────────────────────
    [NavItem(
        "Configuración",
        LabelKey = "app.nav.item.sales.configuration",
        SortOrder = 20,
        Id = "e4000000-0000-4000-9000-000000000020",
        PermissionsAnyCsv = SalesPermissions.View + "," + OperationalPreferencesPermissions.View
            + "," + CajaPermissions.View
    )]
    public const string ConfigurationGroup = "/sales/configuration-group";

    [NavItem(
        "Métodos de Pago",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.sales.paymentMethods",
        SortOrder = 10,
        Id = "d1000000-0000-4000-9000-000000000002",
        ParentId = "e4000000-0000-4000-9000-000000000020"
    )]
    public const string PaymentMethods = "/sales/payment-methods";

    // Enlace contextual al tab "salesPos" de la pantalla única de Preferencias Operativas
    // (/settings/operations) — no duplica la pantalla, solo la referencia con deep-link.
    [NavItem(
        "Preferencias de Ventas/POS",
        Permission = OperationalPreferencesPermissions.View,
        LabelKey = "app.nav.item.sales.posPreferences",
        SortOrder = 20,
        Id = "e4000000-0000-4000-9000-000000000021",
        ParentId = "e4000000-0000-4000-9000-000000000020"
    )]
    public const string PosPreferences = "/settings/operations?tab=salesPos";

    // Cajas registradoras (fusionado desde CajaModule.ConfigurationGroup) — permission alineado
    // con el GET/listado real de CashRegisterController (perm:caja.view): create/update/enable/
    // disable siguen protegidos por CajaPermissions.Manage a nivel de API.
    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: Manage (create/update/enable/disable —
    // useCashRegistersPage.ts canManage) es real y no estaba en el catálogo asignable pese a que
    // el comentario anterior ya documentaba su existencia a nivel de API.
    [NavItem(
        "Cajas registradoras",
        Permission = CajaPermissions.View,
        LabelKey = "app.nav.item.caja.registers",
        SortOrder = 30,
        Id = "f5000000-0000-4000-9000-000000000002",
        ParentId = "e4000000-0000-4000-9000-000000000020",
        RelatedActionPermissionsCsv = CajaPermissions.Manage
    )]
    public const string CajaRegisters = "/cash/registers";

    // Enlace contextual al tab "cash" de la pantalla única de Preferencias Operativas
    // (/settings/operations) — no duplica la pantalla, solo la referencia con deep-link.
    [NavItem(
        "Preferencias de Caja",
        Permission = OperationalPreferencesPermissions.View,
        LabelKey = "app.nav.item.caja.preferences",
        SortOrder = 40,
        Id = "f5000000-0000-4000-9000-000000000021",
        ParentId = "e4000000-0000-4000-9000-000000000020"
    )]
    public const string CajaPreferences = "/settings/operations?tab=cash";

    // ── Reportes ─────────────────────────────────────────────────────
    [NavItem(
        "Reportes",
        LabelKey = "app.nav.item.sales.reports",
        SortOrder = 30,
        Id = "e4000000-0000-4000-9000-000000000030",
        PermissionsAnyCsv = SalesPermissions.View
    )]
    public const string ReportsGroup = "/sales/reports-group";

    // Movido desde ReportsModule (antes /reportes/ventas en el grupo "reports" separado) —
    // mismo Id/ruta/permiso, ahora dentro de Ventas → Reportes.
    [NavItem(
        "Reporte de Ventas",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.reportes.ventas",
        SortOrder = 10,
        Id = "f7000000-0000-4000-9000-000000000001",
        ParentId = "e4000000-0000-4000-9000-000000000030"
    )]
    public const string SalesReport = "/reportes/ventas";
}
