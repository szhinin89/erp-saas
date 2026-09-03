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

/** Tenant/grupo derivado de AdminCoreCompany — para selectores (crear empresa) sin exponer el GUID al usuario. */
export interface AdminCoreTenant {
  tenantId: string;
  tenantName: string;
  tenantIsActive: boolean;
}
