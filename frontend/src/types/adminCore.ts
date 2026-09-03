export interface AdminCoreCompany {
  tenantId: string;
  tenantName: string;
  tenantIsActive: boolean;
  companyId: string;
  ruc: string;
  legalName: string;
  tradeName: string | null;
  isActive: boolean;
}
