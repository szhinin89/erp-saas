import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export type TenantMembershipItem = {
  identityUserId: string;
  email: string;
  fullName: string;
  role: string;
  profileId: string | null;
  isActive: boolean;
};

export type TenantUpsertMembershipRequest = {
  email: string;
  role: string;
  profileId?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  password?: string | null;
};

export const tenantAccessService = {
  listMemberships: (onlyActive = true) =>
    api.get<ApiResponse<TenantMembershipItem[]>>('/api/admin/iam/tenant/memberships', { params: { onlyActive } })
      .then((r) => r.data.responseObject),

  upsertMembership: (req: TenantUpsertMembershipRequest) =>
    api.post<ApiResponse<object>>('/api/admin/iam/tenant/memberships', req)
      .then((r) => r.data.responseObject),

  revokeMembership: (email: string) =>
    api.post<ApiResponse<object>>('/api/admin/iam/tenant/memberships/revoke', { email })
      .then((r) => r.data.responseObject),
};

