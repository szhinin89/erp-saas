using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MAPA-MENU-ERP-SSOT-01: labels ajustados a Usuarios/Perfiles/Permisos/Seguridad/Sesiones/
// Actividad ("Roles"→"Perfiles" ya coincidía con el nombre real de la pantalla, ProfilesPage;
// "Asignación de permisos"→"Permisos"; "Sesiones de usuario"→"Sesiones"; "Activity"→"Actividad",
// corrige un label en inglés suelto). Mismos Ids/rutas/permisos en todos los casos.
[Module("admin", Icon = "🛡️", SortOrder = 120)]
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
        RelatedActionPermissionsCsv = AccessPermissions.IdentityUsersCreate
            + ","
            + AccessPermissions.IdentityUsersAssignTemporaryPassword
    )]
    public const string Users = "/access/users";

    [NavItem(
        "Perfiles",
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
        "Permisos",
        Permission = AccessPermissions.ProfilesView,
        LabelKey = "app.nav.item.admin.permissionsAssignment",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-00000000000c",
        ParentId = "bd7b2326-c77b-4534-ad6f-a7edb19827d6"
    )]
    public const string PermissionsAssignment = "/admin/permissions";

    // NAV-HIERARCHY-UNIFY-01: contenedor "Seguridad" — Sesiones + Actividad.
    [NavItem(
        "Seguridad",
        LabelKey = "app.nav.item.admin.securityGroup",
        SortOrder = 40,
        Id = "09671d4f-1687-44cd-8d26-c8f0c245957b",
        PermissionsAnyCsv = AccessPermissions.SessionsView + "," + AdminPermissions.ActivityView
    )]
    public const string SecurityGroup = "/admin/security-group";

    // Orden final dentro de Seguridad: Sesiones, Actividad.
    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: SessionsClose (POST .../close —
    // AdminUserSessionsPage.tsx "Cerrar sesión"/canClose) es la única acción de escritura real de
    // esta pantalla y no estaba en el catálogo asignable.
    [NavItem(
        "Sesiones",
        Permission = AccessPermissions.SessionsView,
        LabelKey = "app.nav.item.admin.accessSessions",
        SortOrder = 50,
        Id = "a1000000-0000-4000-9000-00000000000b",
        ParentId = "09671d4f-1687-44cd-8d26-c8f0c245957b",
        RelatedActionPermissionsCsv = AccessPermissions.SessionsClose
    )]
    public const string AccessSessions = "/admin/access/sessions";

    [NavItem(
        "Actividad",
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
