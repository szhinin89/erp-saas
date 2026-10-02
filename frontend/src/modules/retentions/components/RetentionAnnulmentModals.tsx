import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { todayIso } from "../../../lib/formatters/dateFormatters";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { retentionsService, type RetentionAnnulmentRequestDto } from "../api/retentionsService";
import {
  abandonAnnulmentSchema,
  submitAnnulmentSchema,
  type AbandonAnnulmentFormValues,
  type SubmitAnnulmentFormValues,
} from "../schemas/retentionAnnulmentSchema";

type ModalProps = {
  open: boolean;
  annulment: RetentionAnnulmentRequestDto;
  onClose: () => void;
  onDone: (updated: RetentionAnnulmentRequestDto) => void;
};

/**
 * "Ya presenté la solicitud": registra la presentación hecha en SRI en Línea. No significa ANULADO: al
 * guardar, el ERP consulta automáticamente el estado en el SRI (ConsultaComprobante).
 */
export function SubmitAnnulmentModal({ open, annulment, onClose, onDone }: ModalProps) {
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const submittingRef = useRef(false);
  const { register, handleSubmit, reset, setError, formState: { errors } } =
    useForm<SubmitAnnulmentFormValues>({
      resolver: zodResolver(submitAnnulmentSchema),
      defaultValues: { submittedOn: todayIso(), reference: "", notes: "" },
    });

  useEffect(() => {
    if (open) {
      reset({ submittedOn: todayIso(), reference: "", notes: "" });
      setSubmitError("");
    }
  }, [open, reset]);

  const onValid = handleSubmit(async (values) => {
    if (submittingRef.current) return;
    submittingRef.current = true;
    setSaving(true);
    setSubmitError("");
    try {
      const updated = await retentionsService.submitAnnulment(annulment.id, {
        submittedOn: values.submittedOn,
        reference: values.reference || null,
        notes: values.notes || null,
      });
      onDone(updated);
      onClose();
    } catch (err: unknown) {
      if (!applyServerErrors(err, setError, (msg) => setSubmitError(msg)))
        setSubmitError(formatApiRequestError(err, { generic: "No se pudo registrar la presentación." }));
    } finally {
      submittingRef.current = false;
      setSaving(false);
    }
  });

  return (
    <ZHModal
      open={open}
      onClose={() => !saving && onClose()}
      size="md"
      title="Ya presenté la solicitud en el SRI"
      subtitle="Registre los datos de la solicitud que presentó en SRI en Línea. Esto NO anula el documento: el ERP consultará su estado en el SRI."
    >
      <div>
        <ZHField label="Fecha de presentación" required fieldError={errors.submittedOn?.message}>
          <ZhDateInput disabled={saving} {...register("submittedOn")} />
        </ZHField>
        <ZHField label="Número de trámite / referencia" fieldError={errors.reference?.message}>
          <ZhTextInput disabled={saving} maxLength={200} {...register("reference")} />
        </ZHField>
        <ZHField label="Observación" fieldError={errors.notes?.message}>
          <ZhTextarea rows={2} disabled={saving} maxLength={1000} {...register("notes")} />
        </ZHField>
        {submitError ? <ZHPageNotice variant="error" message="Error" detail={submitError} /> : null}
        <ZHFormActions
          onCancel={onClose}
          onSave={() => void onValid()}
          hideDraft
          disableSave={saving}
          labels={{ cancel: "Cancelar", save: saving ? "Consultando al SRI..." : "Registrar y verificar en SRI" }}
        />
      </div>
    </ZHModal>
  );
}

/** Desistir: antes de presentar, o cuando el SRI confirma que el comprobante sigue AUTORIZADO. */
export function AbandonAnnulmentModal({ open, annulment, onClose, onDone }: ModalProps) {
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const submittingRef = useRef(false);
  const { register, handleSubmit, reset, setError, formState: { errors } } =
    useForm<AbandonAnnulmentFormValues>({
      resolver: zodResolver(abandonAnnulmentSchema),
      defaultValues: { reason: "" },
    });

  useEffect(() => {
    if (open) {
      reset({ reason: "" });
      setSubmitError("");
    }
  }, [open, reset]);

  const onValid = handleSubmit(async (values) => {
    if (submittingRef.current) return;
    submittingRef.current = true;
    setSaving(true);
    setSubmitError("");
    try {
      const updated = await retentionsService.abandonAnnulment(annulment.id, values.reason.trim());
      onDone(updated);
      onClose();
    } catch (err: unknown) {
      if (!applyServerErrors(err, setError, (msg) => setSubmitError(msg)))
        setSubmitError(formatApiRequestError(err, { generic: "No se pudo desistir de la solicitud." }));
    } finally {
      submittingRef.current = false;
      setSaving(false);
    }
  });

  return (
    <ZHModal
      open={open}
      onClose={() => !saving && onClose()}
      size="md"
      title="Desistir de la anulación"
      subtitle="El documento seguirá vigente y su retención continuará autorizada. Si ya presentó la solicitud, solo es posible cuando el SRI confirma que el comprobante sigue AUTORIZADO."
    >
      <div>
        <ZHField label="Motivo" required fieldError={errors.reason?.message}>
          <ZhTextarea rows={3} disabled={saving} maxLength={1000} {...register("reason")} />
        </ZHField>
        {submitError ? <ZHPageNotice variant="error" message="Error" detail={submitError} /> : null}
        <ZHFormActions
          onCancel={onClose}
          onSave={() => void onValid()}
          hideDraft
          disableSave={saving}
          labels={{ cancel: "Cancelar", save: saving ? "Guardando..." : "Desistir" }}
        />
      </div>
    </ZHModal>
  );
}
