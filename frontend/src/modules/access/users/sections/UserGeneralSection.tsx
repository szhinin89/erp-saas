import { useState } from "react";
import type {
  Control,
  FieldErrors,
  UseFormRegister,
  UseFormSetValue,
} from "react-hook-form";
import { ZHField, ZHLinkButton } from "../../../../components/zh/ZHForm";
import {
  ZHPageNotice,
  type ZHPageNoticeVariant,
} from "../../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../../i18n/i18n";
import { formatApiRequestError } from "../../../lib/apiError";
import {
  membershipService,
  MEMBERSHIP_ROLES,
  type MembershipRole,
} from "../api/membershipService";
import { type Profile } from "../../api/profileService";
import { type UserConfigFormValues } from "../../../../schemas/access/userConfigSchema";
import { UserRoleProfileFields } from "./UserRoleProfileFields";
import { ZhTextInput } from "../../../../components/zh/inputs";

type Notice = { variant: ZHPageNoticeVariant; message: string } | null;

/** Backend persiste Role como string libre (SecurityRoles) — defensivo ante valores inesperados. */
function asMembershipRole(role: string): MembershipRole {
  return (MEMBERSHIP_ROLES as readonly string[]).includes(role)
    ? (role as MembershipRole)
    : "User";
}

interface Props {
  mode: "new" | "edit";
  register: UseFormRegister<UserConfigFormValues>;
  setValue: UseFormSetValue<UserConfigFormValues>;
  control: Control<UserConfigFormValues>;
  errors: FieldErrors<UserConfigFormValues>;
  /** Perfil guardado de la membership en edición (null = operativo existente sin perfil). */
  initialProfileId?: string | null;
  profiles: Profile[];
  profilesLoaded?: boolean;
  canManage: boolean;
  canCreateUser: boolean;
  disabled: boolean;
  blockError: string;
}

/**
 * Sección "Información General" — parte del formulario único de UserConfigPage (Fase 2). Ya no
 * tiene useForm ni submit propios: escribe directamente en el formulario compartido vía
 * setValue/register. El único efecto secundario propio es la resolución de username en modo
 * `new` (lookupUserByUsername onBlur, sin botón "Continuar" intermedio) — mientras `stage` no se
 * resuelva a 'create'/'assign', el botón "Guardar configuración" del padre permanece deshabilitado.
 */
export function UserGeneralSection({
  mode,
  register,
  setValue,
  control,
  errors,
  initialProfileId = null,
  profiles,
  profilesLoaded = true,
  canManage,
  canCreateUser,
  disabled,
  blockError,
}: Props) {
  const { t } = useI18n();

  const [lastLookedUp, setLastLookedUp] = useState("");
  const [lookupLoading, setLookupLoading] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);
  const [alreadyActiveMembershipId, setAlreadyActiveMembershipId] = useState<
    string | null
  >(null);
  const [resolvedMode, setResolvedMode] = useState<
    "idle" | "create" | "assign"
  >("idle");

  const runLookup = async (username: string) => {
    const trimmed = username.trim();
    if (!trimmed || trimmed === lastLookedUp) return;
    setLastLookedUp(trimmed);
    setAlreadyActiveMembershipId(null);
    setLookupLoading(true);
    setNotice(null);
    setValue("stage", "pending");
    setResolvedMode("idle");
    try {
      const result = await membershipService.lookupUserByUsername(trimmed);
      if (!result.identityUserExists) {
        if (!canCreateUser) {
          setNotice({
            variant: "attention",
            message: t(
              "users.addUser.notice.notFoundNoPermission",
              "No existe ninguna cuenta con ese username y no tienes permiso para crear usuarios nuevos.",
            ),
          });
          return;
        }
        setNotice({
          variant: "info",
          message: t(
            "users.addUser.notice.willCreate",
            "No se encontró ninguna cuenta con ese username. Se creará una nueva cuenta.",
          ),
        });
        setValue("firstName", "");
        setValue("lastName", "");
        setValue("email", "");
        setValue("password", "");
        setValue("role", "User");
        setValue("profileId", null);
        setValue("stage", "create");
        setResolvedMode("create");
        return;
      }

      const existingMembership = result.membership;
      if (!existingMembership) {
        setValue("role", "User");
        setValue("profileId", null);
        setNotice({
          variant: "success",
          message: t("users.addUser.notice.foundNoMembership", {
            name: result.fullName ?? "",
          }),
        });
      } else if (existingMembership.isActive) {
        setAlreadyActiveMembershipId(
          existingMembership.companyUserMembershipId,
        );
        setNotice({
          variant: "attention",
          message: t(
            "users.addUser.notice.alreadyMember",
            "Esta persona ya forma parte de tu equipo. Puedes ir a su configuración para revisar o actualizar sus datos.",
          ),
        });
        return;
      } else {
        setValue("role", asMembershipRole(existingMembership.role));
        setValue("profileId", existingMembership.profileId);
        setNotice({
          variant: "warning",
          message: t(
            "users.addUser.notice.revoked",
            "Esta cuenta está revocada. Puede reactivarse completando este formulario.",
          ),
        });
      }
      setValue("stage", "assign");
      setResolvedMode("assign");
    } catch (err) {
      setNotice({
        variant: "error",
        message: formatApiRequestError(err, {
          generic: t(
            "users.addUser.error.lookup",
            "No se pudo verificar el username.",
          ),
        }),
      });
    } finally {
      setLookupLoading(false);
    }
  };

  if (mode === "edit") {
    return (
      <div>
        <ZHField label={t("users.membership.username", "Username")} readOnly>
          <ZhTextInput
            className="zh-input"
            type="text"
            disabled
            readOnly
            {...register("username")}
          />
        </ZHField>

        {blockError ? (
          <ZHPageNotice
            variant="error"
            message={t("common.errorPrefix")}
            detail={blockError}
          />
        ) : null}

        <UserRoleProfileFields
          register={register}
          control={control}
          errors={errors}
          profiles={profiles}
          profilesLoaded={profilesLoaded}
          disabled={!canManage || disabled}
          allowNoProfile={!initialProfileId}
        />
      </div>
    );
  }

  return (
    <div>
      <ZHField
        label={t("users.addUser.username", "Username")}
        error={errors.username?.message}
      >
        <ZhTextInput
          className="zh-input"
          type="text"
          disabled={lookupLoading || disabled}
          {...register("username", {
            onBlur: (e) => void runLookup(e.target.value),
          })}
        />
      </ZHField>
      {lookupLoading ? <p className="subtle">{t("common.loading")}</p> : null}

      {notice ? (
        <ZHPageNotice variant={notice.variant} message={notice.message} />
      ) : null}

      {alreadyActiveMembershipId ? (
        <ZHLinkButton
          variant="secondary"
          size="md"
          to={`/access/users/${alreadyActiveMembershipId}`}
        >
          {t(
            "users.config.goToExisting",
            "Ir a la configuración de este usuario",
          )}
        </ZHLinkButton>
      ) : null}

      {blockError ? (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={blockError}
        />
      ) : null}

      {resolvedMode === "create" ? (
        <>
          <ZHField
            label={t("users.createUser.firstName", "Nombre")}
            error={errors.firstName?.message}
          >
            <ZhTextInput
              className="zh-input"
              type="text"
              disabled={disabled}
              {...register("firstName")}
            />
          </ZHField>
          <ZHField
            label={t("users.createUser.lastName", "Apellido")}
            error={errors.lastName?.message}
          >
            <ZhTextInput
              className="zh-input"
              type="text"
              disabled={disabled}
              {...register("lastName")}
            />
          </ZHField>
          <ZHField
            label={t("users.createUser.email", "Correo electrónico (opcional)")}
            error={errors.email?.message}
          >
            <ZhTextInput
              className="zh-input"
              type="email"
              disabled={disabled}
              {...register("email")}
            />
          </ZHField>
          <ZHField
            label={t("users.createUser.password", "Contraseña inicial")}
            error={errors.password?.message}
            hint={t(
              "users.createUser.password.hint",
              "El usuario deberá cambiarla en su primer inicio de sesión.",
            )}
          >
            <ZhTextInput
              className="zh-input"
              type="password"
              disabled={disabled}
              {...register("password")}
            />
          </ZHField>
          <UserRoleProfileFields
            register={register}
            control={control}
            errors={errors}
            profiles={profiles}
            profilesLoaded={profilesLoaded}
            disabled={disabled}
            allowNoProfile={false}
          />
        </>
      ) : null}

      {resolvedMode === "assign" ? (
        <>
          <UserRoleProfileFields
            register={register}
            control={control}
            errors={errors}
            profiles={profiles}
            profilesLoaded={profilesLoaded}
            disabled={disabled}
            allowNoProfile={false}
          />
        </>
      ) : null}
    </div>
  );
}
