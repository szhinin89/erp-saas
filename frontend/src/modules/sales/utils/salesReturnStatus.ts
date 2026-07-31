import type { BadgeVariant } from "../../../components/PageShell";

/** Etiqueta en español por estado de `SalesReturn` — única fuente de verdad (antes duplicada
 * entre `SalesReturnListPage` y `SalesReturnFormPage`). */
export const SALES_RETURN_STATUS_LABEL: Record<string, string> = {
  Draft: "Borrador",
  Authorized: "Autorizada",
  Cancelled: "Cancelada",
};

/** Variante del componente `Badge` (`PageShell.tsx`) por estado de `SalesReturn`. */
export const SALES_RETURN_STATUS_BADGE: Record<string, BadgeVariant> = {
  Draft: "orange",
  Authorized: "green",
  Cancelled: "red",
};
