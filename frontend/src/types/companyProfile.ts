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

/**
 * CONFIG-FOUNDATION-P1-02: marca de empresa tipada — ya no un JSON crudo
 * (Company.BrandingConfiguration fue eliminado). Respaldada por org_settings vía
 * ICompanyBrandingResolver. El logo no viaja aquí: sigue siendo CompanyProfile.logo/alternateLogo
 * (archivo, MediaFile) — duplicarlo aquí sería una segunda fuente de verdad.
 */
export interface CompanyBrandingDto {
  primaryColor: string | null;
  secondaryColor: string | null;
  slogan: string | null;
  documentFooterText: string | null;
}

export interface UpdateCompanyBrandingPayload {
  primaryColor?: string | null;
  secondaryColor?: string | null;
  slogan?: string | null;
  documentFooterText?: string | null;
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

/**
 * COMPANY-OPERATING-SETUP-01 — checklist de preparación operativa. Deliberadamente sin texto:
 * el backend solo calcula estado/código, el frontend resuelve label/mensaje vía i18n a partir de
 * `code` (ver OperationalReadinessSection.tsx). No expone entidades de dominio.
 */
export type ReadinessStatus = "Ready" | "Warning" | "Missing" | "NotApplicable";
export type ReadinessSeverity = "Info" | "Warning" | "Blocking";
export type ReadinessBlockingArea =
  | "Sales"
  | "ElectronicInvoicing"
  | "Inventory"
  | "CashRegister";

/**
 * Identificador semántico de la pantalla owner donde se corrige el ítem — el backend nunca
 * conoce el router de React. `operationalReadinessActionRoutes.ts` traduce cada valor a su ruta
 * concreta de React Router; si el backend agrega un target nuevo sin mapeo, el ítem simplemente
 * no muestra acción (fail-safe, nunca navega a una ruta inventada).
 */
export type ReadinessActionTarget =
  | "CompanyProfile"
  | "CompanyBranding"
  | "CompanyFiscalSettings"
  | "CompanySalesSettings"
  | "DecimalSettings"
  | "Branches"
  | "Establishments"
  | "EmissionPoints"
  | "Warehouses"
  | "CashRegisters"
  | "PriceLists"
  | "ElectronicInvoicingSettings"
  | "Items";

export interface CompanyOperationalReadinessItem {
  code: string;
  status: ReadinessStatus;
  severity: ReadinessSeverity;
  blockingArea: ReadinessBlockingArea | null;
  actionTarget: ReadinessActionTarget | null;
}

export interface CompanyOperationalReadinessSection {
  code: string;
  status: ReadinessStatus;
  items: CompanyOperationalReadinessItem[];
}

export interface CompanyOperationalReadiness {
  overallStatus: ReadinessStatus;
  canSell: boolean;
  canIssueElectronicInvoices: boolean;
  canUseInventory: boolean;
  canUseCashRegister: boolean;
  sections: CompanyOperationalReadinessSection[];
}
