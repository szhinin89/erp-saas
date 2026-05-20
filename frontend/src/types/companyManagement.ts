export interface CompanyListItem {
  id: string;
  subscriberId: string;
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
  subscriberId: string;
  legalName: string;
  tradeName: string | null;
  taxId: string;
  mainAddress: string;
  phone: string | null;
  email: string | null;
  countryCode: string;
  timezone: string;
  currencyCode: string;
  logoUrl: string | null;
  brandingJson: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCompanyPayload {
  taxId: string;
  legalName: string;
  mainAddress: string;
  tradeName?: string | null;
  phone?: string | null;
  email?: string | null;
  countryCode: string;
  timezone: string;
  currencyCode: string;
  logoUrl?: string | null;
  brandingJson?: string | null;
}

export type UpdateCompanyPayload = CreateCompanyPayload & {
  id: string;
  isActive: boolean;
};
