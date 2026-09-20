/**
 * customerLookupFacade — superficie pública read-only de búsqueda de clientes
 * para consumidores externos (pricing).
 *
 * Expone únicamente la búsqueda de clientes activos para picker; nunca el CRUD
 * completo de businessPartnerFacade. Los módulos externos deben importar desde
 * aquí, nunca directamente de masterData/api/businessPartnerFacade.
 */

import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type { CustomerPickerRow } from "../types/businessPartner.types";

export type { CustomerPickerRow };

export const customerLookupFacade = {
  searchCustomers: businessPartnerFacade.searchCustomersForPicker,
};
