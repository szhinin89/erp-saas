import { useCallback, useEffect, useState } from "react";
import { ZHField, ZHLinkButton } from "../../../../components/zh/ZHForm";
import { Badge } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../../i18n/i18n";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { isAdminRole } from "../../../../access/permissionUi";
import { formatApiRequestError } from "../../../lib/apiError";
import { type CompanyUserMembershipAdminDto } from "../api/membershipService";
import { profileService } from "../../api/profileService";
import {
  adminPermissionsService,
  type PermissionCatalog,
} from "../../api/adminPermissionsService";
import { PermissionGrantSummary } from "../../components/PermissionGrantSummary";
import { membershipRoleDescription, membershipRoleLabel } from "../../roleLabels";

const PROFILES_VIEW_PERMISSION = "access.profiles.view";

interface Props {
  membership: CompanyUserMembershipAdminDto;
}

type Grants = { catalog: PermissionCatalog; granted: ReadonlySet<string> };

/**
 * IAM-V1-F2 — pestaña «Accesos» (solo lectura): función, estado, perfil y lo que ese perfil
 * concede. El Administrador de empresa tiene acceso total por su función (no por un perfil).
 * El detalle de permisos usa los mismos endpoints que Asignación de permisos y solo se muestra
 * a quien tiene `access.profiles.view`; los permisos se editan siempre a nivel de perfil.
 */
export function UserAccessSection({ membership }: Props) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const admin = isAdminRole(membership.role);
  const canViewProfiles = canShow(PROFILES_VIEW_PERMISSION);
  const profileId = membership.profileId;
  const [grants, setGrants] = useState<Grants | null>(null);
  const [loadError, setLoadError] = useState("");

  const loadGrants = useCallback(
    async (id: string) => {
      setLoadError("");
      try {
        const [catalog, permissions] = await Promise.all([
          adminPermissionsService.getCatalog(),
          profileService.getPermissions(id),
        ]);
        const granted = new Set(
          (permissions?.items ?? []).filter((i) => i.isAllowed).map((i) => i.permissionKey),
        );
        setGrants({ catalog, granted });
      } catch (err) {
        setLoadError(
          formatApiRequestError(err, {
            generic: t("users.access.error.load", "No se pudieron cargar los permisos del perfil."),
          }),
        );
      }
    },
    [t],
  );

  useEffect(() => {
    setGrants(null);
    if (!admin && canViewProfiles && profileId) void loadGrants(profileId);
  }, [admin, canViewProfiles, profileId, loadGrants]);

  const isGranted = useCallback((code: string) => !!grants?.granted.has(code), [grants]);

  return (
    <div>
      <ZHField label={t("users.membership.function", "Función")} readOnly>
        <span>{membershipRoleLabel(membership.role, t)}</span>
      </ZHField>
      {admin ? null : (
        <p className="subtle">{membershipRoleDescription(membership.role, t)}</p>
      )}
      <ZHField label={t("users.table.status", "Estado")} readOnly>
        <Badge
          label={membership.isActive ? t("common.active") : t("common.inactive")}
          variant={membership.isActive ? "green" : "gray"}
        />
      </ZHField>
      {admin ? (
        <ZHPageNotice
          variant="info"
          message={t(
            "users.access.adminFullAccess",
            "Acceso total a esta empresa por su función de Administrador de empresa.",
          )}
        />
      ) : !profileId ? (
        <ZHPageNotice
          variant="warning"
          message={t(
            "users.access.noProfile",
            "Sin perfil de acceso: este usuario operativo no tiene acceso a ninguna pantalla.",
          )}
        />
      ) : (
        <>
          <ZHField label={t("users.membership.profile", "Perfil de acceso")} readOnly>
            <span>{membership.profileName ?? "—"}</span>
          </ZHField>
          {loadError ? (
            <ZHPageNotice variant="error" message={t("common.errorPrefix")} detail={loadError} />
          ) : null}
          {canViewProfiles && grants ? (
            <>
              <PermissionGrantSummary
                catalog={grants.catalog}
                isGranted={isGranted}
                title={t("permissionsSummary.title", "Este perfil concede")}
              />
              <ZHLinkButton
                variant="ghost"
                size="md"
                to={`/admin/permissions?profileId=${profileId}`}
              >
                {t("users.access.manageProfilePermissions", "Gestionar permisos del perfil")}
              </ZHLinkButton>
            </>
          ) : null}
        </>
      )}
    </div>
  );
}
