import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import { parseValidationErrors } from "./apiError";

/**
 * Aplica errores 422 del servidor a campos de un formulario React Hook Form.
 * Llama setError(campo, { type: 'server', message }) por cada campo retornado.
 * Retorna true si se aplicaron errores de campo; false si no era un 422 estructurado.
 * Si hay errores generales (campo "_"), los envía al fallback opcional.
 */
export function applyServerErrors<T extends FieldValues>(
  err: unknown,
  setError: UseFormSetError<T>,
  fallback?: (msg: string) => void,
): boolean {
  const fieldErrors = parseValidationErrors(err);
  if (!fieldErrors) return false;

  let applied = false;
  for (const [field, messages] of Object.entries(fieldErrors)) {
    if (field === "_" || !messages.length) continue;
    setError(field as Path<T>, { type: "server", message: messages[0] });
    applied = true;
  }

  if (!applied && fieldErrors["_"]?.length && fallback) {
    fallback(fieldErrors["_"][0]);
    // Fallback handled the error — signal success so the caller doesn't override it.
    return true;
  }

  return applied;
}
