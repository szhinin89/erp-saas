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

/** Datos del administrador inicial. Sin contraseña: la genera el backend. */
export interface InitialAdministratorPayload {
  username: string;
  firstName: string;
  lastName: string;
  email: string;
}

/**
 * Credenciales temporales: existen SOLO en la respuesta de creación. No se persisten en ningún
 * almacenamiento del navegador ni se vuelven a consultar.
 */
export interface InitialAdministratorCredentials {
  identityUserId: string;
  username: string;
  temporaryPassword: string;
}

export interface CreateCompanyPayload {
  tenantId: string;
  taxId: string;
  legalName: string;
  tradeName?: string | null;
  initialAdministrator?: InitialAdministratorPayload | null;
}

/** Respuesta de POST /api/v1/companies: el detalle más, si se registró, el administrador inicial. */
export interface CompanyCreatedResult extends CompanyDetail {
  initialAdministrator?: InitialAdministratorCredentials | null;
}

export interface UpdateCompanyPayload {
  id: string;
  taxId: string;
  legalName: string;
  tradeName?: string | null;
  isActive: boolean;
}
