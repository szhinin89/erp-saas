/**
 * Supplier adapter — re-exporta desde customerAdapter para mantener
 * el archivo existente que algunos imports referencian directamente.
 *
 * V2: ambos adapters usan businessPartnerId directamente como ID operacional.
 */

export {
  mapBusinessPartnerToSupplierPickerRow,
} from './businessPartnerCustomerAdapter';

export type {
  SupplierPickerRow,
} from '../types/businessPartner.types';

/** @deprecated Uso interno de pickers legacy — reemplazado por SupplierPickerRow */
export type SupplierPickerOption = {
  id:          string;
  razonSocial: string;
  selectable:  true;  // siempre true en V2
};
