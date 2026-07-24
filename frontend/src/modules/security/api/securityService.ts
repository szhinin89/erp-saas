import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';

export type SecurityUser = {
  id: string;
  fullName: string;
  username: string;
  email: string | null;
  role: string;
  isActive: boolean;
  /** CompanyUserMembership.Id — distinto de `id` (IdentityUser.Id). Necesario para
   * GET/PUT /api/v1/admin/iam/company-users/{companyUserId}/preferences (Fase F). */
  companyUserMembershipId: string;
};

export type SecurityAdminAssignment = {
  subjectType: 'Role' | 'User';
  subjectKey: string;
  scope: number;
  isAllowed: boolean;
};

export type SecurityAdminMatrix = {
  users: SecurityUser[];
  assignments: SecurityAdminAssignment[];
};

export type UpsertAdminScopesRequest = {
  subjectType: 'Role' | 'User';
  subjectKey: string;
  allowedScopes: number[];
};

export const securityService = {
  getAdminMatrix: () =>
    api.get<ApiResponse<SecurityAdminMatrix>>('/api/v1/security/admin-matrix')
      .then((r) => r.data.data),

  upsertAdminScopes: (req: UpsertAdminScopesRequest) =>
    api.put<ApiResponse<object>>('/api/v1/security/admin-scopes', req)
      .then((r) => r.data.data),
};
