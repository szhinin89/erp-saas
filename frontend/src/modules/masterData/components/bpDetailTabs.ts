/**
 * Pestañas de la ficha unificada de Business Partner (`/masterdata/business-partners/:id`).
 * Única fuente de ids, orden y disponibilidad; `?tab=` solo se acepta si la pestaña existe para
 * el socio (Proveedor/Cliente exigen el rol activo); cualquier otro valor cae en Resumen.
 */
export const BP_TAB_IDS = [
  "summary",
  "general",
  "supplier",
  "customer",
  "locations",
  "contacts",
  "roles",
] as const;

export type BpTabId = (typeof BP_TAB_IDS)[number];

export const BP_DEFAULT_TAB: BpTabId = "summary";

export function availableBpTabs(roles: {
  hasSupplier: boolean;
  hasCustomer: boolean;
}): BpTabId[] {
  return BP_TAB_IDS.filter(
    (id) =>
      (id !== "supplier" || roles.hasSupplier) &&
      (id !== "customer" || roles.hasCustomer),
  );
}

export function resolveBpTab(
  requested: string | null,
  available: readonly BpTabId[],
): BpTabId {
  return available.find((id) => id === requested) ?? BP_DEFAULT_TAB;
}
