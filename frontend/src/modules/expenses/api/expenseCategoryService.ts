import { apiGet, apiPatch, apiPost, apiPut } from "../../lib/apiEnvelope";

const BASE = "/api/v1/expenses/categories";

export type ExpenseCategoryNodeLevel = "Type" | "Category" | "Subcategory";

export interface ExpenseCategoryNodeDto {
  id: string;
  companyId: string;
  parentId: string | null;
  code: string;
  name: string;
  description: string | null;
  level: ExpenseCategoryNodeLevel;
  accountingAccountId: string | null;
  isActive: boolean;
}

export interface ExpenseCategoryTreeNodeDto extends ExpenseCategoryNodeDto {
  children: ExpenseCategoryTreeNodeDto[];
}

export interface ExpenseCategoryNodePayload {
  code: string;
  name: string;
  level: ExpenseCategoryNodeLevel;
  parentId: string | null;
  accountingAccountId: string | null;
  description?: string | null;
}

export interface UpdateExpenseCategoryNodePayload {
  code: string;
  name: string;
  accountingAccountId: string | null;
  description?: string | null;
}

export const expenseCategoryService = {
  getTree: (includeInactive = true) =>
    apiGet<ExpenseCategoryTreeNodeDto[]>(
      `${BASE}/tree?includeInactive=${includeInactive}`,
    ),

  getById: (id: string) => apiGet<ExpenseCategoryNodeDto>(`${BASE}/${id}`),

  create: (payload: ExpenseCategoryNodePayload) =>
    apiPost<ExpenseCategoryNodeDto>(BASE, payload),

  update: (id: string, payload: UpdateExpenseCategoryNodePayload) =>
    apiPut<ExpenseCategoryNodeDto>(`${BASE}/${id}`, payload),

  activate: (id: string) =>
    apiPatch<ExpenseCategoryNodeDto>(`${BASE}/${id}/activate`, {}),

  deactivate: (id: string) =>
    apiPatch<ExpenseCategoryNodeDto>(`${BASE}/${id}/deactivate`, {}),
};
