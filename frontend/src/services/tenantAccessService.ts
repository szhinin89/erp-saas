import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export type SubscriberCompanyUserMembershipItem = {
  identityUserId: string;
  email: string;
  fullName: string;
  role: string;
  profileId: string | null;
  isActive: boolean;
};

export type TenantUpsertCompanyUserMembershipRequest = {
  email: string;
  role: string;
  profileId?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  password?: string | null;
};

export const subscriberAccessService = {
  listCompanyUserMemberships: (onlyActive = true) =>
    api.get<ApiResponse<SubscriberCompanyUserMembershipItem[]>>('/api/admin/iam/tenant/company_user_memberships', { params: { onlyActive } })
      .then((r) => r.data.responseObject),

  upsertCompanyUserMembership: (req: TenantUpsertCompanyUserMembershipRequest) =>
    api.post<ApiResponse<object>>('/api/admin/iam/tenant/company_user_memberships', req)
      .then((r) => r.data.responseObject),

  revokeCompanyUserMembership: (email: string) =>
    api.post<ApiResponse<object>>('/api/admin/iam/tenant/company_user_memberships/revoke', { email })
      .then((r) => r.data.responseObject),
};

