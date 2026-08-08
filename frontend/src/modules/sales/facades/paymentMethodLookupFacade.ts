/**
 * paymentMethodLookupFacade — superficie pública read-only de métodos de
 * pago para consumidores externos (finance).
 *
 * Expone únicamente el listado; nunca las mutaciones de paymentMethodService
 * (create/update/toggle) ni el detalle. Los módulos externos deben importar
 * desde aquí, nunca directamente de sales/api/paymentMethodService.
 */

import { paymentMethodService } from "../api/paymentMethodService";
import type { PaymentMethodDto } from "../api/paymentMethodService";

export type { PaymentMethodDto };

export const paymentMethodLookupFacade = {
  list: paymentMethodService.list,
};
