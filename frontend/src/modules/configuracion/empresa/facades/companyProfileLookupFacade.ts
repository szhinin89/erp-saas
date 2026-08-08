/**
 * companyProfileLookupFacade — superficie pública read-only del perfil de
 * empresa para consumidores externos (formularios de items).
 *
 * Expone únicamente la lectura del perfil; nunca las mutaciones de
 * companyProfileService (updateProfile/uploadLogo/updateFiscal/...). Los
 * módulos externos deben importar desde aquí, nunca directamente de
 * configuracion/empresa/api/companyProfileService.
 */

import { companyProfileService } from "../api/companyProfileService";
import type { CompanyProfile } from "../../../../types/companyProfile";

export type { CompanyProfile };

export const companyProfileLookupFacade = {
  getProfile: companyProfileService.getProfile,
};
