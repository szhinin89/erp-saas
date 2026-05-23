import type { BusinessPartnerDto } from '../types/businessPartner.types';
import type { SupplierPickerRow, SupplierPickerShape } from '../types/pickerRow.types';
import { resolveSupplierOperationalMeta } from '../api/operationalLinkResolver';

export type SupplierPickerOption = {
  id: string;
  razonSocial: string;
  selectable: boolean;
  missingOperationalLink?: boolean;
  warningMessage?: string;
};

export function mapBusinessPartnerToSupplierPickerRow(bp: BusinessPartnerDto): SupplierPickerRow {
  const pickerMeta = resolveSupplierOperationalMeta(bp);
  const legacyId = pickerMeta.legacyOperationalId ?? bp.id;
  const shape: SupplierPickerShape = {
    id: legacyId,
    taxId: bp.identificationNumber,
    legalName: bp.legalName,
    tradeName: bp.tradeName ?? null,
    primaryContact: null,
    email: bp.email ?? null,
    phone: bp.phone ?? null,
    address: null,
    website: null,
    category: null,
    status: bp.isActive ? 'active' : 'inactive',
    isActive: bp.isActive,
    createdAt: new Date(0).toISOString(),
  };
  return { ...shape, pickerMeta };
}

export function mapBusinessPartnerToSupplierPickerOption(bp: BusinessPartnerDto): SupplierPickerOption {
  const row = mapBusinessPartnerToSupplierPickerRow(bp);
  return {
    id: row.id,
    razonSocial: row.tradeName?.trim() || row.legalName,
    selectable: row.pickerMeta.selectable,
    missingOperationalLink: row.pickerMeta.missingOperationalLink,
    warningMessage: row.pickerMeta.warningMessage,
  };
}
