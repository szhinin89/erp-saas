using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("admin", Icon = "🛡️", SortOrder = 60)]
public static class AdminModule
{
    [NavItem(
        "Users",
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

    [NavItem(
        "Administration Delegation",
        Permission = AdminPermissions.DelegationView,
        LabelKey = "app.nav.item.admin.security",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-00000000000a"
    )]
    public const string Security = "/admin/security";

    // ADMIN-SESSIONS-ACTIVITY-POLISH-01: Sesiones antes de Actividad — orden pedido
    // (Acceso usuarios, Perfiles, Delegación de administración, Sesiones de usuario, Actividad).
    [NavItem(
        "Sesiones de usuario",
        Permission = AccessPermissions.SessionsView,
        LabelKey = "app.nav.item.admin.accessSessions",
        SortOrder = 40,
        Id = "a1000000-0000-4000-9000-00000000000b"
    )]
    public const string AccessSessions = "/admin/access/sessions";

    [NavItem(
        "Activity",
        Permission = AdminPermissions.ActivityView,
        LabelKey = "app.nav.item.admin.activity",
        SortOrder = 50,
        Id = "a1000000-0000-4000-9000-000000000009"
    )]
    public const string Activity = "/admin/activity";

    // "Companies" (ADMIN-COMPANIES-REGROUP-01) se movió a SettingsModule — administra datos de
    // empresa/fiscales/branding/documentos, conceptualmente Configuración, no usuarios/perfiles/
    // delegación/sesiones/actividad. Mismo Id, ruta y permiso preservados.
}
