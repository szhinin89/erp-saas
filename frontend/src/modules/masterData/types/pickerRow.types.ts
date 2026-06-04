/**
 * Picker row types para selectores de cliente/proveedor en formularios.
 *
 * V2: el concepto de legacyOperationalId fue eliminado.
 * El businessPartnerId ES el ID operacional — no hay puente legacy.
 * Los pickers son siempre seleccionables si el BP tiene el rol activo.
 *
 * Los tipos CustomerPickerRow / SupplierPickerRow se definen canónicamente
 * en businessPartner.types.ts — estos re-exports mantienen compatibilidad
 * con los imports existentes en adaptadores y fachada.
 */

export type { CustomerPickerRow, SupplierPickerRow } from './businessPartner.types';

/** Forma mínima de cliente necesaria para pickers de ventas. */
export type CustomerPickerShape = {
  id:                   string;  // businessPartnerId
  identificationType:   string;
  identificationNumber: string;
  fullName:             string;
  isActive:             boolean;
};

/** Forma mínima de proveedor necesaria para pickers de compras. */
export type SupplierPickerShape = {
  id:                   string;  // businessPartnerId
  identificationNumber: string;
  legalName:            string;
  tradeName:            string | null;
  isActive:             boolean;
};
