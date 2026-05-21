import { apiGet, apiPatch, apiPost, apiPut } from '../../lib/apiEnvelope';

export type BranchDto = {
  id: string;
  name: string;
  address: string;
  code: string | null;
  branchType: string | null;
  reference: string | null;
  phones: string | null;
  email: string | null;
  managerName: string | null;
  countryId: string | null;
  provinceId: string | null;
  cantonId: string | null;
  parishId: string | null;
  latitude: string | null;
  longitude: string | null;
  storageCapacity: number | null;
  dailySalesGoal: number | null;
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

export function normalizeGeographyList(data: unknown): GeographyItemDto[] {
  if (!Array.isArray(data)) return [];
  const out: GeographyItemDto[] = [];
  for (const row of data) {
    const g = rowToGeo(row);
    if (g) out.push(g);
  }
  return out;
}

function listQuery(activeStatus: CatalogActiveStatus, search?: string) {
  const q = new URLSearchParams();
  q.set('activeStatus', activeStatus);
  if (search?.trim()) q.set('search', search.trim());
  return `?${q.toString()}`;
}

function getGeography(url: string) {
  return apiGet<unknown>(url).then(normalizeGeographyList);
}

export const branchService = {
  list: (activeStatus: CatalogActiveStatus = 'all', search?: string) =>
    apiGet<BranchDto[]>(`/api/settings/branches${listQuery(activeStatus, search)}`),

  getById: (id: string) => apiGet<BranchDetailDto>(`/api/settings/branches/${id}`),

  create: (body: Omit<BranchDto, 'id' | 'code'>) => apiPost<BranchDto>('/api/settings/branches', body),

  update: (id: string, body: Omit<BranchDto, 'id' | 'code'> & { id: string }) =>
    apiPut<BranchDto>(`/api/settings/branches/${id}`, body),

  disable: (id: string) => apiPatch<BranchDto>(`/api/settings/branches/${id}/disable`),

  enable: (id: string) => apiPatch<BranchDto>(`/api/settings/branches/${id}/enable`),

  countries: () => getGeography('/api/settings/geography/countries'),

  provinces: (countryId: string) =>
    getGeography(`/api/settings/geography/provinces?countryId=${encodeURIComponent(countryId)}`),

  cantons: (provinceId: string) =>
    getGeography(`/api/settings/geography/cantons?provinceId=${encodeURIComponent(provinceId)}`),

  parishes: (cantonId: string) =>
    getGeography(`/api/settings/geography/parishes?cantonId=${encodeURIComponent(cantonId)}`),
};
