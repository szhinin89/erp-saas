/**
 * payableLookupFacade — superficie pública read-only de cuentas por pagar para consumidores
 * externos (finance: aplicar crédito de proveedor a una CxP).
 *
 * Expone únicamente el listado filtrado, el tipo de fila y la etiqueta de origen; nunca
 * mutaciones. Los módulos externos deben importar desde aquí, nunca directamente de
 * payables/api/payablesService.
 */
import { payablesService, payableOriginLabel } from "../api/payablesService";
import type { PayableListItemDto } from "../api/payablesService";

export type { PayableListItemDto };
export { payableOriginLabel };

export const payableLookupFacade = {
  list: payablesService.list,
};
