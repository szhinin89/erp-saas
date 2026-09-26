/**
 * Adapters Customer y Supplier — BusinessPartner V2.
 *
 * V2: businessPartnerId ES el ID operacional.
 * ELIMINADO: legacyOperationalId, pickerMeta, missingOperationalLink, selectable flag.
 * Todos los BP con el rol activo son seleccionables directamente.
 */

import type {
  BusinessPartnerDetailDto,
  BusinessPartnerSummaryDto,
  CustomerPickerRow,
  SupplierPickerRow,
} from "../types/businessPartner.types";

/** Mapea un BP summary a una fila del picker de cliente. */
export function mapBusinessPartnerToCustomerPickerRow(
  bp: BusinessPartnerSummaryDto,
): CustomerPickerRow {
  return {
    id: bp.id, // businessPartnerId — el ID operacional en V2
    identificationNumber: bp.identificationNumber,
    fullName: bp.tradeName?.trim() || bp.legalName,
    isActive: bp.isActive,
    hasCustomerRole: true, // la búsqueda ya filtró por role=Customer
  };
}

/** Mapea un BP summary a una fila del picker de proveedor. */
export function mapBusinessPartnerToSupplierPickerRow(
  bp: BusinessPartnerSummaryDto,
): SupplierPickerRow {
  return {
    id: bp.id,
    identificationNumber: bp.identificationNumber,
    fullName: bp.tradeName?.trim() || bp.legalName,
    isActive: bp.isActive,
    hasSupplierRole: true,
    supplierConfig: null, // disponible vía GET /roles si se necesita
  };
}

/** Mapea el detalle de un BP (con roles) a una fila de proveedor — usado para hidratar la
 * selección inicial por id en `SupplierSearchSelect` (antes `buildSupplierPickerRow` en compras). */
export function mapBusinessPartnerDetailToSupplierPickerRow(
  bp: BusinessPartnerDetailDto,
): SupplierPickerRow {
  const role = bp.roles?.find((r) => r.roleType === "Supplier" && r.isActive);
  return {
    id: bp.id,
    identificationNumber: bp.identificationNumber,
    fullName: bp.tradeName?.trim() || bp.legalName,
    isActive: bp.isActive,
    hasSupplierRole: !!role,
    supplierConfig: role?.supplierConfig ?? null,
  };
}
