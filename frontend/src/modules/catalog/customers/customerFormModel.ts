import type { CustomerDetailDto, CustomerDto } from '../../../services/customerService';
import type { CustomerFormValues } from '../../../schemas/catalog/customerSchema';

/** Tipos de identificación soportados en catálogo (misma lista que API). */
export const CUSTOMER_ID_TYPES = ['RUC', 'CI', 'PASSPORT', 'OTHER'] as const;

export function emptyCustomerForm(): CustomerFormValues {
  return {
    identificationType: 'RUC',
    identificationNumber: '',
    legalName: '',
    tradeName: '',
    addressLine: '',
    phone: '',
    email: '',
    notes: '',
    isActive: true,
  };
}

export function customerFormFromDto(d: CustomerDto | CustomerDetailDto): CustomerFormValues {
  return {
    identificationType: d.identificationType,
    identificationNumber: d.identificationNumber,
    legalName: d.legalName,
    tradeName: d.tradeName ?? '',
    addressLine: d.addressLine ?? '',
    phone: d.phone ?? '',
    email: d.email ?? '',
    notes: d.notes ?? '',
    isActive: d.isActive,
  };
}

/** Cuerpo para crear o actualizar cliente (sin `id`; el update lo añade la página). */
export function customerFormToApiBody(form: CustomerFormValues) {
  return {
    identificationType: form.identificationType.trim(),
    identificationNumber: form.identificationNumber.trim(),
    legalName: form.legalName.trim(),
    tradeName: form.tradeName.trim() || null,
    addressLine: form.addressLine.trim() || null,
    phone: form.phone.trim() || null,
    email: form.email.trim() || null,
    notes: form.notes.trim() || null,
    isActive: form.isActive,
  };
}
