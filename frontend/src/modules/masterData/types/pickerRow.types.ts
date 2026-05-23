import type { Customer } from '../../customers/api/customerService';
import type { Supplier } from '../../compras/suppliers/api/supplierService';
import type { OperationalPickerMeta } from './operationalLink.types';

export type CustomerPickerRow = Customer & {
  pickerMeta: OperationalPickerMeta;
};

export type SupplierPickerRow = Supplier & {
  pickerMeta: OperationalPickerMeta;
};
