export interface CompanyLogo {
  id: string;
  url: string;
  lastUpdatedAt: string;
}

export interface CompanyProfile {
  id: string;
  taxIdentificationNumber: string;
  taxIdentificationStatus: "Pending" | "Verified" | "Invalid";
  isTemporaryTaxIdentification: boolean;
  legalName: string;
  tradeName: string | null;
  corporateEmail: string | null;
  phone: string | null;
  website: string | null;
  countryCode: string;
  currencyCode: string;
  timezone: string;
  legalRepName: string | null;
  legalRepPosition: string | null;
  legalRepIdNumber: string | null;
  legalRepEmail: string | null;
  legalRepPhone: string | null;
  taxRegimeCode: string | null;
  isAccountingReq: boolean;
  specialTaxpayerNo: string | null;
  isForeignTrade: boolean;
  withholdsRenta: boolean;
  withholdsVat: boolean;
  languageCode: string;
  operationalStatus: string;
  onboardingCompleted: boolean;
  extraLegend: string | null;
  brandingConfiguration: string | null;
  logo: CompanyLogo | null;
  alternateLogo: CompanyLogo | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
}

export interface UpdateCompanyProfilePayload {
  corporateEmail?: string | null;
  phone?: string | null;
  website?: string | null;
  currencyCode: string;
  timezone: string;
  legalRepName?: string | null;
  legalRepPosition?: string | null;
  legalRepIdNumber?: string | null;
  legalRepEmail?: string | null;
  legalRepPhone?: string | null;
}

export interface UpdateCompanyFiscalPayload {
  taxRegimeCode?: string | null;
  isAccountingReq: boolean;
  specialTaxpayerNo?: string | null;
  isForeignTrade: boolean;
  withholdsRenta: boolean;
  withholdsVat: boolean;
}

export interface UpdateCompanyOperationPayload {
  languageCode: string;
}

export interface UpdateCompanyDocumentsPayload {
  extraLegend?: string | null;
}

export interface CompanyBrandingIdentity {
  primaryColor?: string;
  secondaryColor?: string;
  slogan?: string;
}

export type ConsumerFinalMaxAmountSource =
  | "Manual"
  | "TaxRegimeDefault"
  | "Fallback";

/**
 * Política fiscal efectiva de Consumidor Final. BlockConsumerFinalCredit siempre es `true`
 * (regla fija, no editable) — se expone tal cual la calcula el backend, nunca hardcodeada aquí.
 * Los dos mensajes ya vienen formateados por el backend (incluyen el monto máximo).
 */
export interface SalesFiscalPolicy {
  blockConsumerFinalCredit: boolean;
  consumerFinalMaxAmount: number;
  consumerFinalMaxAmountSource: ConsumerFinalMaxAmountSource;
  taxRegimeCode: string | null;
  creditBlockedMessage: string;
  amountExceededMessage: string;
}

export interface UpdateConsumerFinalMaxAmountPayload {
  consumerFinalMaxAmount: number;
}
