import { useAsync } from "../../../hooks/useAsync";
import { itemTypeService, type ItemTypeDto } from "../api/itemTypeService";

/**
 * Única fuente de verdad para consumir el catálogo de tipos de ítem activos.
 * La promesa se comparte a nivel de módulo: si varios componentes se montan
 * en la misma carga de página (ej. ItemsPage + ItemFormTabs), disparan una
 * sola petición GET /api/v1/item-types en vez de una por componente.
 */
let cachedPromise: Promise<ItemTypeDto[]> | null = null;

function fetchItemTypeOptions(): Promise<ItemTypeDto[]> {
  if (!cachedPromise) {
    cachedPromise = itemTypeService
      .list(true)
      .then((list) => list.slice().sort((a, b) => a.sortOrder - b.sortOrder))
      .catch((err) => {
        cachedPromise = null;
        throw err;
      });
  }
  return cachedPromise;
}

/** Invalidar tras crear/editar/activar/desactivar un tipo desde la administración. */
export function invalidateItemTypeOptionsCache(): void {
  cachedPromise = null;
}

export function useItemTypeOptions() {
  return useAsync(() =>
    fetchItemTypeOptions().catch(() => [] as ItemTypeDto[]),
  );
}
