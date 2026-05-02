import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export type BranchDto = {
  id: string;
  name: string;
  address: string;
  reference: string | null;
  phones: string | null;
  countryId: string | null;
  provinceId: string | null;
  cantonId: string | null;
  parishId: string | null;
  latitude: string | null;
  longitude: string | null;
  rechargeOption: string | null;
  isActive: boolean;
  isMainBranch: boolean;
};

export type BranchDetailDto = BranchDto & {
  createdAt: string;
  updatedAt: string | null;
  createdBy: string;
  updatedBy: string | null;
};

export type GeographyItemDto = { id: string; name: string };

export type CatalogActiveStatus = 'all' | 'active' | 'inactive';

type GeoRow = {
  id?: string;
  name?: string;
  Id?: string;
  Name?: string;
  item1?: string;
  item2?: string;
  Item1?: string;
  Item2?: string;
};

function rowToGeo(row: unknown): GeographyItemDto | null {
  if (Array.isArray(row) && row.length >= 2) {
    return { id: String(row[0] ?? ''), name: String(row[1] ?? '') };
  }
  if (row && typeof row === 'object') {
    const o = row as GeoRow;
    const id = String(o.id ?? o.Id ?? o.item1 ?? o.Item1 ?? '');
    const name = String(o.name ?? o.Name ?? o.item2 ?? o.Item2 ?? '');
    if (id.length > 0) return { id, name };
  }
  return null;
}

/** Acepta id/name, Id/Name, tuplas JSON, o nombres tipo Item1/Item2. */
export function normalizeGeographyList(data: unknown): GeographyItemDto[] {
  if (!Array.isArray(data)) return [];
  const out: GeographyItemDto[] = [];
  for (const row of data) {
    const g = rowToGeo(row);
    if (g) out.push(g);
  }
  return out;
}

function readEnvelopePayload<T>(body: unknown): T {
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if ('responseObject' in o && o.responseObject !== undefined) return o.responseObject as T;
    if ('ResponseObject' in o && o.ResponseObject !== undefined) return o.ResponseObject as T;
  }
  return body as T;
}

function get<T>(url: string) {
  return api.get<ApiResponse<T> | Record<string, unknown>>(url).then((r) => readEnvelopePayload<T>(r.data));
}

function getGeography(url: string) {
  return get<unknown>(url).then(normalizeGeographyList);
}

function post<T>(url: string, body: unknown) {
  return api.post<ApiResponse<T> | Record<string, unknown>>(url, body).then((r) => readEnvelopePayload<T>(r.data));
}

function put<T>(url: string, body: unknown) {
  return api.put<ApiResponse<T> | Record<string, unknown>>(url, body).then((r) => readEnvelopePayload<T>(r.data));
}

function patch<T>(url: string) {
  return api.patch<ApiResponse<T> | Record<string, unknown>>(url, {}).then((r) => readEnvelopePayload<T>(r.data));
}

function listQuery(activeStatus: CatalogActiveStatus, search?: string) {
  const q = new URLSearchParams();
  q.set('activeStatus', activeStatus);
  if (search?.trim()) q.set('search', search.trim());
  return `?${q.toString()}`;
}

export const branchService = {
  list: (activeStatus: CatalogActiveStatus = 'all', search?: string) =>
    get<BranchDto[]>(`/api/branches${listQuery(activeStatus, search)}`),

  getById: (id: string) => get<BranchDetailDto>(`/api/branches/${id}`),

  create: (body: Omit<BranchDto, 'id' | 'isActive' | 'isMainBranch'> & { isActive: boolean; isMainBranch: boolean }) =>
    post<BranchDto>('/api/branches', body),

  update: (id: string, body: Omit<BranchDto, 'id'> & { id: string }) =>
    put<BranchDto>(`/api/branches/${id}`, body),

  disable: (id: string) => patch<BranchDto>(`/api/branches/${id}/disable`),
  enable: (id: string) => patch<BranchDto>(`/api/branches/${id}/enable`),

  countries: () => getGeography('/api/geography/countries'),
  provinces: (countryId: string) =>
    getGeography(`/api/geography/provinces?countryId=${encodeURIComponent(countryId)}`),
  cantons: (provinceId: string) =>
    getGeography(`/api/geography/cantons?provinceId=${encodeURIComponent(provinceId)}`),
  parishes: (cantonId: string) =>
    getGeography(`/api/geography/parishes?cantonId=${encodeURIComponent(cantonId)}`),
};
