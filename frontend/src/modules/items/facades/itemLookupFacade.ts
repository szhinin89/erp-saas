/**
 * itemLookupFacade — superficie pública read-only del módulo items para
 * consumidores externos (pickers, matching, lookup en formularios de otros módulos).
 *
 * Expone únicamente búsqueda y detalle; nunca mutaciones (create/update/disable/enable/...).
 * Los módulos externos deben importar desde aquí, nunca directamente de items/api/itemService.
 */

import { itemService } from "../api/itemService";
import type { GetItemsParams } from "../api/itemService";
import type { GetItemsResponse, ItemDetailDto, ItemDto } from "../../../types/items";

export type { GetItemsParams, GetItemsResponse, ItemDetailDto, ItemDto };

export const itemLookupFacade = {
  search: (params?: GetItemsParams): Promise<GetItemsResponse> =>
    itemService.getAll(params),

  getById: (id: string): Promise<ItemDetailDto> => itemService.getById(id),
};
