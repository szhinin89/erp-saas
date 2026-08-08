/**
 * Códigos SRI de tipo de identificación (Ecuador) — constantes de validación frontend.
 *
 * El catálogo `global.sri_id_type` en backend/BD es la fuente única de verdad; estos
 * valores son solo espejo local para validación de formularios (Zod) y lógica de UI
 * que no puede depender de una llamada async. No usar para poblar selects — para eso,
 * consumir el catálogo vía `useSriIdTypes` / `useSriIdTypesByUsage`.
 */
export const SRI_ID_TYPE_RUC = "04";
export const SRI_ID_TYPE_CEDULA = "05";
export const SRI_ID_TYPE_PASSPORT = "06";
export const SRI_ID_TYPE_CONSUMIDOR_FINAL = "07";
export const SRI_ID_TYPE_EXTERIOR = "08";
export const SRI_ID_TYPE_PLACA = "09";

export const SRI_ID_TYPE_CODES = {
  RUC: SRI_ID_TYPE_RUC,
  CEDULA: SRI_ID_TYPE_CEDULA,
  PASSPORT: SRI_ID_TYPE_PASSPORT,
  CONSUMIDOR_FINAL: SRI_ID_TYPE_CONSUMIDOR_FINAL,
  EXTERIOR: SRI_ID_TYPE_EXTERIOR,
  PLACA: SRI_ID_TYPE_PLACA,
} as const;
