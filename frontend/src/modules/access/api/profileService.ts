import { api } from "../../lib/api";
import type { ApiResponse } from "../../../types/api";

export type Profile = {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
};

export const profileService = {
  list: (onlyActive = true) =>
    api
      .get<ApiResponse<Profile[]>>("/api/v1/admin/iam/profiles", {
        params: { onlyActive },
      })
      .then((r) => r.data.data),

  create: (name: string, description?: string | null) =>
    api
      .post<ApiResponse<Profile>>("/api/v1/admin/iam/profiles", {
        name,
        description: description ?? null,
      })
      .then((r) => r.data.data),

  update: (p: Profile) =>
    api
      .put<ApiResponse<Profile>>(`/api/v1/admin/iam/profiles/${p.id}`, {
        profileId: p.id,
        name: p.name,
        description: p.description,
        isActive: p.isActive,
      })
      .then((r) => r.data.data),

  getPermissions: (profileId: string) =>
    api
      .get<
        ApiResponse<{
          profileId: string;
          items: { permissionKey: string; isAllowed: boolean }[];
        }>
      >(`/api/v1/admin/iam/profiles/${profileId}/permissions`)
      .then((r) => r.data.data),

  upsertPermissions: (
    profileId: string,
    items: { permissionKey: string; isAllowed: boolean }[],
  ) =>
    api
      .put<ApiResponse<PermissionUpsertResult>>(
        `/api/v1/admin/iam/profiles/${profileId}/permissions`,
        {
          profileId,
          items,
        },
      )
      .then((r) => r.data.data),

  getPermissionAudit: (profileId: string) =>
    api
      .get<ApiResponse<ProfilePermissionAudit>>(
        `/api/v1/admin/iam/profiles/${profileId}/permission-audit`,
      )
      .then((r) => r.data.data),
};

export type PermissionUpsertResult = {
  saved: string[];
  rejected: { permissionKey: string; reason: string; rejectionCode: string }[];
  allSaved: boolean;
};

export type ProfilePermissionAudit = {
  profileId: string;
  profileName: string;
  permissions: {
    permissionKey: string;
    isAllowed: boolean;
    auditStatus: "Effective" | "BlockedByPlan" | "UnknownPrefix";
    note: string | null;
  }[];
};
