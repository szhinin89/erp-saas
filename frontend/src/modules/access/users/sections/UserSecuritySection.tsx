import { useState } from "react";
import { ZHField, ZHBtn, ZHLinkButton } from "../../../../components/zh/ZHForm";
import { Badge } from "../../../../components/PageShell";
import { useI18n } from "../../../../i18n/i18n";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { type CompanyUserMembershipAdminDto } from "../api/membershipService";
import { AssignTemporaryPasswordModal } from "../components/AssignTemporaryPasswordModal";

const ASSIGN_TEMPORARY_PASSWORD_PERMISSION =
  "access.identity_users.assign_temporary_password";

interface Props {
  membership: CompanyUserMembershipAdminDto;
}

/**
 * Sección "Seguridad". Resumen de estado/perfil (solo lectura, sin cambios) + acción
 * administrativa "Asignar contraseña temporal" (nueva). No agrega edición de permisos por
 * usuario — eso sigue viviendo exclusivamente a nivel Perfil (ProfilesPage).
 */
export function UserSecuritySection({ membership }: Props) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canAssignTemporaryPassword = canShow(
    ASSIGN_TEMPORARY_PASSWORD_PERMISSION,
  );
  const [modalOpen, setModalOpen] = useState(false);

  return (
    <div>
      <ZHField label={t("users.table.status", "Estado")} readOnly>
        <Badge
          label={
            membership.isActive ? t("common.active") : t("common.inactive")
          }
          variant={membership.isActive ? "green" : "gray"}
        />
      </ZHField>

      <ZHField label={t("users.membership.profile", "Perfil")} readOnly>
        <span>
          {membership.profileName ?? t("users.table.noProfile", "Sin perfil")}
        </span>
      </ZHField>

      {membership.profileId ? (
        <ZHLinkButton variant="ghost" size="md" to="/admin/roles">
          {t(
            "users.config.security.viewProfilePermissions",
            "Ver permisos de este perfil",
          )}
        </ZHLinkButton>
      ) : null}

      {canAssignTemporaryPassword ? (
        <div className="pg-section zh-mt-5">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span
                className="material-symbols-outlined pg-section-icon"
                aria-hidden="true"
              >
                password
              </span>
              <h3 className="pg-section-label">
                {t("users.config.security.card.title", "Seguridad")}
              </h3>
            </div>
          </div>
          <div className="pg-section-body">
            <p className="subtle">
              {t(
                "users.config.security.card.description",
                "Administra la contraseña del usuario.",
              )}
            </p>
            <ZHBtn
              type="button"
              variant="secondary"
              size="md"
              onClick={() => setModalOpen(true)}
            >
              {t(
                "users.config.security.assignTemporaryPassword.action",
                "Asignar contraseña temporal",
              )}
            </ZHBtn>
          </div>
        </div>
      ) : null}

      <AssignTemporaryPasswordModal
        open={modalOpen}
        username={membership.username}
        onClose={() => setModalOpen(false)}
      />
    </div>
  );
}
