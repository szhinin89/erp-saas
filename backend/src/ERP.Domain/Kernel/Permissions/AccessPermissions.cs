namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Permisos del módulo Access — perfiles de acceso y membresías usuario↔empresa.
/// </summary>
public static class AccessPermissions
{
    public const string ProfilesView = "access.profiles.view";
    public const string MembershipsView = "access.company_user_memberships.view";

    /// <summary>Fase 10 — dashboard administrativo de UserSession. Ningún permiso existente
    /// cubría "ver/cerrar sesiones de otros usuarios" (ProfilesView/MembershipsView son sobre
    /// perfiles y membresías, no sesiones; AdminPermissions.ActivityView es sobre el feed de
    /// actividad reciente, no sobre UserSession). Mínimo agregado justificado.</summary>
    public const string SessionsView = "access.sessions.view";
    public const string SessionsClose = "access.sessions.close";

    /// <summary>Crear un IdentityUser nuevo (credenciales de login) es más sensible que gestionar
    /// membership sobre un usuario ya existente — permiso propio, no se reutiliza MembershipsView.</summary>
    public const string IdentityUsersCreate = "access.identity_users.create";

    /// <summary>Asignar una contraseña temporal a un usuario existente de la empresa activa
    /// (fuerza RequirePasswordReset + revoca todo su acceso vivo) — permiso propio, más sensible
    /// que IdentityUsersCreate porque actúa sobre la credencial de un usuario ya operativo.</summary>
    public const string IdentityUsersAssignTemporaryPassword =
        "access.identity_users.assign_temporary_password";
}
