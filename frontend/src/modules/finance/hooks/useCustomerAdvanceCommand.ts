import { useRef, useState, type ReactNode } from "react";
import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import { message } from "../../../lib/messages";
import { useClientRequestId } from "../../../lib/idempotency/useClientRequestId";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError, readApiErrorMessage } from "../../lib/apiError";

interface Command<TResult> {
  /** Datos que definen la intención: mientras no cambien se reutiliza la misma clave. */
  intent: unknown;
  confirm: {
    title: string;
    message: ReactNode;
    confirmLabel: string;
    variant?: "danger" | "warning";
  };
  send: (clientRequestId: string) => Promise<TResult>;
  successMessage: string;
  errorMessage: string;
  onDone: (result: TResult) => void;
}

/**
 * Camino único de las operaciones de dinero de Anticipos: un solo envío por intención, confirmación
 * obligatoria, `ClientRequestId` estable entre reintentos y errores del servidor en su campo.
 * `fieldAliases` traduce campos del contrato a campos del formulario; los que no existan en el
 * formulario se muestran como aviso para que ningún error quede oculto.
 */
export function useCustomerAdvanceCommand<TForm extends FieldValues>(
  setError: UseFormSetError<TForm>,
  formFields: readonly string[],
  fieldAliases: Readonly<Record<string, string>> = {},
) {
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const busy = useRef(false);
  const clientRequest = useClientRequestId();

  const run = async <TResult>(command: Command<TResult>) => {
    if (busy.current) return;
    busy.current = true;
    setSubmitError("");
    try {
      if (!(await message.confirm(command.confirm))) return;
      setSaving(true);
      const result = await command.send(clientRequest.idFor(command.intent));
      clientRequest.complete();
      message.success(command.successMessage);
      command.onDone(result);
    } catch (err: unknown) {
      const unmatched: string[] = [];
      const toForm: UseFormSetError<TForm> = (name, error) => {
        const server = String(name);
        const field = fieldAliases[server] ?? server;
        if (formFields.includes(field.split(".")[0])) setError(field as Path<TForm>, error);
        else if (error.message) unmatched.push(error.message);
      };
      if (!applyServerErrors<TForm>(err, toForm, setSubmitError))
        setSubmitError(
          readApiErrorMessage(err) || formatApiRequestError(err, { generic: command.errorMessage }),
        );
      else if (unmatched.length) setSubmitError(unmatched.join(" "));
    } finally {
      busy.current = false;
      setSaving(false);
    }
  };

  return { saving, submitError, clearError: () => setSubmitError(""), run };
}
