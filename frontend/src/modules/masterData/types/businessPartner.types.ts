/**
 * BusinessPartner V2 — Contract types
 *
 * RESPONSE fields con enum: son STRINGS (el backend llama .ToString() explícitamente).
 * REQUEST fields con enum: son NUMBERS (integer value del enum de C#).
 *
 * Convención de fechas: ISO 8601 string (el frontend formatea según locale).
 *
 * Enumeraciones como const objects para usar en requests y comparaciones en lógica de UI.
 */

// ── Enumeraciones (valores numéricos para request bodies) ─────────────────────

/** PersonType enum — usar en CreateBusinessPartnerBody.personType */
export const PersonTypeEnum = {
  Natural:      1,
  Legal:        2,
  Government:   3,
  Organization: 4,
} as const;
export type PersonTypeValue = typeof PersonTypeEnum[keyof typeof PersonTypeEnum];

/** RoleType enum — usar en AssignRoleBody.roleType y filtros de búsqueda */
export const RoleTypeEnum = {
  Customer:    1,
  Supplier:    2,
  Employee:    3,
  Carrier:     4,
  Broker:      5,
  Agent:       6,
  Distributor: 7,
  Contractor:  8,
} as const;
export type RoleTypeValue = typeof RoleTypeEnum[keyof typeof RoleTypeEnum];

/** LocationType enum — usar en CreateLocationBody.type */
export const LocationTypeEnum = {
  Matrix:        1,
  Branch:        2,
  Office:        3,
  Warehouse:     4,
  DeliveryPoint: 5,
  Other:         99,
} as const;
export type LocationTypeValue = typeof LocationTypeEnum[keyof typeof LocationTypeEnum];

/**
 * LocationPurpose flags — bitmask, usar en CreateLocationBody.purpose
 * Combinar con OR: Billing | Fiscal = 1 | 4 = 5
 */
export const LocationPurposeEnum = {
  None:           0,
  Billing:        1,
  Delivery:       2,
  Fiscal:         4,
  Correspondence: 8,
} as const;
export type LocationPurposeValue = number; // bitmask — puede combinar valores

/** ContactRole enum — usar en CreateContactBody.role */
export const ContactRoleEnum = {
  Commercial:  1,
  Accounting:  2,
  Management:  3,
  Reception:   4,
  Dispatch:    5,
  Billing:     6,
  Technical:   7,
  Purchasing:  8,
  Legal:       9,
  Other:       99,
} as const;
export type ContactRoleValue = typeof ContactRoleEnum[keyof typeof ContactRoleEnum];

// ── Response DTOs ─────────────────────────────────────────────────────────────

/**
 * Identidad del BP para listas y resultados de búsqueda.
 * No incluye roles — evita N+1. Usar filtro `roles[]` para acotar la búsqueda.
 * Response: GET /api/master/business-partners (items[])
 */
export type BusinessPartnerSummaryDto = {
  id:                   string;
  identificationType:   string;   // SRI code: "04" | "05" | "06" | "07" | "08" | "09"
  identificationNumber: string;
  legalName:            string;
  tradeName:            string | null;
  personType:           string;   // "Natural" | "Legal" | "Government" | "Organization"
  countryCode:          string | null;
  isActive:             boolean;
  createdAt:            string;   // ISO 8601
};

/**
 * Detalle completo — incluye roles con sus configs.
 * Response: GET /api/master/business-partners/{id}
 */
export type BusinessPartnerDetailDto = BusinessPartnerSummaryDto & {
  roles: BusinessPartnerRoleDto[];
};

/**
 * Rol asignado al BP.
 * Response: dentro de BusinessPartnerDetailDto.roles
 *           y GET /api/master/business-partners/{bpId}/roles
 */
export type BusinessPartnerRoleDto = {
  id:                   string;
  roleType:             string;   // "Customer" | "Supplier" | "Employee" | "Carrier" | ...
  roleLabel:            string;   // Etiqueta en español
  isActive:             boolean;
  notes:                string | null;
  assignedAt:           string;
  revokedAt:            string | null;
  supplierConfig:        SupplierRoleConfigDto           | null;  // solo si roleType === "Supplier"
  carrierConfig:         CarrierRoleConfigDto            | null;  // solo si roleType === "Carrier"
  customerConfig:        CustomerRoleConfigDto           | null;  // solo si roleType === "Customer"
  classificationConfig:  SupplierClassificationConfigDto | null;  // solo si roleType === "Supplier"
};

/** Datos de transporte — parte de BusinessPartnerRoleDto cuando roleType="Carrier" */
export type CarrierRoleConfigDto = {
  transportAuthorizationNumber: string | null;
  vehicleCapacityTons:          number | null;
};

/**
 * Config SRI operativa del proveedor — parte de BusinessPartnerRoleDto cuando roleType="Supplier".
 * Incluye S3-A: DefaultPaymentMethodCode + IsRetentionExempt.
 */
export type SupplierRoleConfigDto = {
  defaultTaxSupportCode?:       string | null;
  defaultRetentionVatCode?:     string | null;
  defaultRetentionIncomeCode?:  string | null;
  defaultPaymentMethodCode?:    string | null;  // SRI code: 01-21
  refundProviderTypeCode?:      string | null;  // global.sri_supplier_type: 01=Persona Natural, 02=Sociedad
  isRetentionExempt:            boolean;         // RISE, microempresa, sector público
  paymentTermId:                string;          // FK a master_payment_terms (obligatorio)
};

/**
 * Clasificación estratégica del proveedor (S3-B) — parte de BusinessPartnerRoleDto cuando roleType="Supplier".
 */
export type SupplierClassificationConfigDto = {
  supplierCategory?:        string | null;  // 'Manufacturer' | 'Distributor' | 'ServiceProvider' | 'Agent' | 'Retailer' | 'Other'
  supplierType?:            string | null;  // 'National' | 'International' | 'Both'
  supplierRisk?:            string | null;  // 'Low' | 'Medium' | 'High' | 'Critical'
  supplierRating?:          string | null;  // 'AAA' | 'AA' | 'A' | 'BBB' | 'B' | 'C' | 'D' | 'NR'
  primaryGoodType?:         string | null;  // 'Goods' | 'Services' | 'Both' | 'Digital'
  supplierSegment?:         string | null;  // 'Strategic' | 'Preferred' | 'Approved' | 'Transactional'
  paymentMethodPreference?: string | null;  // texto libre operativo interno
};

/**
 * Clasificación y scoring del cliente — CRM-ready.
 * Parte de BusinessPartnerRoleDto cuando roleType === "Customer".
 * Subscriber-scoped: describe al cliente como tercero, no por empresa.
 */
export type CustomerRoleConfigDto = {
  customerCategory?:       string | null;  // 'Retail' | 'Wholesale' | 'Corporate' | 'Government' | 'VIP' | 'Other'
  customerSegment?:        string | null;  // 'Individual' | 'SMB' | 'MidMarket' | 'Enterprise' | 'StartUp'
  salesZone?:              string | null;  // texto libre: "Norte", "Sur", código de zona
  creditRating?:           string | null;  // 'AAA' | 'AA' | 'A' | 'BBB' | 'B' | 'C' | 'D' | 'NR'
  loyaltyTier?:            string | null;  // 'None' | 'Bronze' | 'Silver' | 'Gold' | 'Platinum'
  preferredInvoiceFormat?: string | null;  // 'PDF' | 'XML' | 'EMAIL' | 'PORTAL' | 'PAPER'
  customerClassification?: string | null;  // 'Nacional' | 'Exportador' | 'Importador' | 'Exento' | 'Especial'
};

/**
 * Ubicación física del BP. Subscriber-scoped.
 * Response: GET /api/master/business-partners/{bpId}/locations
 */
export type BpLocationDto = {
  id:               string;
  businessPartnerId: string;
  name:             string;
  locationType:     string;     // "Matrix" | "Branch" | "Office" | "Warehouse" | "DeliveryPoint" | "Other"
  typeLabel:        string;     // Etiqueta en español
  purposes:         string[];   // ["Facturación", "Entrega", "Fiscal", "Correspondencia"]
  addressLine:      string;
  provinceCode:     string | null;
  cantonCode:       string | null;
  parishCode:       string | null;
  phone:            string | null;
  email:            string | null;
  otherDescription: string | null;  // Presente cuando locationType="Other"
  isPrimary:        boolean;
  isActive:         boolean;
  createdAt:        string;
};

/**
 * Contacto del BP. Subscriber-scoped.
 * Response: GET /api/master/business-partners/{bpId}/contacts
 */
export type BpContactDto = {
  id:               string;
  businessPartnerId: string;
  locationId:       string | null;
  firstName:        string;
  lastName:         string | null;
  fullName:         string;   // Computed: firstName + ' ' + lastName
  position:         string | null;
  contactRole:      string;   // "Commercial" | "Accounting" | "Legal" | ... | "Other"
  roleLabel:        string;   // Etiqueta en español
  otherDescription: string | null;
  phone:            string | null;
  mobile:           string | null;
  email:            string | null;
  notes:            string | null;
  isPrimary:        boolean;
  isActive:         boolean;
  createdAt:        string;
};

/**
 * Configuración comercial del BP en la empresa activa. Company-scoped.
 * Response: GET /api/master/business-partners/{bpId}/trading-settings
 */
export type CompanyBpTradingSettingsDto = {
  id:                     string;
  businessPartnerId:      string;
  creditLimit:            number;
  creditCurrencyCode:     string;   // ISO 4217, default "USD"
  paymentDays:            number;
  paymentTermId:          string | null;
  installments:           number;
  daysBetweenInstallments: number;
  isBlocked:              boolean;
  blockedReason:          string | null;
  blockedAt:              string | null;
  hasCustomConfiguration: boolean;
};

// ── Paginación ────────────────────────────────────────────────────────────────

/** Resultado paginado — data de la API para búsquedas */
export type BusinessPartnerPagedResult = {
  items:       BusinessPartnerSummaryDto[];
  pageNumber:  number;
  pageSize:    number;
  totalCount:  number;
};

// ── Request Bodies (Frontend → Backend) ──────────────────────────────────────

/** POST /api/master/business-partners */
export type CreateBusinessPartnerBody = {
  identificationType:   string;         // SRI code
  identificationNumber: string;
  personType:           PersonTypeValue; // INTEGER: 1=Natural, 2=Legal, 3=Government, 4=Organization
  legalName:            string;
  tradeName?:           string | null;
  countryCode?:         string | null;  // ISO alpha-2
};

/** PUT /api/master/business-partners/{id} */
export type UpdateBusinessPartnerBody = {
  legalName:   string;
  personType:  PersonTypeValue;
  tradeName?:  string | null;
  countryCode?: string | null;
};

/** PATCH /api/master/business-partners/{id}/identification */
export type UpdateIdentificationBody = {
  identificationType:   string;
  identificationNumber: string;
};

/** POST /api/master/business-partners/{bpId}/roles */
export type AssignRoleBody = {
  roleType:        RoleTypeValue;               // INTEGER: Customer=1, Supplier=2, ...
  supplierConfig?:  SupplierConfigBody  | null;  // Solo para roleType=2
  carrierConfig?:   CarrierConfigBody   | null;  // Solo para roleType=4
  customerConfig?:  CustomerConfigBody  | null;  // Solo para roleType=1
};

/** Body para PATCH /{bpId}/roles/{roleId}/customer-config */
export type CustomerConfigBody = {
  customerCategory?:        string | null;
  customerSegment?:         string | null;
  salesZone?:               string | null;
  creditRating?:            string | null;
  loyaltyTier?:             string | null;
  preferredInvoiceFormat?:  string | null;
  customerClassification?:  string | null;
};

// ── Constantes de validación (espejo de CustomerRoleConfig C#) ────────────────
export const CUSTOMER_CATEGORIES    = ['Retail', 'Wholesale', 'Corporate', 'Government', 'VIP', 'Other'] as const;
export const CUSTOMER_SEGMENTS      = ['Individual', 'SMB', 'MidMarket', 'Enterprise', 'StartUp'] as const;
export const CREDIT_RATINGS         = ['AAA', 'AA', 'A', 'BBB', 'BB', 'B', 'C', 'D', 'NR'] as const;
export const LOYALTY_TIERS          = ['None', 'Bronze', 'Silver', 'Gold', 'Platinum'] as const;
export const INVOICE_FORMATS        = ['PDF', 'XML', 'EMAIL', 'PORTAL', 'PAPER'] as const;
export const CUSTOMER_CLASSIFICATIONS = ['Nacional', 'Exportador', 'Importador', 'Exento', 'Especial'] as const;

/** Body para PATCH /{bpId}/roles/{roleId}/supplier-config */
export type SupplierConfigBody = {
  paymentTermId:                string;          // FK obligatorio a master_payment_terms
  defaultTaxSupportCode?:       string | null;
  defaultRetentionVatCode?:     string | null;
  defaultRetentionIncomeCode?:  string | null;
  defaultPaymentMethodCode?:    string | null;  // SRI code 01-21
  refundProviderTypeCode?:      string | null;  // global.sri_supplier_type: 01=Persona Natural, 02=Sociedad
  isRetentionExempt?:           boolean;         // default false
};

/** Body para PATCH /{bpId}/roles/{roleId}/supplier-classification */
export type SupplierClassificationBody = {
  supplierCategory?:        string | null;
  supplierType?:            string | null;
  supplierRisk?:            string | null;
  supplierRating?:          string | null;
  primaryGoodType?:         string | null;
  supplierSegment?:         string | null;
  paymentMethodPreference?: string | null;
};

// ── Constantes de validación (espejo de SupplierRoleConfig + SupplierClassificationConfig C#) ──
export const SRI_PAYMENT_METHOD_CODES  = ['01', '15', '16', '17', '18', '19', '20', '21'] as const;
export const SUPPLIER_CATEGORIES        = ['Manufacturer', 'Distributor', 'ServiceProvider', 'Agent', 'Retailer', 'Other'] as const;
export const SUPPLIER_TYPES             = ['National', 'International', 'Both'] as const;
export const SUPPLIER_RISKS             = ['Low', 'Medium', 'High', 'Critical'] as const;
export const SUPPLIER_RATINGS           = ['AAA', 'AA', 'A', 'BBB', 'BB', 'B', 'C', 'D', 'NR'] as const;
export const SUPPLIER_GOOD_TYPES        = ['Goods', 'Services', 'Both', 'Digital'] as const;
export const SUPPLIER_SEGMENTS          = ['Strategic', 'Preferred', 'Approved', 'Transactional'] as const;

export type CarrierConfigBody = {
  transportAuthorizationNumber?: string | null;
  vehicleCapacityTons?:          number | null;
};

export type UpdateRoleNotesBody = {
  notes: string | null;
};

/** POST /api/master/business-partners/{bpId}/locations */
export type CreateLocationBody = {
  name:             string;
  type:             LocationTypeValue;    // INTEGER: Matrix=1, ..., Other=99
  purpose:          LocationPurposeValue; // BITMASK: None=0, Billing=1, Delivery=2, Fiscal=4, Correspondence=8
  addressLine:      string;
  provinceCode?:    string | null;
  cantonCode?:      string | null;
  parishCode?:      string | null;
  phone?:           string | null;
  email?:           string | null;
  isPrimary?:       boolean;
  otherDescription?: string | null;  // Obligatorio si type=99 (Other)
};

/** PUT /api/master/business-partners/{bpId}/locations/{id} */
export type UpdateLocationBody = Omit<CreateLocationBody, 'isPrimary'>;

/** POST /api/master/business-partners/{bpId}/contacts */
export type CreateContactBody = {
  firstName:        string;
  role:             ContactRoleValue;    // INTEGER: Commercial=1, ..., Legal=9, Other=99
  locationId?:      string | null;
  lastName?:        string | null;
  position?:        string | null;
  phone?:           string | null;
  mobile?:          string | null;
  email?:           string | null;
  notes?:           string | null;
  isPrimary?:       boolean;
  otherDescription?: string | null;  // Obligatorio si role=99 (Other)
};

/** PUT /api/master/business-partners/{bpId}/contacts/{id} */
export type UpdateContactBody = Omit<CreateContactBody, 'isPrimary'>;

/** PUT /api/master/business-partners/{bpId}/trading-settings */
export type UpsertTradingSettingsBody = {
  creditLimit:        number;   // >= 0
  paymentDays:        number;   // >= 0
  creditCurrencyCode: string;   // ISO 4217, default "USD"
};

/** PATCH /api/master/business-partners/{bpId}/trading-settings/block */
export type BlockBody = {
  reason: string;  // Obligatorio, max 500 chars
};

// ── Parámetros de búsqueda ────────────────────────────────────────────────────

/**
 * Query params para GET /api/master/business-partners
 * roles: enviar como parámetro repetido → ?roles=1&roles=2
 */
export type SearchBusinessPartnersParams = {
  q?:        string;
  isActive?: boolean;
  roles?:    RoleTypeValue[];  // RoleType integers: [1] = Customers, [2] = Suppliers, [1,2] = ambos
  skip?:     number;
  take?:     number;
};

// ── Picker types (simplificados — sin legacyId) ───────────────────────────────

/**
 * Fila de picker de cliente — usa businessPartnerId directamente como ID operacional.
 * El concepto de legacyOperationalId fue eliminado en V2.
 */
export type CustomerPickerRow = {
  id:                   string;  // businessPartnerId — este ES el ID operacional
  identificationNumber: string;
  fullName:             string;  // tradeName ?? legalName
  isActive:             boolean;
  hasCustomerRole:      boolean;
};

/**
 * Fila de picker de proveedor — usa businessPartnerId directamente.
 */
export type SupplierPickerRow = {
  id:                   string;  // businessPartnerId — este ES el ID operacional
  identificationNumber: string;
  fullName:             string;
  isActive:             boolean;
  hasSupplierRole:      boolean;
  supplierConfig:       SupplierRoleConfigDto | null;
};
