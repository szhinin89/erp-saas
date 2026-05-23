import type { Customer } from '../../customers/api/customerService';
import type { BusinessPartnerDto } from '../types/businessPartner.types';
import type { CustomerPickerRow } from '../types/pickerRow.types';
import { resolveCustomerOperationalMeta } from '../api/operationalLinkResolver';

function normalizeIdType(type: string): 'RUC' | 'CI' {
  return type.trim().toUpperCase() === 'CI' ? 'CI' : 'RUC';
}

/**
 * Mapea BP → fila de picker de ventas.
 * `id` = legacy CustomerId cuando existe vínculo operacional; si no, id de BP (no seleccionable).
 */
export function mapBusinessPartnerToCustomerPickerRow(bp: BusinessPartnerDto): CustomerPickerRow {
  const pickerMeta = resolveCustomerOperationalMeta(bp);
  const legacyId = pickerMeta.legacyOperationalId ?? bp.id;
  const customer: Customer = {
    id: legacyId,
    identificationType: normalizeIdType(bp.identificationType),
    identificationNumber: bp.identificationNumber,
    fullName: bp.tradeName?.trim() || bp.legalName,
    email: bp.email ?? null,
    phone: bp.phone ?? null,
    address: null,
    isActive: bp.isActive,
  };
  return { ...customer, pickerMeta };
}

/** @deprecated Usar `mapBusinessPartnerToCustomerPickerRow`. */
export function mapBusinessPartnerToLegacyCustomer(
  bp: BusinessPartnerDto,
  legacyId?: string,
): Customer {
  const row = mapBusinessPartnerToCustomerPickerRow({
    ...bp,
    legacyCustomerId: legacyId ?? bp.legacyCustomerId,
  });
  return row;
}
