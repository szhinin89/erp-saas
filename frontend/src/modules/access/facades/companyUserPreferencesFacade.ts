/**
 * companyUserPreferencesFacade — superficie pública de preferencias del
 * usuario de empresa para consumidores externos (security).
 *
 * Expone lectura y la mutación real de guardado (`update`) — no es una
 * facade de solo lectura, porque `security` necesita persistir las
 * preferencias del usuario (modo de login, sucursal por defecto), cuyo
 * endpoint vive en `access`. Los módulos externos deben importar desde
 * aquí, nunca directamente de access/api/companyUserPreferencesService.
 */

import {
  companyUserPreferencesService,
  COMPANY_USER_LOGIN_MODES,
} from "../api/companyUserPreferencesService";
import type {
  CompanyUserPreferencesDto,
  UpdateCompanyUserPreferencesPayload,
  CompanyUserLoginMode,
} from "../api/companyUserPreferencesService";

export { COMPANY_USER_LOGIN_MODES };
export type {
  CompanyUserPreferencesDto,
  UpdateCompanyUserPreferencesPayload,
  CompanyUserLoginMode,
};

export const companyUserPreferencesFacade = {
  get: companyUserPreferencesService.get,
  update: companyUserPreferencesService.update,
};
