import type {
  PermissionCatalog,
  PermissionCatalogCategory,
  PermissionCatalogItem,
} from "./api/adminPermissionsService";

/** Estado de edición de la asignación de permisos: actionCode → permitido. */
export type PermState = Record<string, boolean>;

/**
 * Toggle con cascada por ítem: cualquier acción distinta de la principal (actions[0], el permiso
 * de acceso) exige que la principal esté activa; apagar la principal apaga el resto de acciones
 * del mismo ítem.
 */
export function toggleActionState(
  state: PermState,
  item: PermissionCatalogItem,
  actionCode: string,
  checked: boolean,
): PermState {
  const next = { ...state, [actionCode]: checked };
  const baseCode = item.actions[0]?.code;
  if (baseCode) {
    if (checked && actionCode !== baseCode) next[baseCode] = true;
    if (!checked && actionCode === baseCode) {
      for (const action of item.actions) next[action.code] = false;
    }
  }
  return next;
}

/** actionCodes de una sola categoría del menú (selector masivo Nivel 2). */
export function categoryActionCodes(category: PermissionCatalogCategory): Set<string> {
  const codes = new Set<string>();
  for (const item of category.items) for (const action of item.actions) codes.add(action.code);
  return codes;
}

export function categorySelectionState(
  category: PermissionCatalogCategory,
  state: PermState,
): "all" | "none" | "partial" {
  const codes = [...categoryActionCodes(category)];
  if (codes.length === 0) return "none";
  const activeCount = codes.filter((code) => state[code]).length;
  if (activeCount === 0) return "none";
  if (activeCount === codes.length) return "all";
  return "partial";
}

/** IAM-V1-F2 — resumen de solo lectura de lo que concede un conjunto de permisos. */
export type GrantSummaryScreen = {
  id: string;
  labelKey: string;
  /** Etiquetas de las acciones concedidas en orden del catálogo (la primera es el acceso). */
  actionLabels: string[];
};
export type GrantSummaryGroup = { code: string; labelKey: string; screens: GrantSummaryScreen[] };
export type GrantSummary = {
  groups: GrantSummaryGroup[];
  screenCount: number;
  actionCount: number;
  totalActionCount: number;
};

/**
 * Agrupa por módulo las pantallas con acceso concedido y sus acciones, a partir del mismo
 * catálogo que se edita (nunca una lista paralela). Una pantalla solo cuenta si su acción de
 * acceso (actions[0]) está concedida, igual que la cascada de la edición.
 */
export function summarizeGrants(
  catalog: PermissionCatalog,
  isGranted: (code: string) => boolean,
): GrantSummary {
  const groups: GrantSummaryGroup[] = [];
  let screenCount = 0;
  let actionCount = 0;
  let totalActionCount = 0;
  for (const group of catalog.groups) {
    const screens: GrantSummaryScreen[] = [];
    for (const category of group.categories) {
      for (const item of category.items) {
        totalActionCount += item.actions.length;
        const access = item.actions[0];
        if (!access || !isGranted(access.code)) continue;
        const granted = item.actions.filter((action) => isGranted(action.code));
        screens.push({ id: item.id, labelKey: item.labelKey, actionLabels: granted.map((a) => a.label) });
        screenCount += 1;
        actionCount += granted.length;
      }
    }
    if (screens.length > 0) groups.push({ code: group.code, labelKey: group.labelKey, screens });
  }
  return { groups, screenCount, actionCount, totalActionCount };
}

/** Umbral de asignación masiva: más de la mitad del catálogo pide confirmación explícita. */
export const MASS_ASSIGNMENT_RATIO = 0.5;

export function isMassAssignment(activeCount: number, totalCount: number): boolean {
  return totalCount > 0 && activeCount / totalCount > MASS_ASSIGNMENT_RATIO;
}

/** Diferencias entre lo guardado y lo que se va a guardar (solo códigos del catálogo). */
export function permStateDiff(
  saved: PermState,
  next: PermState,
  codes: Iterable<string>,
): { added: number; removed: number } {
  let added = 0;
  let removed = 0;
  for (const code of codes) {
    const before = !!saved[code];
    const after = !!next[code];
    if (!before && after) added += 1;
    if (before && !after) removed += 1;
  }
  return { added, removed };
}
