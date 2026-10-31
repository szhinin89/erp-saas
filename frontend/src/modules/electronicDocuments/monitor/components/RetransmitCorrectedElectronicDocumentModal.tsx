import { useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ZHModal } from "../../../../components/zh/ZHModal";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZhTextarea } from "../../../../components/zh/inputs";
import type { ElectronicDocumentRetransmissionDto } from "../../../../components/zh/electronicDocuments/electronicDocumentDiagnosticTypes";
import { useI18n } from "../../../../i18n/i18n";
import { message } from "../../../../lib/messages";
import { applyServerErrors } from "../../../lib/validationErrors";
import { formatApiRequestError } from "../../../lib/apiError";

// Límite espejo de ElectronicDocument.RetransmissionReasonMaxLen (backend valida con FluentValidation).
const REASON_MAX = 300;
const FORM_ID = "edm-retransmit-form";

type Values = { reason: string };

/**
 * R12 Fase B — confirmación explícita de "Corregir y retransmitir". El backend regenera el XML desde la
 * operación confirmada, exige una corrección demostrable y transmite una sola vez con la misma clave; la
 * clave de idempotencia se fija al abrir el modal para que un reintento del mismo envío nunca duplique.
 */
export function RetransmitCorrectedElectronicDocumentModal({
  retransmission,
  onClose,
  onSubmit,
}: {
  retransmission: ElectronicDocumentRetransmissionDto;
  onClose: () => void;
  onSubmit: (values: { reason: string; idempotencyKey: string }) => Promise<void>;
}) {
  const { t } = useI18n();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const submitting = useRef(false);
  const idempotencyKey = useRef(crypto.randomUUID());
  const schema = z.object({
    reason: z
      .string()
      .trim()
      .min(1, t("electronicDocuments.monitor.retransmit.reasonRequired"))
      .max(REASON_MAX),
  });
  const {
    register,
    handleSubmit,
    setError: setFieldError,
    formState: { errors },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { reason: "" },
  });

  const submit = handleSubmit(async (values) => {
    if (submitting.current) return;
    submitting.current = true;
    setSaving(true);
    setError("");
    try {
      await onSubmit({
        reason: values.reason,
        idempotencyKey: idempotencyKey.current,
      });
      message.success(t("electronicDocuments.monitor.retransmit.success"));
      onClose();
    } catch (err) {
      if (!applyServerErrors(err, setFieldError, setError))
        setError(
          formatApiRequestError(err, {
            generic: t("electronicDocuments.monitor.retransmit.error"),
          }),
        );
    } finally {
      submitting.current = false;
      setSaving(false);
    }
  });

  const stageKey =
    retransmission.rejectionStage === "Reception"
      ? "electronicDocuments.monitor.retransmit.stageReception"
      : "electronicDocuments.monitor.retransmit.stageAuthorization";

  return (
    <ZHModal
      open
      size="md"
      title={t("electronicDocuments.monitor.retransmit.title")}
      subtitle={t("electronicDocuments.monitor.retransmit.subtitle")}
      onClose={() => {
        if (!saving) onClose();
      }}
      footer={
        <>
          <ZHBtn type="button" onClick={onClose} disabled={saving}>
            {t("electronicDocuments.monitor.retransmit.cancel")}
          </ZHBtn>
          <ZHBtn variant="primary" type="submit" form={FORM_ID} disabled={saving}>
            {t("electronicDocuments.monitor.retransmit.submit")}
          </ZHBtn>
        </>
      }
    >
      <form id={FORM_ID} onSubmit={(e) => void submit(e)} noValidate>
        {error && <p role="alert">{error}</p>}
        <p className="zh-edoc-hint-sm">
          {t(stageKey)} · {t("electronicDocuments.monitor.retransmit.codes")}:{" "}
          {retransmission.rejectionCodes.join(", ")} ·{" "}
          {t("electronicDocuments.monitor.retransmit.nextAttempt")}:{" "}
          {retransmission.transmissionAttempt + 1}
        </p>
        <ZHGrid cols={1}>
          <ZHField
            label={t("electronicDocuments.monitor.retransmit.reason")}
            required
            error={errors.reason?.message}
          >
            <ZhTextarea
              {...register("reason")}
              disabled={saving}
              maxLength={REASON_MAX}
              aria-label={t("electronicDocuments.monitor.retransmit.reason")}
            />
          </ZHField>
        </ZHGrid>
      </form>
    </ZHModal>
  );
}
