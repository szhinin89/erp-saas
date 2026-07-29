import { apiGet, apiPatch, apiPost, apiPut } from "../../lib/apiEnvelope";

export type BranchListItemDto = {
  id: string;
  name: string;
  code: string | null;
  address: string;
  countryId: string | null;
  provinceId: string | null;
  cantonId: string | null;
  parishId: string | null;
  phone: string | null;
  email: string | null;
  managerName: string | null;
  isActive: boolean;
  isMainBranch: boolean;
};

export type BranchDetailDto = {
  id: string;
  name: string;
  code: string | null;
  description: string | null;
  isMainBranch: boolean;
  address: string;
  countryId: string | null;
  provinceId: string | null;
  cantonId: string | null;
  parishId: string | null;
  reference: string | null;
  postalCode: string | null;
  latitude: string | null;
  longitude: string | null;
  phone: string | null;
  secondaryPhone: string | null;
  email: string | null;
  website: string | null;
  managerName: string | null;
  managerPosition: string | null;
  managerEmail: string | null;
  managerPhone: string | null;
  openingDate: string | null;
  internalNotes: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
  createdBy: string;
  updatedBy: string | null;
};

export type BranchPayload = {
  name: string;
  address: string;
  description: string | null;
  reference: string | null;
  postalCode: string | null;
  phone: string | null;
  secondaryPhone: string | null;
  email: string | null;
  website: string | null;
  managerName: string | null;
  managerPosition: string | null;
  managerEmail: string | null;
  managerPhone: string | null;
  countryId: string | null;
  provinceId: string | null;
  cantonId: string | null;
  parishId: string | null;
  latitude: string | null;
  longitude: string | null;
  openingDate: string | null;
  internalNotes: string | null;
  isActive: boolean;
  isMainBranch: boolean;
};

export type GeographyItemDto = { id: string; name: string };

export type CatalogActiveStatus = "all" | "active" | "inactive";

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
    return { id: String(row[0] ?? ""), name: String(row[1] ?? "") };
  }
  if (row && typeof row === "object") {
    const o = row as GeoRow;
    const id = String(o.id ?? o.Id ?? o.item1 ?? o.Item1 ?? "");
    const name = String(o.name ?? o.Name ?? o.item2 ?? o.Item2 ?? "");
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
  q.set("activeStatus", activeStatus);
  if (search?.trim()) q.set("search", search.trim());
  return `?${q.toString()}`;
}

function getGeography(url: string) {
  return apiGet<unknown>(url).then(normalizeGeographyList);
}

export const branchService = {
  list: (activeStatus: CatalogActiveStatus = "all", search?: string) =>
    apiGet<BranchListItemDto[]>(
      `/api/v1/settings/branches${listQuery(activeStatus, search)}`,
    ),

  getById: (id: string) =>
    apiGet<BranchDetailDto>(`/api/v1/settings/branches/${id}`),

  create: (body: BranchPayload) =>
    apiPost<BranchListItemDto>("/api/v1/settings/branches", body),

  update: (id: string, body: BranchPayload & { id: string }) =>
    apiPut<BranchListItemDto>(`/api/v1/settings/branches/${id}`, body),

  disable: (id: string) =>
    apiPatch<BranchListItemDto>(`/api/v1/settings/branches/${id}/disable`),

  enable: (id: string) =>
    apiPatch<BranchListItemDto>(`/api/v1/settings/branches/${id}/enable`),

  countries: () => getGeography("/api/v1/settings/geography/countries"),

  provinces: (countryId: string) =>
    getGeography(
      `/api/v1/settings/geography/provinces?countryId=${encodeURIComponent(countryId)}`,
    ),

  cantons: (provinceId: string) =>
    getGeography(
      `/api/v1/settings/geography/cantons?provinceId=${encodeURIComponent(provinceId)}`,
    ),

  parishes: (cantonId: string) =>
    getGeography(
      `/api/v1/settings/geography/parishes?cantonId=${encodeURIComponent(cantonId)}`,
    ),
};
