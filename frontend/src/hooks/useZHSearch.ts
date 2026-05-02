import { useCallback, useMemo, useState } from 'react';

function fieldHaystack<T>(row: T, keys: (keyof T)[]): string {
  const parts: string[] = [];
  for (const k of keys) {
    const v = row[k];
    if (v != null && v !== '') parts.push(String(v));
  }
  return parts.join(' ').toLowerCase();
}

function rowMatchesFilter<T>(row: T, key: keyof T, filterValue: string): boolean {
  const raw = row[key];
  const cell = raw == null ? '' : String(raw).trim().toLowerCase();
  const want = filterValue.trim().toLowerCase();
  return cell === want;
}

/**
 * Filtrado en cliente: texto en `searchFields` y coincidencia exacta (string) por filtro.
 */
export function useZHSearch<T>(
  data: T[],
  searchFields: (keyof T)[],
  filterFields?: Record<string, keyof T>
): {
  filteredData: T[];
  query: string;
  setQuery: (q: string) => void;
  filters: Record<string, string>;
  setFilter: (id: string, value: string) => void;
  clearAll: () => void;
  activeFilterCount: number;
} {
  const [query, setQuery] = useState('');
  const [filters, setFilters] = useState<Record<string, string>>({});

  const setFilter = useCallback((id: string, value: string) => {
    setFilters((prev) => ({ ...prev, [id]: value }));
  }, []);

  const clearAll = useCallback(() => {
    setQuery('');
    setFilters({});
  }, []);

  const activeFilterCount = useMemo(() => {
    let n = 0;
    for (const v of Object.values(filters)) {
      if (String(v ?? '').trim() !== '') n += 1;
    }
    if (query.trim() !== '') n += 1;
    return n;
  }, [filters, query]);

  const filteredData = useMemo(() => {
    let out = data ?? [];
    const q = query.trim().toLowerCase();
    if (q) {
      out = out.filter((row) => fieldHaystack(row, searchFields).includes(q));
    }
    if (filterFields && Object.keys(filterFields).length > 0) {
      for (const [filterId, col] of Object.entries(filterFields)) {
        const fv = filters[filterId];
        if (!fv || String(fv).trim() === '') continue;
        out = out.filter((row) => rowMatchesFilter(row, col, fv));
      }
    }
    return out;
  }, [data, query, filters, searchFields, filterFields]);

  return {
    filteredData,
    query,
    setQuery,
    filters,
    setFilter,
    clearAll,
    activeFilterCount,
  };
}
