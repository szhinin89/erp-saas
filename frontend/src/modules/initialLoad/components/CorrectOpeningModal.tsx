import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhTextarea } from "../../../components/zh/inputs";
import { message } from "../../../lib/messages";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError, readApiErrorCode } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { initialLoadService } from "../api/initialLoadService";
import {
  correctOpeningSchema,
  type CorrectOpeningFormValues,
} from "../schemas/openingJournalSchema";

/** Código del guard de períodos del Posting Engine (PostingPeriodGuard) que el reverso IL-8B devuelve. */
const PERIOD_NOT_OPEN = "PERIOD_NOT_OPEN";

const CLOSED_PERIOD_MESSAGE =
  "La apertura pertenece a un período contable cerrado y ya no puede modificarse. Registre un ajuste contable en un período abierto.";

interface Props {
  open: boolean;
  postingId: string | null;
  version: number | null;
  cutoffDate: string | null;
  onClose: () => void;
  /** Se llama tras un reverso exitoso: la conciliación pasa a "Apertura incompleta". */
  onCorrected: () => void;
}

/**
 * IL-8D — "Corregir apertura", paso 1 (IL-8B): pide motivo obligatorio y reversa la versión vigente
 * del ASI. El paso 2 (publicar la nueva versión) lo hace el usuario después desde la misma tarjeta.
 * Nunca se presenta como "anular": el asiento original queda como historial.
 *
 * Auditoría de reutilización: ZHModal + ZHField + ZhTextarea (mismo patrón de motivo que
 * `ManualCashMovementModal`); el mensaje de período cerrado sale del código público del backend.
 */
export function CorrectOpeningModal({
  open,
  postingId,
  version,
  cutoffDate,
  onClose,
  onCorrected,
}: Readonly<Props>) {
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<CorrectOpeningFormValues>({
    resolver: zodResolver(correctOpeningSchema),
    defaultValues: { reason: "" },
  });

  useEffect(() => {
    if (open) {
      reset({ reason: "" });
      setServerError(null);
    }
  }, [open, reset]);

  const onSubmit = async (values: CorrectOpeningFormValues) => {
    if (!postingId) return;
    setServerError(null);
    try {
      await initialLoadService.reverseOpeningJournal(postingId, values.reason);
      message.success("Asiento de apertura reversado. Publique la nueva versión para completar la apertura.");
      onCorrected();
      onClose();
    } catch (err: unknown) {
      if (readApiErrorCode(err) === PERIOD_NOT_OPEN) {
        setServerError(CLOSED_PERIOD_MESSAGE);
        return;
      }
      if (!applyServerErrors(err, setError))
        setServerError(
          formatApiRequestError(err, { generic: "No se pudo corregir la apertura." }),
        );
    }
  };

  return (
    <ZHModal
      open={open}
      onClose={onClose}
      title="Corregir apertura"
      subtitle={version ? `Versión vigente ${version}` : undefined}
      footer={
        <>
          <ZHBtn type="button" variant="ghost" onClick={onClose} disabled={isSubmitting}>
            Cancelar
          </ZHBtn>
          <ZHBtn
            type="button"
            variant="destructive"
            disabled={isSubmitting}
            onClick={() => void handleSubmit(onSubmit)()}
          >
            Corregir apertura
          </ZHBtn>
        </>
      }
    >
      <ZHPageNotice
        variant="warning"
        message={`Corregir la apertura cambia los saldos históricos desde ${
          cutoffDate ? formatDate(cutoffDate) : "la fecha de apertura"
        }.`}
        detail="Se reversará el asiento de apertura vigente (queda como historial) y la apertura quedará incompleta hasta que publique una nueva versión."
      />
      {serverError && <ZHPageNotice variant="error" message={serverError} />}
      <ZHField label="Motivo de la corrección" required fieldError={errors.reason?.message}>
        <ZhTextarea rows={3} maxLength={500} disabled={isSubmitting} {...register("reason")} />
      </ZHField>
    </ZHModal>
  );
}
