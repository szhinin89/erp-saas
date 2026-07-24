import { apiGet, apiPut } from '../../lib/apiEnvelope';

/**
 * Catálogo cerrado del backend (enum CompanyUserLoginMode, Fase A) — no confundir con un
 * catálogo dinámico de BD. Solo dos valores existen; se hardcodean aquí porque no hay
 * infraestructura de catálogo real detrás (mismo patrón que companyOperationSchema.languageCode).
 */
export const COMPANY_USER_LOGIN_MODES = ['AskBranch', 'DirectToDefault'] as const;
export type CompanyUserLoginMode = (typeof COMPANY_USER_LOGIN_MODES)[number];

/** Proyección mínima expuesta por GET/PUT .../company-users/{id}/preferences (Fase F). */
export type CompanyUserPreferencesDto = {
  companyUserId: string;
  defaultBranchId: string | null;
  loginMode: CompanyUserLoginMode;
};

export type UpdateCompanyUserPreferencesPayload = {
  loginMode: CompanyUserLoginMode;
  defaultBranchId: string | null;
};

export const companyUserPreferencesService = {
  get: (companyUserId: string) =>
    apiGet<CompanyUserPreferencesDto | null>(
      `/api/v1/admin/iam/company-users/${companyUserId}/preferences`,
    ),

  update: (companyUserId: string, payload: UpdateCompanyUserPreferencesPayload) =>
    apiPut<CompanyUserPreferencesDto>(
      `/api/v1/admin/iam/company-users/${companyUserId}/preferences`,
      payload,
    ),
};
