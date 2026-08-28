using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("admin", Icon = "🛡️", SortOrder = 60)]
public static class AdminModule
{
    [NavItem(
        "Usuarios",
        Permission = AccessPermissions.MembershipsView,
        LabelKey = "app.nav.item.admin.users",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000008"
    )]
    public const string Users = "/access/users";

    [NavItem(
        "Roles",
        Permission = AccessPermissions.ProfilesView,
        LabelKey = "app.nav.item.admin.roles",
        SortOrder = 20,
        Id = "a1000000-0000-4000-9000-000000000007"
    )]
    public const string Roles = "/admin/roles";

    // ADMINISTRATION-CLEAN-ACCESS-01: extraída de la sección de permisos que vivía embebida en el
    // mismo formulario de Perfiles (ProfilesPage.tsx) — pantalla propia de responsabilidad única.
    // Reutiliza el permiso ya existente (mismo que ya exige GET/PUT .../profiles/{id}/permissions)
    // y los endpoints ya existentes de AccessProfilesController — sin cambios de backend/API.
    [NavItem(
        "Asignación de permisos",
        Permission = AccessPermissions.ProfilesView,
        LabelKey = "app.nav.item.admin.permissionsAssignment",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-00000000000c"
    )]
    public const string PermissionsAssignment = "/admin/permissions";

    // ADMINISTRATION-CLEAN-ACCESS-01: renombrado de "Delegar Funciones" a "Seguridad
    // administrativa" — esta pantalla es una matriz permanente de capacidades administrativas
    // (manageRoles/manageModules/manageScreens/manageProcesses), no delegación temporal
    // (delegante/delegado/fechas/motivo/estado); el nombre anterior prometía algo que la pantalla
    // no hace. Mismo Id/ruta/permiso — la delegación temporal real queda para un ticket futuro.
    [NavItem(
        "Seguridad administrativa",
        Permission = AdminPermissions.DelegationView,
        LabelKey = "app.nav.item.admin.security",
        SortOrder = 40,
        Id = "a1000000-0000-4000-9000-00000000000a"
    )]
    public const string Security = "/admin/security";

    // ADMIN-SESSIONS-ACTIVITY-POLISH-01 / ADMINISTRATION-CLEAN-ACCESS-01: orden final —
    // Usuarios, Perfiles, Asignación de permisos, Seguridad administrativa, Sesiones de usuario,
    // Actividad.
    [NavItem(
        "Sesiones de usuario",
        Permission = AccessPermissions.SessionsView,
        LabelKey = "app.nav.item.admin.accessSessions",
        SortOrder = 50,
        Id = "a1000000-0000-4000-9000-00000000000b"
    )]
    public const string AccessSessions = "/admin/access/sessions";

    [NavItem(
        "Activity",
        Permission = AdminPermissions.ActivityView,
        LabelKey = "app.nav.item.admin.activity",
        SortOrder = 60,
        Id = "a1000000-0000-4000-9000-000000000009"
    )]
    public const string Activity = "/admin/activity";

    // "Companies" (ADMIN-COMPANIES-REGROUP-01) se movió a SettingsModule — administra datos de
    // empresa/fiscales/branding/documentos, conceptualmente Configuración, no usuarios/perfiles/
    // delegación/sesiones/actividad. Mismo Id, ruta y permiso preservados.
}
