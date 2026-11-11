import { useEffect, useState } from "react";
import { profileService, type Profile } from "../api/profileService";
import { membershipService } from "../users/api/membershipService";
import { isAdminRole } from "../../../access/permissionUi";

export type ProfileUsage = {
  /** Permisos concedidos por el perfil (null mientras carga o si no se pudo leer). */
  permissionCount: number | null;
  /** Usuarios operativos activos con este perfil (null si no se tiene permiso para verlos). */
  userCount: number | null;
};

/**
 * IAM-V1-F2 — alcance y uso de cada perfil para la tabla de Perfiles, con los endpoints ya
 * existentes: permisos por perfil (`access.profiles.view`) y, solo si se puede ver Usuarios
 * (`access.company_user_memberships.view`), cuántos usuarios operativos activos lo tienen. El
 * listado de usuarios ya excluye cuentas globales; el Administrador de empresa no usa perfil y
 * no se cuenta.
 */
export function useProfileUsage(
  profiles: Profile[],
  canViewUsers: boolean,
): Record<string, ProfileUsage> {
  const [usage, setUsage] = useState<Record<string, ProfileUsage>>({});

  useEffect(() => {
    let cancelled = false;
    if (profiles.length === 0) {
      setUsage({});
      return;
    }
    void (async () => {
      const [permissionCounts, memberships] = await Promise.all([
        Promise.all(
          profiles.map((p) =>
            Promise.resolve()
              .then(() => profileService.getPermissions(p.id))
              .then((r) => (r?.items ?? []).filter((i) => i.isAllowed).length)
              .catch(() => null),
          ),
        ),
        canViewUsers
          ? Promise.resolve()
              .then(() => membershipService.list(true))
              .catch(() => null)
          : Promise.resolve(null),
      ]);
      if (cancelled) return;
      const next: Record<string, ProfileUsage> = {};
      profiles.forEach((p, index) => {
        next[p.id] = {
          permissionCount: permissionCounts[index],
          userCount: memberships
            ? memberships.filter(
                (m) => m.isActive && !isAdminRole(m.role) && m.profileId === p.id,
              ).length
            : null,
        };
      });
      setUsage(next);
    })();
    return () => {
      cancelled = true;
    };
  }, [profiles, canViewUsers]);

  return usage;
}
