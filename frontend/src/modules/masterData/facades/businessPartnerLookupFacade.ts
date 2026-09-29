/**
 * businessPartnerLookupFacade — superficie pública read-only de detalle de socio de negocio
 * para consumidores externos (supplier-payments, purchases: datos del proveedor; sales:
 * cliente seleccionado con sus sucursales, contactos y configuración comercial).
 *
 * Expone únicamente lecturas; nunca el CRUD completo de businessPartnerFacade (las
 * mutaciones públicas viven en businessPartnerRegistrationFacade). Los módulos externos
 * deben importar desde aquí, nunca directamente de masterData/api/businessPartnerFacade.
 */
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type {
  BpContactDto,
  BpLocationDto,
  BusinessPartnerDetailDto,
  CompanyBpSalesSettingsDto,
  SupplierRoleConfigDto,
} from "../types/businessPartner.types";

export type {
  BpContactDto,
  BpLocationDto,
  BusinessPartnerDetailDto,
  CompanyBpSalesSettingsDto,
  SupplierRoleConfigDto,
};

export const businessPartnerLookupFacade = {
  getBusinessPartner: businessPartnerFacade.getBusinessPartner,
  getLocations: businessPartnerFacade.getLocations,
  getContacts: businessPartnerFacade.getContacts,
  getSalesSettings: businessPartnerFacade.getSalesSettings,
};
