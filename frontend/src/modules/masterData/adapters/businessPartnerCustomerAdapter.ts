/**
 * Adapters Customer y Supplier — BusinessPartner V2.
 *
 * V2: businessPartnerId ES el ID operacional.
 * ELIMINADO: legacyOperationalId, pickerMeta, missingOperationalLink, selectable flag.
 * Todos los BP con el rol activo son seleccionables directamente.
 */

import type { BusinessPartnerSummaryDto, CustomerPickerRow, SupplierPickerRow } from '../types/businessPartner.types';

/** Mapea un BP summary a una fila del picker de cliente. */
export function mapBusinessPartnerToCustomerPickerRow(bp: BusinessPartnerSummaryDto): CustomerPickerRow {
  return {
    id:                   bp.id,                               // businessPartnerId — el ID operacional en V2
    identificationNumber: bp.identificationNumber,
    fullName:             bp.tradeName?.trim() || bp.legalName,
    isActive:             bp.isActive,
    hasCustomerRole:      true,                                // la búsqueda ya filtró por role=Customer
  };
}

/** Mapea un BP summary a una fila del picker de proveedor. */
export function mapBusinessPartnerToSupplierPickerRow(bp: BusinessPartnerSummaryDto): SupplierPickerRow {
  return {
    id:                   bp.id,
    identificationNumber: bp.identificationNumber,
    fullName:             bp.tradeName?.trim() || bp.legalName,
    isActive:             bp.isActive,
    hasSupplierRole:      true,
    supplierConfig:       null, // disponible vía GET /roles si se necesita
  };
}
