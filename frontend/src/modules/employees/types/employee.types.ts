/** Contratos de `api/v1/employees` (docs/architecture/employees.md). Fechas "YYYY-MM-DD". */

export type PagedResult<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
};

export type EmployeeBusinessPartner = {
  id: string;
  displayName: string | null;
  identificationType: string | null;
  identificationNumber: string | null;
};

export type EmployeeCatalogRef = { id: string; name: string; isActive: boolean };

export type EmployeePeriod = {
  id: string;
  startDate: string;
  endDate: string | null;
  terminationReason: string | null;
};

export type EmployeeAssignment = {
  id: string;
  branchId: string;
  branchName: string;
  branchCode: string | null;
  startDate: string;
  endDate: string | null;
  rowVersion: number;
};

/** Nulo sin vínculo o cuando IAM protege la cuenta: no distingue ambos casos. */
export type EmployeeIdentityUser = {
  id: string;
  username: string;
  membershipActive: boolean;
};

export type EmployeeDto = {
  id: string;
  companyId: string;
  businessPartner: EmployeeBusinessPartner;
  isActive: boolean;
  jobPosition: EmployeeCatalogRef | null;
  profession: EmployeeCatalogRef | null;
  specialties: EmployeeCatalogRef[];
  identityUser: EmployeeIdentityUser | null;
  periods: EmployeePeriod[];
  assignments: EmployeeAssignment[];
  rowVersion: number;
  /** Solo en la respuesta de la operación que la produce; no se persiste. */
  warnings: string[];
};

export type EmployeeListItem = {
  id: string;
  companyId: string;
  businessPartner: EmployeeBusinessPartner;
  isActive: boolean;
  jobPosition: EmployeeCatalogRef | null;
  profession: EmployeeCatalogRef | null;
  rowVersion: number;
};

export type EmployeeListParams = {
  isActive?: boolean;
  search?: string;
  jobPositionId?: string;
  professionId?: string;
  specialtyId?: string;
  branchId?: string;
  asOf?: string;
  skip?: number;
  take?: number;
};

export type CreateEmployeeBody = {
  businessPartnerId: string;
  startDate: string;
  jobPositionId?: string;
  professionId?: string;
  specialtyIds?: string[];
};

/** PATCH: propiedad omitida = sin cambio; `null` = quitar el valor. */
export type UpdateEmployeeBody = {
  rowVersion: number;
  jobPositionId?: string | null;
  professionId?: string | null;
};

export type EmployeeCatalogKind = "job-positions" | "professions" | "specialties";

export type EmployeeCatalogEntry = {
  id: string;
  name: string;
  isActive: boolean;
  rowVersion: number;
};

export type EmployeeCatalogOptions = {
  jobPositions: EmployeeCatalogEntry[];
  professions: EmployeeCatalogEntry[];
  specialties: EmployeeCatalogEntry[];
};

/** Advertencia (no error) de la baja: el usuario vinculado conserva membresía activa. */
export const ACTIVE_MEMBERSHIP_WARNING = "ACTIVE_MEMBERSHIP";
