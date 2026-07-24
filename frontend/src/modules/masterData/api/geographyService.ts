import { apiGet } from '../../lib/apiEnvelope';

export type GeoOption = { id: string; name: string };

function normalize(data: unknown): GeoOption[] {
  if (!Array.isArray(data)) return [];
  return data
    .map((r: Record<string, unknown>) => {
      const id = String(r.id ?? r.Id ?? r.item1 ?? r.Item1 ?? '');
      const name = String(r.name ?? r.Name ?? r.item2 ?? r.Item2 ?? '');
      return id ? { id, name } : null;
    })
    .filter(Boolean) as GeoOption[];
}

const BASE = '/api/v1/settings/geography';

export const geographyService = {
  countries:  ()                  => apiGet<unknown>(`${BASE}/countries`).then(normalize),
  provinces:  (countryId: string) => apiGet<unknown>(`${BASE}/provinces?countryId=${encodeURIComponent(countryId)}`).then(normalize),
  cantons:    (provinceId: string)=> apiGet<unknown>(`${BASE}/cantons?provinceId=${encodeURIComponent(provinceId)}`).then(normalize),
  parishes:   (cantonId: string)  => apiGet<unknown>(`${BASE}/parishes?cantonId=${encodeURIComponent(cantonId)}`).then(normalize),
};
