import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';

export type SubscriberCompanyUserMembershipItem = {
  identityUserId: string;
  email: string;
  fullName: string;
  role: string;
  profileId: string | null;
  isActive: boolean;
};

export type SubscriberUpsertCompanyUserMembershipRequest = {
  email: string;
  role: string;
  profileId?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  password?: string | null;
};

const IAM_SUBSCRIBER_MEMBERSHIPS = '/api/admin/iam/subscriber/company_user_memberships';

export const subscriberAccessService = {
  listCompanyUserMemberships: (onlyActive = true) =>
    api
      .get<ApiResponse<SubscriberCompanyUserMembershipItem[]>>(IAM_SUBSCRIBER_MEMBERSHIPS, {
        params: { onlyActive },
      })
      .then((r) => r.data.responseObject),

  upsertCompanyUserMembership: (req: SubscriberUpsertCompanyUserMembershipRequest) =>
    api
      .post<ApiResponse<object>>(IAM_SUBSCRIBER_MEMBERSHIPS, req)
      .then((r) => r.data.responseObject),

  revokeCompanyUserMembership: (email: string) =>
    api
      .post<ApiResponse<object>>(`${IAM_SUBSCRIBER_MEMBERSHIPS}/revoke`, { email })
      .then((r) => r.data.responseObject),
};
