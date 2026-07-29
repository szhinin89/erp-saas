import { apiGet, apiPost, apiPut } from "../../lib/apiEnvelope";

export type ItemTypeDto = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  sortOrder: number;
};

export type CreateItemTypePayload = {
  code: string;
  name: string;
  sortOrder?: number;
};

export type UpdateItemTypePayload = {
  id: string;
  name: string;
  sortOrder: number;
};

const BASE = "/api/v1/item-types";

export const itemTypeService = {
  list: (onlyActive = false) =>
    apiGet<ItemTypeDto[]>(`${BASE}?onlyActive=${onlyActive}`),

  getById: (id: string) => apiGet<ItemTypeDto>(`${BASE}/${id}`),

  create: (body: CreateItemTypePayload) => apiPost<ItemTypeDto>(BASE, body),

  update: (id: string, body: UpdateItemTypePayload) =>
    apiPut<ItemTypeDto>(`${BASE}/${id}`, body),

  toggle: (id: string) => apiPost<ItemTypeDto>(`${BASE}/${id}/toggle`, {}),
};
