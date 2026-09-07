import type {
  BusinessPartnerDetailDto,
  SupplierPickerRow,
  SupplierRoleConfigDto,
} from "../../masterData/types/businessPartner.types";

export type SupplierProfile = {
  ruc: string;
  name: string;
  isActive: boolean;
  config: SupplierRoleConfigDto | null;
  isRequiredToKeepAccounting: boolean;
  /** ADR-033: default de condición de pago para compras/gastos de la empresa activa — única
   * fuente (CompanyBpPurchaseSettings.PaymentTermId). Null si no hay default configurado. */
  purchaseDefaultPaymentTermId: string | null;
};

// El backend incluye este campo adicional en supplierConfig, aunque el contrato
// compartido de masterData solo describe la configuración base del proveedor.
type SupplierConfigWithAccounting = SupplierRoleConfigDto & {
  isRequiredToKeepAccounting?: boolean;
};

function getActiveSupplierRole(bp: BusinessPartnerDetailDto) {
  return bp.roles?.find((r) => r.roleType === "Supplier" && r.isActive);
}

export function buildSupplierProfile(
  bp: BusinessPartnerDetailDto,
  purchaseDefaultPaymentTermId: string | null = null,
): SupplierProfile {
  const role = getActiveSupplierRole(bp);
  const config = role?.supplierConfig ?? null;

  return {
    ruc: bp.identificationNumber,
    name: bp.tradeName || bp.legalName,
    isActive: bp.isActive,
    config,
    isRequiredToKeepAccounting:
      (config as SupplierConfigWithAccounting | null)
        ?.isRequiredToKeepAccounting ?? false,
    purchaseDefaultPaymentTermId,
  };
}

export function buildSupplierPickerRow(
  bp: BusinessPartnerDetailDto,
): SupplierPickerRow {
  const role = getActiveSupplierRole(bp);

  return {
    id: bp.id,
    identificationNumber: bp.identificationNumber,
    fullName: bp.tradeName || bp.legalName,
    isActive: bp.isActive,
    hasSupplierRole: !!role,
    supplierConfig: role?.supplierConfig ?? null,
  };
}

export function buildSupplierInactiveMessage(supplierName: string) {
  return `El proveedor '${supplierName}' se encuentra inactivo.`;
}
