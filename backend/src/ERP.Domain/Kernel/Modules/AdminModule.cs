using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("admin", Icon = "🛡️", SortOrder = 60)]
public static class AdminModule
{
    // NAV-HIERARCHY-UNIFY-01: contenedor "Usuarios y roles" — Usuarios + Roles + Asignación de
    // permisos, ningún ítem plano bajo el módulo.
    [NavItem(
        "Usuarios y roles",
        LabelKey = "app.nav.item.admin.usersRolesGroup",
        SortOrder = 10,
        Id = "bd7b2326-c77b-4534-ad6f-a7edb19827d6",
        PermissionsAnyCsv = AccessPermissions.MembershipsView + "," + AccessPermissions.ProfilesView
    )]
    public const string UsersRolesGroup = "/access/users-roles-group";

    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: IdentityUsersCreate ("Agregar usuario" cuando el
    // username no existe aún — IdentityUsersController) e IdentityUsersAssignTemporaryPassword
    // ("Restablecer contraseña" — UserSecuritySection.tsx) son acciones reales de esta pantalla que
    // no estaban en el catálogo asignable: ningún perfil no-Admin podía recibirlas nunca (el toggle
    // no existía en Asignación de permisos), aunque el botón de UI y el endpoint sí las exigían.
    [NavItem(
        "Usuarios",
        Permission = AccessPermissions.MembershipsView,
        LabelKey = "app.nav.item.admin.users",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000008",
        ParentId = "bd7b2326-c77b-4534-ad6f-a7edb19827d6",
        RelatedActionPermissionsCsv = AccessPermissions.IdentityUsersCreate + ","
            + AccessPermissions.IdentityUsersAssignTemporaryPassword
    )]
    public const string Users = "/access/users";

    [NavItem(
        "Roles",
        Permission = AccessPermissions.ProfilesView,
        LabelKey = "app.nav.item.admin.roles",
        SortOrder = 20,
        Id = "a1000000-0000-4000-9000-000000000007",
        ParentId = "bd7b2326-c77b-4534-ad6f-a7edb19827d6"
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
        Id = "a1000000-0000-4000-9000-00000000000c",
        ParentId = "bd7b2326-c77b-4534-ad6f-a7edb19827d6"
    )]
    public const string PermissionsAssignment = "/admin/permissions";

    // NAV-HIERARCHY-UNIFY-01: contenedor "Seguridad" — Seguridad administrativa + Sesiones de
    // usuario + Actividad.
    [NavItem(
        "Seguridad",
        LabelKey = "app.nav.item.admin.securityGroup",
        SortOrder = 40,
        Id = "09671d4f-1687-44cd-8d26-c8f0c245957b",
        PermissionsAnyCsv = AdminPermissions.DelegationView + "," + AccessPermissions.SessionsView
            + "," + AdminPermissions.ActivityView
    )]
    public const string SecurityGroup = "/admin/security-group";

    // ADMINISTRATION-CLEAN-ACCESS-01: renombrado de "Delegar Funciones" a "Seguridad
    // administrativa" — esta pantalla es una matriz permanente de capacidades administrativas
    // (manageRoles/manageModules/manageScreens/manageProcesses), no delegación temporal
    // (delegante/delegado/fechas/motivo/estado); el nombre anterior prometía algo que la pantalla
    // no hace. Mismo Id/ruta/permiso — la delegación temporal real queda para un ticket futuro.
    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: DelegationConfigure (PUT admin-scopes —
    // SecuritySettingsPage.tsx "Guardar"/canConfigure) es la única acción de escritura real de
    // esta pantalla y no estaba en el catálogo asignable.
    [NavItem(
        "Seguridad administrativa",
        Permission = AdminPermissions.DelegationView,
        LabelKey = "app.nav.item.admin.security",
        SortOrder = 40,
        Id = "a1000000-0000-4000-9000-00000000000a",
        ParentId = "09671d4f-1687-44cd-8d26-c8f0c245957b",
        RelatedActionPermissionsCsv = AdminPermissions.DelegationConfigure
    )]
    public const string Security = "/admin/security";

    // ADMIN-SESSIONS-ACTIVITY-POLISH-01 / ADMINISTRATION-CLEAN-ACCESS-01: orden final —
    // Usuarios, Perfiles, Asignación de permisos, Seguridad administrativa, Sesiones de usuario,
    // Actividad.
    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: SessionsClose (POST .../close —
    // AdminUserSessionsPage.tsx "Cerrar sesión"/canClose) es la única acción de escritura real de
    // esta pantalla y no estaba en el catálogo asignable.
    [NavItem(
        "Sesiones de usuario",
        Permission = AccessPermissions.SessionsView,
        LabelKey = "app.nav.item.admin.accessSessions",
        SortOrder = 50,
        Id = "a1000000-0000-4000-9000-00000000000b",
        ParentId = "09671d4f-1687-44cd-8d26-c8f0c245957b",
        RelatedActionPermissionsCsv = AccessPermissions.SessionsClose
    )]
    public const string AccessSessions = "/admin/access/sessions";

    [NavItem(
        "Activity",
        Permission = AdminPermissions.ActivityView,
        LabelKey = "app.nav.item.admin.activity",
        SortOrder = 60,
        Id = "a1000000-0000-4000-9000-000000000009",
        ParentId = "09671d4f-1687-44cd-8d26-c8f0c245957b"
    )]
    public const string Activity = "/admin/activity";

    // "Companies" (ADMIN-COMPANIES-REGROUP-01) se movió a SettingsModule — administra datos de
    // empresa/fiscales/branding/documentos, conceptualmente Configuración, no usuarios/perfiles/
    // delegación/sesiones/actividad. Mismo Id, ruta y permiso preservados.
}
