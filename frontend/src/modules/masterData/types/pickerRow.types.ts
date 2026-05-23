import type { OperationalPickerMeta } from './operationalLink.types';

/** Forma mínima de cliente necesaria para pickers de ventas. */
export type CustomerPickerShape = {
  id: string;
  identificationType: 'RUC' | 'CI';
  identificationNumber: string;
  fullName: string;
  email: string | null;
  phone: string | null;
  address: string | null;
  isActive: boolean;
};

/** Forma mínima de proveedor necesaria para pickers de compras. */
export type SupplierPickerShape = {
  id: string;
  taxId: string;
  legalName: string;
  tradeName: string | null;
  primaryContact: string | null;
  email: string | null;
  phone: string | null;
  address: string | null;
  website: string | null;
  category: string | null;
  status: 'active' | 'inactive';
  isActive: boolean;
  createdAt: string;
};

export type CustomerPickerRow = CustomerPickerShape & {
  pickerMeta: OperationalPickerMeta;
};

export type SupplierPickerRow = SupplierPickerShape & {
  pickerMeta: OperationalPickerMeta;
};
