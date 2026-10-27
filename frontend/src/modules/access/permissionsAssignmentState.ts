import type {
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
