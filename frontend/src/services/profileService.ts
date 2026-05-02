import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export type Profile = {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
};

export const profileService = {
  list: (onlyActive = true) =>
    api.get<ApiResponse<Profile[]>>('/api/access/profiles', { params: { onlyActive } })
      .then((r) => r.data.responseObject),

  create: (name: string, description?: string | null) =>
    api.post<ApiResponse<Profile>>('/api/access/profiles', { name, description: description ?? null })
      .then((r) => r.data.responseObject),

  update: (p: Profile) =>
    api.put<ApiResponse<Profile>>(`/api/access/profiles/${p.id}`, {
      profileId: p.id,
      name: p.name,
      description: p.description,
      isActive: p.isActive,
    }).then((r) => r.data.responseObject),

  getPermissions: (profileId: string) =>
    api.get<ApiResponse<{ profileId: string; items: { permissionKey: string; isAllowed: boolean }[] }>>(
      `/api/access/profiles/${profileId}/permissions`
    ).then((r) => r.data.responseObject),

  upsertPermissions: (profileId: string, items: { permissionKey: string; isAllowed: boolean }[]) =>
    api.put<ApiResponse<object>>(`/api/access/profiles/${profileId}/permissions`, {
      profileId,
      items,
    }).then((r) => r.data.responseObject),
};

