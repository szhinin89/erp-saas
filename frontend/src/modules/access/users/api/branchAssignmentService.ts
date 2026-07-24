import { apiGet, apiPut } from '../../../lib/apiEnvelope';

/** Proyección de GET .../memberships/{membershipId}/branches (Fase I-B). */
export type CompanyUserBranchOptionDto = {
  branchId: string;
  branchName: string;
  authorized: boolean;
};

export type CompanyUserBranchesAdminDto = {
  companyUserId: string;
  branches: CompanyUserBranchOptionDto[];
};

/**
 * El frontend nunca valida pertenencia a la empresa, autorización previa ni IsActive de la
 * sucursal — solo recolecta los BranchId marcados y los envía tal cual. Toda esa validación
 * (existencia, empresa, activa) es responsabilidad exclusiva del backend
 * (UpdateCompanyUserBranchesAdminHandler, Fase I-B).
 */
export const branchAssignmentService = {
  getMembershipBranches: (membershipId: string) =>
    apiGet<CompanyUserBranchesAdminDto>(`/api/v1/admin/iam/memberships/${membershipId}/branches`),

  updateMembershipBranches: (membershipId: string, authorizedBranchIds: string[]) =>
    apiPut<CompanyUserBranchesAdminDto>(`/api/v1/admin/iam/memberships/${membershipId}/branches`, {
      authorizedBranchIds,
    }),
};
