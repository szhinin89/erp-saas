/**
 * businessPartnerLookupFacade — superficie pública read-only de detalle de socio de negocio
 * para consumidores externos (supplier-payments: datos del proveedor).
 *
 * Expone únicamente la lectura por Id; nunca el CRUD completo de businessPartnerFacade.
 * Los módulos externos deben importar desde aquí, nunca directamente de
 * masterData/api/businessPartnerFacade.
 */
import { businessPartnerFacade } from "../api/businessPartnerFacade";

export const businessPartnerLookupFacade = {
  getBusinessPartner: businessPartnerFacade.getBusinessPartner,
};
