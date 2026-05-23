import type { BusinessPartnerDto } from './businessPartner.types';

/** Vínculo canónico BP ↔ entidades operacionales legacy (resuelto en API). */
export type BusinessPartnerOperationalLink = {
  businessPartnerId: string;
  customerProfileId?: string | null;
  supplierProfileId?: string | null;
  legacyCustomerId?: string | null;
  legacySupplierId?: string | null;
};

export type OperationalPickerMeta = {
  businessPartnerId: string;
  /** Id legacy para payloads operacionales (factura/compra). */
  legacyOperationalId: string | null;
  selectable: boolean;
  missingOperationalLink: boolean;
  warningMessage?: string;
};

export function extractOperationalLink(bp: BusinessPartnerDto): BusinessPartnerOperationalLink {
  return {
    businessPartnerId: bp.id,
    customerProfileId: bp.customerProfileId ?? null,
    supplierProfileId: bp.supplierProfileId ?? null,
    legacyCustomerId: bp.legacyCustomerId ?? null,
    legacySupplierId: bp.legacySupplierId ?? null,
  };
}
