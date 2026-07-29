import { useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../../components/zh/ZHModal";
import { ZHField, ZHFormActions } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../../i18n/i18n";
import { message } from "../../../../lib/messages";
import { membershipService } from "../api/membershipService";
import { applyServerErrors } from "../../../lib/validationErrors";
import {
  formatApiRequestError,
  readApiErrorMessage,
} from "../../../lib/apiError";
import {
  assignTemporaryPasswordSchema,
  type AssignTemporaryPasswordFormValues,
} from "../../../../schemas/access/assignTemporaryPasswordSchema";

interface Props {
  open: boolean;
  username: string;
  onClose: () => void;
}

/**
 * Modal Admin IAM — asigna una contraseña temporal a un usuario existente de la empresa activa.
 * El backend (POST .../assign-temporary-password) ya hace hash, RequirePasswordReset, revoca
 * refresh tokens y cierra sesiones activas; este modal solo consume el endpoint y nunca conserva
 * la contraseña más allá del ciclo de vida del formulario (no se guarda en estado global, no se
 * loguea, no se muestra tras el envío).
 */
export function AssignTemporaryPasswordModal({
  open,
  username,
  onClose,
}: Props) {
  const { t } = useI18n();
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState("");
  // Guard síncrono además de `saving`: RHF valida de forma asíncrona (zodResolver), así que dos
  // clics disparados en el mismo tick pueden completar su validación antes de que React
  // re-renderice `disabled={saving}` — sin este ref, ambos envíos llegarían a invocar el
  // servicio. La comprobación ocurre como primera línea del callback, antes de cualquier await.
  const submittingRef = useRef(false);

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<AssignTemporaryPasswordFormValues>({
    resolver: zodResolver(assignTemporaryPasswordSchema),
    defaultValues: { temporaryPassword: "", confirmPassword: "" },
  });

  const handleClose = () => {
    if (saving) return;
    reset({ temporaryPassword: "", confirmPassword: "" });
    setSubmitError("");
    onClose();
  };

  // Sin <form> propio (ver comentario más abajo) se pierde el submit-on-Enter nativo del
  // navegador — se restituye explícitamente sobre los dos inputs de contraseña.
  const handleEnterKey = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter" && !saving) {
      e.preventDefault();
      void onValid();
    }
  };

  const onValid = handleSubmit(async (values) => {
    if (submittingRef.current) return;
    submittingRef.current = true;
    setSubmitError("");
    setSaving(true);
    try {
      await membershipService.assignTemporaryPassword(
        username,
        values.temporaryPassword,
      );
      message.success(
        t(
          "users.config.security.assignTemporaryPassword.success",
          "Contraseña temporal asignada correctamente.",
        ),
      );
      reset({ temporaryPassword: "", confirmPassword: "" });
      onClose();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setError, (msg) =>
        setSubmitError(msg),
      );
      if (!applied) {
        const fromApi = readApiErrorMessage(err);
        setSubmitError(
          fromApi ||
            formatApiRequestError(err, {
              generic: t(
                "users.config.security.assignTemporaryPassword.error",
                "No se pudo asignar la contraseña temporal.",
              ),
            }),
        );
      }
    } finally {
      submittingRef.current = false;
      setSaving(false);
    }
  });

  return (
    <ZHModal
      open={open}
      onClose={handleClose}
      size="md"
      title={t(
        "users.config.security.assignTemporaryPassword.title",
        "Asignar contraseña temporal",
      )}
    >
      {/*
        Nunca un <form> aquí: este modal se monta dentro de UserSecuritySection, que a su vez
        vive dentro del <form onSubmit=...> único de UserConfigPage.tsx ("Guardar configuración").
        HTML no permite formularios anidados — el navegador reasigna el submit al form exterior
        y recarga la página en vez de que RHF lo intercepte. El envío se dispara por clic
        explícito (ZHFormActions.onSave) en vez de onSubmit de un <form> propio.
      */}
      <div>
        <ZHField
          label={t(
            "users.config.security.assignTemporaryPassword.newPassword",
            "Nueva contraseña temporal",
          )}
          error={errors.temporaryPassword?.message}
        >
          <input
            className="zh-input"
            type="password"
            autoComplete="new-password"
            disabled={saving}
            onKeyDown={handleEnterKey}
            {...register("temporaryPassword")}
          />
        </ZHField>

        <ZHField
          label={t(
            "users.config.security.assignTemporaryPassword.confirmPassword",
            "Confirmar contraseña",
          )}
          error={errors.confirmPassword?.message}
        >
          <input
            className="zh-input"
            type="password"
            autoComplete="new-password"
            disabled={saving}
            onKeyDown={handleEnterKey}
            {...register("confirmPassword")}
          />
        </ZHField>

        <ZHPageNotice
          variant="info"
          message={t(
            "users.config.security.assignTemporaryPassword.requirements",
            "Mínimo 8 caracteres, al menos una mayúscula y un número.",
          )}
          detail={t(
            "users.config.security.assignTemporaryPassword.notice",
            "La contraseña será temporal. El usuario estará obligado a cambiarla en su próximo inicio de sesión. Todas las sesiones activas serán cerradas automáticamente.",
          )}
        />

        {submitError ? (
          <ZHPageNotice
            variant="error"
            message={t("common.errorPrefix")}
            detail={submitError}
          />
        ) : null}

        <ZHFormActions
          onCancel={handleClose}
          onSave={() => {
            void onValid();
          }}
          hideDraft
          disableSave={saving}
          labels={{
            cancel: t("common.cancel"),
            save: saving
              ? t("common.saving")
              : t(
                  "users.config.security.assignTemporaryPassword.submit",
                  "Asignar contraseña",
                ),
          }}
        />
      </div>
    </ZHModal>
  );
}
