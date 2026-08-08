/**
 * paymentTermLookupFacade — superficie pública read-only de condiciones de
 * pago para consumidores externos (configuracion).
 *
 * Expone únicamente el listado; nunca las mutaciones de paymentTermService
 * (create/update) ni el detalle. Los módulos externos deben importar desde
 * aquí, nunca directamente de masterData/api/paymentTermService.
 */

import { paymentTermService } from "../api/paymentTermService";
import type { PaymentTermDto } from "../api/paymentTermService";

export type { PaymentTermDto };

export const paymentTermLookupFacade = {
  list: paymentTermService.list,
};
