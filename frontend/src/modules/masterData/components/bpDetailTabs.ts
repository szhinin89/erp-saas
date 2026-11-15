/**
 * Pestañas de la ficha de Business Partner por contexto de módulo: `/suppliers/:id` (Proveedor) y
 * `/customers/:id` (Cliente). Única fuente de ids, orden y contenido; el contexto determina el
 * conjunto de pestañas, así que nunca se mezclan secciones de Proveedor con Cliente. `?tab=` solo se
 * acepta si la pestaña pertenece al contexto; cualquier otro valor cae en Resumen.
 */
export type BpContext = "supplier" | "customer";

export const BP_CONTEXT_TABS = {
  supplier: [
    "summary",
    "general",
    "sri",
    "purchases",
    "retentions",
    "expenseSubcategories",
    "locations",
    "contacts",
  ],
  customer: [
    "summary",
    "general",
    "classification",
    "sales",
    "locations",
    "contacts",
  ],
} as const;

export type BpTabId =
  | (typeof BP_CONTEXT_TABS.supplier)[number]
  | (typeof BP_CONTEXT_TABS.customer)[number];

export const BP_DEFAULT_TAB: BpTabId = "summary";

/** Rol que habilita la ficha de cada contexto (valor de `roleType` del backend). */
export const BP_CONTEXT_ROLE: Record<BpContext, "Supplier" | "Customer"> = {
  supplier: "Supplier",
  customer: "Customer",
};

/** Listado al que regresa cada contexto y base de la ruta de su ficha. */
export const BP_CONTEXT_LIST_PATH: Record<BpContext, string> = {
  supplier: "/suppliers",
  customer: "/customers",
};

export function bpDetailPath(context: BpContext, id: string, tab?: BpTabId): string {
  const base = `${BP_CONTEXT_LIST_PATH[context]}/${id}`;
  return tab ? `${base}?tab=${tab}` : base;
}

export function tabsForContext(context: BpContext): readonly BpTabId[] {
  return BP_CONTEXT_TABS[context];
}

export function resolveBpTab(
  requested: string | null,
  available: readonly BpTabId[],
): BpTabId {
  return available.find((id) => id === requested) ?? BP_DEFAULT_TAB;
}
