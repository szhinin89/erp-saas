using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MAPA-MENU-ERP-SSOT-01: nuevo módulo de nivel superior "SRI/Documentos electrónicos" —
// concentra el Monitor de Documentos Electrónicos (movido desde SalesModule) y la configuración
// de Facturación electrónica (movida desde SettingsModule > Facturación electrónica). Ambos son
// sobre el ciclo de vida del comprobante electrónico ante el SRI, no una operación de venta en sí
// ni una configuración general de empresa — el árbol objetivo los agrupa en un tile propio.
//
// Rutas nuevas por política de URL del ticket: /sri/electronic-documents/monitor (antes
// /electronic-documents/monitor) y /sri/configuration/electronic-invoicing (antes
// /settings/electronic-invoicing). Las rutas antiguas quedan como redirect en el frontend
// (catalogRoutes.tsx) — no se duplica ninguna pantalla ni endpoint.
[Module("sri", Icon = "📡", SortOrder = 100)]
public static class SriModule
{
    [NavItem(
        "Documentos electrónicos",
        LabelKey = "app.nav.item.sri.electronicDocumentsGroup",
        SortOrder = 10,
        Id = "51000000-0000-4000-9000-000000000010",
        PermissionsAnyCsv = ElectronicDocumentsPermissions.View
    )]
    public const string ElectronicDocumentsGroup = "/sri/electronic-documents/group";

    // Movido desde SalesModule.ElectronicDocumentsMonitor — antes derivaba su Id automáticamente
    // de module.Code="sales" + ruta; al cambiar de módulo Y de ruta (nueva política de URL) no
    // había un Id previo estable que preservar, así que se fija uno explícito nuevo aquí (mismo
    // criterio que "Electronic Invoicing"/Payables/SupplierPayments en otros módulos).
    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: Detail/Retry (ver detalle/reintentar un documento
    // varado — useElectronicDocumentsMonitor.ts) son acciones reales de esta pantalla, no
    // estaban en el catálogo asignable.
    [NavItem(
        "Monitor electrónico",
        Permission = ElectronicDocumentsPermissions.View,
        LabelKey = "app.nav.item.electronicDocuments.monitor",
        SortOrder = 10,
        Id = "51000000-0000-4000-9000-000000000001",
        ParentId = "51000000-0000-4000-9000-000000000010",
        RelatedActionPermissionsCsv = ElectronicDocumentsPermissions.Detail + ","
            + ElectronicDocumentsPermissions.Retry
    )]
    public const string ElectronicDocumentsMonitor = "/sri/electronic-documents/monitor";

    [NavItem(
        "Configuración",
        LabelKey = "app.nav.item.sri.configurationGroup",
        SortOrder = 20,
        Id = "60530be0-ce1c-4a1c-b1e8-fa5b4256bde7",
        PermissionsAnyCsv = ElectronicInvoicingPermissions.View
    )]
    public const string ConfigurationGroup = "/sri/configuration/group";

    // Movido desde SettingsModule.ElectronicInvoicing — mismo Id/permiso, nueva ruta coherente
    // con SRI (antes /settings/electronic-invoicing).
    [NavItem(
        "Facturación electrónica",
        Permission = ElectronicInvoicingPermissions.View,
        LabelKey = "app.nav.item.settings.electronicInvoicing",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000014",
        ParentId = "60530be0-ce1c-4a1c-b1e8-fa5b4256bde7",
        RelatedActionPermissionsCsv = ElectronicInvoicingPermissions.Configure
    )]
    public const string ElectronicInvoicing = "/sri/configuration/electronic-invoicing";
}
