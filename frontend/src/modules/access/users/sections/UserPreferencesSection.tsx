import { useEffect, useState } from "react";
import {
  useWatch,
  type Control,
  type FieldErrors,
  type UseFormGetValues,
  type UseFormRegister,
  type UseFormSetValue,
} from "react-hook-form";
import { ZHField } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import { COMPANY_USER_LOGIN_MODES } from "../../api/companyUserPreferencesService";
import { type BranchListItemDto } from "../../../branches/api/branchService";
import { type UserConfigFormValues } from "../../../../schemas/access/userConfigSchema";

interface Props {
  control: Control<UserConfigFormValues>;
  register: UseFormRegister<UserConfigFormValues>;
  setValue: UseFormSetValue<UserConfigFormValues>;
  getValues: UseFormGetValues<UserConfigFormValues>;
  errors: FieldErrors<UserConfigFormValues>;
  branchesCatalog: BranchListItemDto[];
  canManage: boolean;
  disabled: boolean;
  blockError: string;
}

/**
 * Sección "Preferencias de acceso" — parte del formulario único de UserConfigPage (Fase 2).
 * Reglas de negocio cliente-side agregadas en el pulido UX (UAT):
 * - 0 sucursales autorizadas → fuerza AskBranch, deshabilita el modo de ingreso directo y explica
 *   por qué (nunca deja un select vacío o roto).
 * - 1 sola sucursal autorizada → autoselecciona la default sin preguntarle al admin algo obvio.
 * - Si la sucursal por defecto deja de estar autorizada (se revocó) → se limpia automáticamente
 *   con una nota explícita, en vez de dejar que el submit llegue con un valor inválido al backend.
 *
 * El efecto de estas reglas depende únicamente de `authorizedBranchIds` (vía `getValues` para
 * leer loginMode/defaultBranchId como snapshot, no como dependencia reactiva) — si dependiera
 * también de `defaultBranchId`/`loginMode`, el propio `setValue` que hace el efecto dispararía
 * una segunda pasada que borraría el aviso recién mostrado.
 */
export function UserPreferencesSection({
  control,
  register,
  setValue,
  getValues,
  errors,
  branchesCatalog,
  canManage,
  disabled,
  blockError,
}: Props) {
  const { t } = useI18n();

  const authorizedBranchIds = useWatch({
    control,
    name: "authorizedBranchIds",
  });
  const loginMode = useWatch({ control, name: "loginMode" });

  const authorizedOptions = branchesCatalog.filter((b) =>
    authorizedBranchIds.includes(b.id),
  );
  const hasNoAuthorizedBranches = authorizedBranchIds.length === 0;

  const [defaultBranchAutoCleared, setDefaultBranchAutoCleared] =
    useState(false);

  useEffect(() => {
    const currentDefaultBranchId = getValues("defaultBranchId");
    const currentLoginMode = getValues("loginMode");

    if (hasNoAuthorizedBranches) {
      if (currentLoginMode !== "AskBranch")
        setValue("loginMode", "AskBranch", { shouldDirty: true });
      if (currentDefaultBranchId)
        setValue("defaultBranchId", "", { shouldDirty: true });
      setDefaultBranchAutoCleared(false);
      return;
    }

    if (
      currentDefaultBranchId &&
      !authorizedBranchIds.includes(currentDefaultBranchId)
    ) {
      // La sucursal por defecto ya no está autorizada (se acaba de revocar, o el dato cargado ya
      // venía inconsistente) — se limpia y se avisa en ambos casos, nunca se deja llegar al submit.
      setValue("defaultBranchId", "", { shouldDirty: true });
      setDefaultBranchAutoCleared(true);
      return;
    }

    setDefaultBranchAutoCleared(false);

    if (authorizedBranchIds.length === 1 && !currentDefaultBranchId) {
      setValue("defaultBranchId", authorizedBranchIds[0], {
        shouldDirty: true,
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    hasNoAuthorizedBranches,
    authorizedBranchIds.join(","),
    getValues,
    setValue,
  ]);

  return (
    <div>
      {blockError ? (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={blockError}
        />
      ) : null}

      {defaultBranchAutoCleared ? (
        <ZHPageNotice
          variant="warning"
          message={t(
            "users.config.preferences.defaultBranchAutoCleared",
            "Se quitó la sucursal por defecto porque ya no está autorizada. Selecciona una nueva si el modo de ingreso directo sigue activo.",
          )}
        />
      ) : null}

      {hasNoAuthorizedBranches ? (
        <ZHPageNotice
          variant="info"
          message={t(
            "users.config.preferences.noBranchesAuthorized",
            'Este usuario no tiene sucursales autorizadas todavía. Autorice al menos una en "Acceso a sucursales" para configurar el ingreso directo.',
          )}
        />
      ) : null}

      <ZHField
        label={t("security.preferences.loginMode", "Modo de ingreso")}
        error={errors.loginMode?.message}
      >
        <ZhSelect
          disabled={disabled || !canManage || hasNoAuthorizedBranches}
          {...register("loginMode")}
        >
          {COMPANY_USER_LOGIN_MODES.map((mode) => (
            <option
              key={mode}
              value={mode}
              disabled={mode === "DirectToDefault" && hasNoAuthorizedBranches}
            >
              {mode === "AskBranch"
                ? t(
                    "security.preferences.loginMode.askBranch",
                    "Preguntar sucursal al iniciar sesión",
                  )
                : t(
                    "security.preferences.loginMode.directToDefault",
                    "Ingresar directamente a la sucursal por defecto",
                  )}
            </option>
          ))}
        </ZhSelect>
      </ZHField>

      <ZHField
        label={t("security.preferences.defaultBranch", "Sucursal por defecto")}
        error={errors.defaultBranchId?.message}
        hint={
          loginMode === "AskBranch"
            ? t(
                "security.preferences.defaultBranch.optional",
                "Opcional en este modo.",
              )
            : null
        }
      >
        <ZhSelect
          disabled={disabled || !canManage || hasNoAuthorizedBranches}
          {...register("defaultBranchId")}
        >
          <option value="">
            {t(
              "security.preferences.defaultBranch.none",
              "Sin sucursal por defecto",
            )}
          </option>
          {authorizedOptions.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </ZhSelect>
      </ZHField>
    </div>
  );
}
