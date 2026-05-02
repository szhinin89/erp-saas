import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export type SecurityUser = {
  id: string;
  fullName: string;
  email: string;
  role: string;
  isActive: boolean;
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
    api.get<ApiResponse<SecurityAdminMatrix>>('/api/security/admin-matrix')
      .then((r) => r.data.responseObject),

  upsertAdminScopes: (req: UpsertAdminScopesRequest) =>
    api.put<ApiResponse<object>>('/api/security/admin-scopes', req)
      .then((r) => r.data.responseObject),
};

