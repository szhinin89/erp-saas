import { logDevMasterDataPicker } from '../../../lib/observability/devApiLog';
import type { BusinessPartnerDto } from '../types/businessPartner.types';
import {
  extractOperationalLink,
  type BusinessPartnerOperationalLink,
  type OperationalPickerMeta,
} from '../types/operationalLink.types';

const WARN_NO_CUSTOMER =
  'BusinessPartner sin cliente operacional vinculado (legacy CustomerId).';
const WARN_NO_SUPPLIER =
  'BusinessPartner sin proveedor operacional vinculado (legacy SupplierId).';

function hasGuid(value: string | null | undefined): value is string {
  return typeof value === 'string' && value.trim().length > 0;
}

export function resolveCustomerOperationalMeta(
  bp: BusinessPartnerDto,
  link: BusinessPartnerOperationalLink = extractOperationalLink(bp),
): OperationalPickerMeta {
  const legacyOperationalId = hasGuid(link.legacyCustomerId) ? link.legacyCustomerId : null;
  const selectable = legacyOperationalId !== null;
  const missingOperationalLink = !selectable;

  if (missingOperationalLink && import.meta.env.DEV) {
    logDevMasterDataPicker({
      kind: 'customer',
      businessPartnerId: bp.id,
      identificationNumber: bp.identificationNumber,
      legacyCustomerId: null,
      selectable: false,
    });
  }

  return {
    businessPartnerId: bp.id,
    legacyOperationalId,
    selectable,
    missingOperationalLink,
    warningMessage: missingOperationalLink ? WARN_NO_CUSTOMER : undefined,
  };
}

export function resolveSupplierOperationalMeta(
  bp: BusinessPartnerDto,
  link: BusinessPartnerOperationalLink = extractOperationalLink(bp),
): OperationalPickerMeta {
  const legacyOperationalId = hasGuid(link.legacySupplierId) ? link.legacySupplierId : null;
  const selectable = legacyOperationalId !== null;
  const missingOperationalLink = !selectable;

  if (missingOperationalLink && import.meta.env.DEV) {
    logDevMasterDataPicker({
      kind: 'supplier',
      businessPartnerId: bp.id,
      identificationNumber: bp.identificationNumber,
      legacySupplierId: null,
      selectable: false,
    });
  }

  return {
    businessPartnerId: bp.id,
    legacyOperationalId,
    selectable,
    missingOperationalLink,
    warningMessage: missingOperationalLink ? WARN_NO_SUPPLIER : undefined,
  };
}
