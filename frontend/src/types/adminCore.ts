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

/** Administrador empresarial (no global) de una empresa, para regenerar su contraseña temporal. */
export interface AdminCoreCompanyAdministrator {
  identityUserId: string;
  username: string;
  fullName: string;
  email: string | null;
  /** true mientras no haya cambiado la contraseña temporal. */
  passwordResetPending: boolean;
}

/** Contraseña temporal regenerada: solo existe en esta respuesta, nunca se vuelve a consultar. */
export interface AdminCoreRegeneratedPassword {
  identityUserId: string;
  username: string;
  temporaryPassword: string;
}

/** Tenant/grupo derivado de AdminCoreCompany — para selectores (crear empresa) sin exponer el GUID al usuario. */
export interface AdminCoreTenant {
  tenantId: string;
  tenantName: string;
  tenantIsActive: boolean;
}
