import { apiGet, apiPatch, apiPost, apiPut } from '../../../lib/apiEnvelope';

const BASE = '/api/v1/catalog/category-nodes';

export interface CategoryNodeDto {
  id: string;
  parentId: string | null;
  code: string;
  name: string;
  description: string | null;
  level: string;
  path: string;
  depth: number;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface CategoryTreeDto {
  nodes: CategoryNodeDto[];
  // Profundidad máxima configurada por empresa (OrgSettings.catalog.max_category_depth,
  // default 3). Un nodo con depth === maxDepth no debe ofrecerse como padre para crear hijos.
  maxDepth: number;
}

export interface CreateCategoryNodePayload {
  parentId?: string | null;
  code: string;
  name: string;
  description?: string | null;
  level: string;
  sortOrder?: number;
}

export interface UpdateCategoryNodePayload {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  sortOrder?: number;
}

export const categoryNodeService = {
  getTree: (includeInactive = true) =>
    apiGet<CategoryTreeDto>(`${BASE}?includeInactive=${includeInactive}`),

  getById: (id: string) =>
    apiGet<CategoryNodeDto>(`${BASE}/${id}`),

  create: (payload: CreateCategoryNodePayload) =>
    apiPost<CategoryNodeDto>(BASE, payload),

  update: (id: string, payload: UpdateCategoryNodePayload) =>
    apiPut<CategoryNodeDto>(`${BASE}/${id}`, payload),

  disable: (id: string) =>
    apiPatch<boolean>(`${BASE}/${id}/disable`),

  enable: (id: string) =>
    apiPatch<boolean>(`${BASE}/${id}/enable`),
};
