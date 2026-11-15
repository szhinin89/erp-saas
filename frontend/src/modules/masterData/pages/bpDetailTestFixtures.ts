import type {
  BpContactDto,
  BpLocationDto,
  BusinessPartnerDetailDto,
  BusinessPartnerRoleDto,
} from "../types/businessPartner.types";

/** Datos y permisos compartidos por las pruebas de la ficha unificada de Business Partner. */

export const PERM = {
  view: "masterdata.businesspartners.view",
  update: "masterdata.businesspartners.update",
  disable: "masterdata.businesspartners.disable",
  configure: "masterdata.businesspartners.configure-company",
} as const;

export const ALL_PERMS: string[] = Object.values(PERM);

export function permsOf(granted: string[]) {
  return {
    canShow: (p: string) => granted.includes(p),
    has: (p: string) => granted.includes(p),
    isAdminRole: false,
  };
}

export const CUSTOMER_ROLE: BusinessPartnerRoleDto = {
  id: "role-c",
  roleType: "Customer",
  roleLabel: "Cliente",
  isActive: true,
  notes: null,
  assignedAt: "2026-08-01T00:00:00Z",
  revokedAt: null,
  supplierConfig: null,
  carrierConfig: null,
  customerConfig: { customerCategory: null, salesZone: "Sur" },
};

export const SUPPLIER_ROLE: BusinessPartnerRoleDto = {
  id: "role-s",
  roleType: "Supplier",
  roleLabel: "Proveedor",
  isActive: true,
  notes: null,
  assignedAt: "2026-08-01T00:00:00Z",
  revokedAt: null,
  supplierConfig: {
    defaultTaxSupportCode: null,
    defaultPaymentMethodCode: null,
    refundProviderTypeCode: null,
    isRetentionExempt: false,
    isRequiredToKeepAccounting: false,
  },
  carrierConfig: null,
  customerConfig: null,
};

export function makeBp(
  roles: BusinessPartnerRoleDto[],
  overrides: Partial<BusinessPartnerDetailDto> = {},
): BusinessPartnerDetailDto {
  return {
    id: "bp-1",
    identificationType: "05",
    identificationNumber: "1710034065",
    legalName: "Cliente Uno",
    tradeName: null,
    legalEntityTypeCode: 1,
    countryCode: "EC",
    isActive: true,
    createdAt: "2026-08-01T00:00:00Z",
    isCustomer: roles.some((r) => r.roleType === "Customer" && r.isActive),
    isSupplier: roles.some((r) => r.roleType === "Supplier" && r.isActive),
    canAssignAsCustomer: false,
    canAssignAsSupplier: false,
    roles,
    ...overrides,
  };
}

export const LOCATION: BpLocationDto = {
  id: "loc-1",
  businessPartnerId: "bp-1",
  name: "Matriz Quito",
  locationType: "Matrix",
  typeLabel: "Matriz",
  purposes: [],
  addressLine: "Av. Siempre Viva 123",
  provinceCode: null,
  cantonCode: null,
  parishCode: null,
  phone: null,
  email: null,
  otherDescription: null,
  isPrimary: true,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
};

export const CONTACT: BpContactDto = {
  id: "con-1",
  businessPartnerId: "bp-1",
  locationId: null,
  firstName: "Ana",
  lastName: "Perez",
  fullName: "Ana Perez",
  position: null,
  contactRole: "Commercial",
  roleLabel: "Comercial",
  otherDescription: null,
  phone: null,
  mobile: null,
  email: null,
  notes: null,
  isPrimary: false,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
};
