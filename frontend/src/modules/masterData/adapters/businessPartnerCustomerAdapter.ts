import type { BusinessPartnerDto } from '../types/businessPartner.types';
import type { CustomerPickerRow, CustomerPickerShape } from '../types/pickerRow.types';
import { resolveCustomerOperationalMeta } from '../api/operationalLinkResolver';

function normalizeIdType(type: string): 'RUC' | 'CI' {
  return type.trim().toUpperCase() === 'CI' ? 'CI' : 'RUC';
}

export function mapBusinessPartnerToCustomerPickerRow(bp: BusinessPartnerDto): CustomerPickerRow {
  const pickerMeta = resolveCustomerOperationalMeta(bp);
  const legacyId = pickerMeta.legacyOperationalId ?? bp.id;
  const shape: CustomerPickerShape = {
    id: legacyId,
    identificationType: normalizeIdType(bp.identificationType),
    identificationNumber: bp.identificationNumber,
    fullName: bp.tradeName?.trim() || bp.legalName,
    email: bp.email ?? null,
    phone: bp.phone ?? null,
    address: null,
    isActive: bp.isActive,
  };
  return { ...shape, pickerMeta };
}
