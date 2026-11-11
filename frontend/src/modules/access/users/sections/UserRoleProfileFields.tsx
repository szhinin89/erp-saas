import { useWatch, type Control, type FieldErrors, type UseFormRegister } from "react-hook-form";
import { ZHField } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import { isAdminRole } from "../../../../access/permissionUi";
import { MEMBERSHIP_ROLES } from "../api/membershipService";
import { type Profile } from "../../api/profileService";
import { membershipRoleDescription, membershipRoleLabel } from "../../roleLabels";
import { type UserConfigFormValues } from "../../../../schemas/access/userConfigSchema";

interface Props {
  register: UseFormRegister<UserConfigFormValues>;
  control: Control<UserConfigFormValues>;
  errors: FieldErrors<UserConfigFormValues>;
  profiles: Profile[];
  disabled: boolean;
  /**
   * Solo al editar un usuario operativo que ya existía sin perfil: se conserva y se permite
   * guardarlo así. Toda asignación nueva exige perfil (IAM-V1-F2).
   */
  allowNoProfile: boolean;
}

/**
 * IAM-V1-F2 — función y perfil del usuario, único bloque para alta, vinculación y edición.
 * «Administrador de empresa» (Role = Admin) tiene acceso total por su función: el perfil no
 * aplica y no se muestra (si ya tenía uno guardado, se conserva sin cambios).
 */
export function UserRoleProfileFields({
  register,
  control,
  errors,
  profiles,
  disabled,
  allowNoProfile,
}: Props) {
  const { t } = useI18n();
  const role = useWatch({ control, name: "role" });
  const admin = isAdminRole(role);

  return (
    <>
      <ZHField
        label={t("users.membership.function", "Función")}
        error={errors.role?.message}
        hint={membershipRoleDescription(role, t)}
      >
        <ZhSelect disabled={disabled} {...register("role")}>
          {MEMBERSHIP_ROLES.map((value) => (
            <option key={value} value={value}>
              {membershipRoleLabel(value, t)}
            </option>
          ))}
        </ZhSelect>
      </ZHField>
      {admin ? (
        <ZHPageNotice
          variant="info"
          message={t(
            "users.membership.profile.notApplicable",
            "Perfil de acceso: no aplica. El Administrador de empresa tiene acceso total a esta empresa.",
          )}
        />
      ) : (
        <ZHField
          label={t("users.membership.profile", "Perfil de acceso")}
          error={errors.profileId?.message}
        >
          <ZhSelect disabled={disabled} {...register("profileId")}>
            <option value="">
              {allowNoProfile
                ? t("users.membership.profile.none", "Sin perfil (sin acceso)")
                : t("users.membership.profile.select", "Selecciona un perfil")}
            </option>
            {profiles.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      )}
    </>
  );
}
