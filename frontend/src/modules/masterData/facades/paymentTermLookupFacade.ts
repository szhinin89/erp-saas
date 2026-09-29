/**
 * paymentTermLookupFacade — superficie pública read-only de condiciones de
 * pago para consumidores externos (configuracion, sales, purchases, expenses).
 *
 * Expone únicamente listado y detalle; nunca las mutaciones de
 * paymentTermService (create/update). Los módulos externos deben importar
 * desde aquí, nunca directamente de masterData/api/paymentTermService.
 */

import { paymentTermService } from "../api/paymentTermService";
import type { PaymentTermDto } from "../api/paymentTermService";

export type { PaymentTermDto };

export const paymentTermLookupFacade = {
  list: paymentTermService.list,
  getById: paymentTermService.getById,
};
