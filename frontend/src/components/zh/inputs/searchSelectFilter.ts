/**
 * Helpers puros de filtrado local para `ZhSearchSelect` (ZH-SUPPLIER-SEARCH-REUSABLE-01).
 * Sin conocimiento de dominio: el consumidor decide qué texto de cada opción es buscable.
 */

/** Minúsculas + sin tildes/diacríticos + espacios colapsados — búsqueda case/accent-insensitive. */
export function normalizeSearchText(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .toLowerCase()
    .replace(/\s+/g, " ")
    .trim();
}

export interface SearchIndexEntry<T> {
  option: T;
  key: string;
  text: string;
}

/** Precalcula el texto normalizado de cada opción una sola vez (memoizable por el consumidor). */
export function buildSearchIndex<T>(
  options: readonly T[],
  getKey: (option: T) => string,
  getSearchText: (option: T) => string,
): SearchIndexEntry<T>[] {
  return options.map((option) => ({
    option,
    key: getKey(option),
    text: normalizeSearchText(getSearchText(option)),
  }));
}

/**
 * Devuelve como máximo `maxResults` coincidencias (substring sobre el texto normalizado),
 * excluyendo `excludeKeys` (p.ej. ya seleccionadas en modo múltiple). Corta el recorrido al
 * llegar al límite, así con miles de opciones nunca se construye/renderiza la lista completa.
 */
export function filterSearchIndex<T>(
  index: readonly SearchIndexEntry<T>[],
  query: string,
  maxResults: number,
  excludeKeys?: ReadonlySet<string>,
): T[] {
  const q = normalizeSearchText(query);
  const out: T[] = [];
  for (const entry of index) {
    if (out.length >= maxResults) break;
    if (excludeKeys?.has(entry.key)) continue;
    if (q && !entry.text.includes(q)) continue;
    out.push(entry.option);
  }
  return out;
}
