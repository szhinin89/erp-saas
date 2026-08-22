namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Permisos del módulo Administration — roles, usuarios y auditoría de actividad.
///
/// ADMIN-PERM-ALIGN-01: <see cref="RolesView"/> y <see cref="UsersView"/> quedan LEGACY/inertes —
/// ya no los referencia ningún NavItem ni controller. Antes gateaban solo la visibilidad del ítem
/// de menú de "Perfiles"/"Acceso usuarios" mientras la pantalla y su API ya exigían
/// <see cref="AccessPermissions.ProfilesView"/>/<see cref="AccessPermissions.MembershipsView"/>
/// respectivamente (menú visible pero página negando acceso, o viceversa por URL directa). El
/// NavItem de cada pantalla (AdminModule.cs) y el filtro "Administración" de la matriz de permisos
/// de Perfiles (ProfilesPage.tsx) ahora usan el permiso canónico real. Se conservan estas dos
/// constantes (sin eliminarlas) por compatibilidad: perfiles ya creados que tengan
/// "admin.roles.view"/"admin.users.view" guardado no deben aparecer como "permiso desconocido" en
/// la auditoría de permisos (GetProfilePermissionAudit) — simplemente dejan de tener efecto.
/// </summary>
public static class AdminPermissions
{
    public const string RolesView = "admin.roles.view";
    public const string UsersView = "admin.users.view";
    public const string ActivityView = "admin.activity.view";

    /// <summary>
    /// ADMIN-SECURITY-SPLIT-01: permisos de la pantalla "Delegación de administración"
    /// (/admin/security — matriz de manageRoles/manageModules/manageScreens/manageProcesses por
    /// usuario). No confundir con los permisos funcionales de Perfiles (AccessPermissions.
    /// ProfilesView) — esta pantalla solo controla quién puede administrar delegación
    /// administrativa, nunca permisos operativos de negocio.
    /// </summary>
    public const string DelegationView = "admin.delegation.view";
    public const string DelegationConfigure = "admin.delegation.configure";
}
