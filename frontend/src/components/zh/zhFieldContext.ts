import { createContext, useContext } from "react";
import type { AriaAttributes } from "react";

/**
 * Contrato compartido de error entre `<ZHField>` y los inputs oficiales (Paquete C3).
 *
 * `ZHField` publica este contexto SOLO cuando hay un mensaje de error (`fieldError`/`error`): el
 * valor es `null` en cualquier otro caso. Cada input oficial lee el contexto con
 * `useZHFieldA11y` y añade `aria-invalid` y `aria-describedby` (apuntando al mensaje de error)
 * sin cambiar estilos, estructura ni textos. Un atributo explícito del consumidor siempre gana
 * y su `aria-describedby` se conserva (se concatena, no se reemplaza).
 */
export type ZHFieldErrorContextValue = {
  /** id del `<p>` que contiene el mensaje de error visible. */
  messageId: string;
};

export const ZHFieldErrorContext = createContext<ZHFieldErrorContextValue | null>(null);

type ExplicitA11y = {
  "aria-invalid"?: AriaAttributes["aria-invalid"];
  "aria-describedby"?: string;
};

/** Atributos ARIA que un control oficial debe aplicar dentro de un `ZHField` con error. */
export function useZHFieldA11y(explicit: ExplicitA11y): ExplicitA11y {
  const ctx = useContext(ZHFieldErrorContext);
  if (!ctx) {
    return {
      "aria-invalid": explicit["aria-invalid"],
      "aria-describedby": explicit["aria-describedby"],
    };
  }
  const described = [explicit["aria-describedby"], ctx.messageId].filter(Boolean).join(" ");
  return {
    "aria-invalid": explicit["aria-invalid"] ?? true,
    "aria-describedby": described || undefined,
  };
}
