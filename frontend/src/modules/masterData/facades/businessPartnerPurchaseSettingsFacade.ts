/** Public read-only purchase settings lookup for expense and purchase forms. */
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type { CompanyBpPurchaseSettingsDto } from "../types/businessPartner.types";

export type { CompanyBpPurchaseSettingsDto };

export const businessPartnerPurchaseSettingsFacade = {
  getPurchaseSettings: businessPartnerFacade.getPurchaseSettings,
};