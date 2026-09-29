/**
 * businessPartnerRegistrationFacade — superficie pública de alta/actualización rápida de
 * socios de negocio desde otros módulos (purchases: crear proveedor desde la factura;
 * sales: registrar/actualizar el cliente desde el POS).
 *
 * Expone el wizard de alta de masterData y solo las mutaciones que esos flujos usan; nunca
 * activación/desactivación, roles de otro tipo de configuración ni retenciones. Los módulos
 * externos deben importar desde aquí, nunca de masterData/api, components, types o constants.
 */
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { RoleTypeEnum } from "../types/businessPartner.types";
import type {
  ContactRoleValue,
  CreateBusinessPartnerBody,
  LocationTypeValue,
} from "../types/businessPartner.types";
import { SRI_ID_TYPE_RUC } from "../constants/sriIdentificationCodes";

export { MasterDataPartnerWizard } from "../components/MasterDataPartnerWizard";
export { RoleTypeEnum, SRI_ID_TYPE_RUC };
export type { ContactRoleValue, CreateBusinessPartnerBody, LocationTypeValue };

export const businessPartnerRegistrationFacade = {
  createBusinessPartner: businessPartnerFacade.createBusinessPartner,
  updateBusinessPartner: businessPartnerFacade.updateBusinessPartner,
  assignRole: businessPartnerFacade.assignRole,
  createLocation: businessPartnerFacade.createLocation,
  updateLocation: businessPartnerFacade.updateLocation,
  createContact: businessPartnerFacade.createContact,
  updateContact: businessPartnerFacade.updateContact,
};
