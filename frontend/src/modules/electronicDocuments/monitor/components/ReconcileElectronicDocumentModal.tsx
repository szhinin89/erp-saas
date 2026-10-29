import { useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ZHModal } from "../../../../components/zh/ZHModal";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZhTextarea, ZhTextInput } from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import { message } from "../../../../lib/messages";
import { applyServerErrors } from "../../../lib/validationErrors";
import { formatApiRequestError } from "../../../lib/apiError";

// Límites espejo de ElectronicDocument.Reconciliation*MaxLen (backend valida con FluentValidation).
const REFERENCE_MAX = 150;
const OBSERVATION_MAX = 300;
const FORM_ID = "edm-reconcile-form";

type Values = { reference: string; observation: string };

/**
 * Caso #32 — conciliación manual de un comprobante con resultado SRI desconocido. Solo registra la
 * evidencia y reconsulta al SRI: nunca autoriza, rechaza ni reenvía por declaración del usuario.
 */
export function ReconcileElectronicDocumentModal({
  onClose,
  onSubmit,
}: {
  onClose: () => void;
  onSubmit: (values: Values) => Promise<void>;
}) {
  const { t } = useI18n();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const submitting = useRef(false);
  const schema = z.object({
    reference: z
      .string()
      .trim()
      .min(1, t("electronicDocuments.monitor.reconcile.referenceRequired"))
      .max(REFERENCE_MAX),
    observation: z
      .string()
      .trim()
      .min(1, t("electronicDocuments.monitor.reconcile.observationRequired"))
      .max(OBSERVATION_MAX),
  });
  const {
    register,
    handleSubmit,
    setError: setFieldError,
    formState: { errors },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { reference: "", observation: "" },
  });

  const submit = handleSubmit(async (values) => {
    if (submitting.current) return;
    submitting.current = true;
    setSaving(true);
    setError("");
    try {
      await onSubmit(values);
      message.success(t("electronicDocuments.monitor.reconcile.success"));
      onClose();
    } catch (err) {
      if (!applyServerErrors(err, setFieldError, setError))
        setError(
          formatApiRequestError(err, {
            generic: t("electronicDocuments.monitor.reconcile.error"),
          }),
        );
    } finally {
      submitting.current = false;
      setSaving(false);
    }
  });

  return (
    <ZHModal
      open
      size="md"
      title={t("electronicDocuments.monitor.reconcile.title")}
      subtitle={t("electronicDocuments.monitor.reconcile.subtitle")}
      onClose={() => {
        if (!saving) onClose();
      }}
      footer={
        <>
          <ZHBtn type="button" onClick={onClose} disabled={saving}>
            {t("electronicDocuments.monitor.reconcile.cancel")}
          </ZHBtn>
          <ZHBtn variant="primary" type="submit" form={FORM_ID} disabled={saving}>
            {t("electronicDocuments.monitor.reconcile.submit")}
          </ZHBtn>
        </>
      }
    >
      <form id={FORM_ID} onSubmit={(e) => void submit(e)} noValidate>
        {error && <p role="alert">{error}</p>}
        <ZHGrid cols={1}>
          <ZHField
            label={t("electronicDocuments.monitor.reconcile.reference")}
            required
            error={errors.reference?.message}
          >
            <ZhTextInput
              {...register("reference")}
              disabled={saving}
              maxLength={REFERENCE_MAX}
              aria-label={t("electronicDocuments.monitor.reconcile.reference")}
            />
          </ZHField>
          <ZHField
            label={t("electronicDocuments.monitor.reconcile.observation")}
            required
            error={errors.observation?.message}
          >
            <ZhTextarea
              {...register("observation")}
              disabled={saving}
              maxLength={OBSERVATION_MAX}
              aria-label={t("electronicDocuments.monitor.reconcile.observation")}
            />
          </ZHField>
        </ZHGrid>
      </form>
    </ZHModal>
  );
}
