/**
 * Códigos oficiales SRI (Ficha Técnica, Tabla 4 "Ambiente"): binario técnico estable,
 * no configurable por tenant/empresa — no requiere catálogo persistido (ver backend
 * ERP.Domain.Configuration.Constants.SriEnvironmentCodes, misma fuente de verdad).
 */
export const SRI_ENVIRONMENT = {
  TESTING: 1,
  PRODUCTION: 2,
} as const;

export type SriEnvironmentCode =
  (typeof SRI_ENVIRONMENT)[keyof typeof SRI_ENVIRONMENT];

export function isSriProduction(code: number): boolean {
  return code === SRI_ENVIRONMENT.PRODUCTION;
}
