/** DTO unificado MasterData — alineado a `ERP.Application.MasterData.DTOs.BusinessPartnerDto`. */
export type BusinessPartnerDto = {
  id: string;
  legalName: string;
  tradeName?: string | null;
  identificationType: string;
  identificationNumber: string;
  email?: string | null;
  phone?: string | null;
  isCustomer: boolean;
  isSupplier: boolean;
  isActive: boolean;
  customerProfileId?: string | null;
  supplierProfileId?: string | null;
  legacyCustomerId?: string | null;
  legacySupplierId?: string | null;
  // Customer profile data
  customerNotes?: string | null;
  // Supplier SRI defaults
  defaultTaxSupportCode?: string | null;
  defaultRetentionVatCode?: string | null;
  defaultRetentionIncomeCode?: string | null;
  supplierPaymentTerms?: string | null;
};

export type SearchBusinessPartnersParams = {
  q?: string;
  isActive?: boolean;
  isCustomer?: boolean;
  isSupplier?: boolean;
  skip?: number;
  take?: number;
};

export type CreateBusinessPartnerBody = {
  identificationType: string;
  identificationNumber: string;
  legalName: string;
  tradeName?: string | null;
  email?: string | null;
  phone?: string | null;
  countryCode?: string | null;
  asCustomer: boolean;
  asSupplier: boolean;
};

export type UpdateBusinessPartnerBody = {
  identificationType: string;
  identificationNumber: string;
  legalName: string;
  tradeName?: string | null;
  email?: string | null;
  phone?: string | null;
  countryCode?: string | null;
};

export type CompanyBpSettingsDto = {
  businessPartnerId: string;
  creditLimit?: number | null;
  paymentDays: number;
  isBlocked: boolean;
  creditCurrencyCode?: string | null;
};

export type UpdateSupplierProfileBody = {
  defaultTaxSupportCode?: string | null;
  defaultRetentionVatCode?: string | null;
  defaultRetentionIncomeCode?: string | null;
  paymentTerms?: string | null;
};

/** Respuesta cruda del API (camelCase o PascalCase). */
export type BusinessPartnerApiRow = {
  id?: string;
  Id?: string;
  identificationType?: string;
  IdentificationType?: string;
  identificationNumber?: string;
  IdentificationNumber?: string;
  legalName?: string;
  LegalName?: string;
  tradeName?: string | null;
  TradeName?: string | null;
  email?: string | null;
  Email?: string | null;
  phone?: string | null;
  Phone?: string | null;
  isActive?: boolean;
  IsActive?: boolean;
  isCustomer?: boolean;
  IsCustomer?: boolean;
  isSupplier?: boolean;
  IsSupplier?: boolean;
  customerProfileId?: string | null;
  CustomerProfileId?: string | null;
  supplierProfileId?: string | null;
  SupplierProfileId?: string | null;
  legacyCustomerId?: string | null;
  LegacyCustomerId?: string | null;
  legacySupplierId?: string | null;
  LegacySupplierId?: string | null;
  // Customer profile data
  customerNotes?: string | null;
  CustomerNotes?: string | null;
  // Supplier SRI defaults
  defaultTaxSupportCode?: string | null;
  DefaultTaxSupportCode?: string | null;
  defaultRetentionVatCode?: string | null;
  DefaultRetentionVatCode?: string | null;
  defaultRetentionIncomeCode?: string | null;
  DefaultRetentionIncomeCode?: string | null;
  supplierPaymentTerms?: string | null;
  SupplierPaymentTerms?: string | null;
};
