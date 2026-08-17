export interface CompanyListItem {
  id: string;
  tenantId: string;
  legalName: string;
  tradeName: string | null;
  taxId: string;
  countryCode: string;
  timezone: string;
  currencyCode: string;
  isActive: boolean;
  role: string;
}

export interface CompanyDetail {
  id: string;
  tenantId: string;
  legalName: string;
  tradeName: string | null;
  taxId: string;
  countryCode: string;
  timezone: string;
  currencyCode: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCompanyPayload {
  taxId: string;
  legalName: string;
  tradeName?: string | null;
}

export type UpdateCompanyPayload = CreateCompanyPayload & {
  id: string;
  isActive: boolean;
};
