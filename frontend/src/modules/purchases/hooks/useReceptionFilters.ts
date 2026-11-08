import { useMemo, useState } from "react";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import type { ReceptionSummaryBucket } from "../utils/receptionErpRecord";
import {
  EMPTY_RECEPTION_FILTERS,
  buildReceptionFilterPredicate,
  countActiveAdvancedFilters,
  type ReceptionFilters,
  type ReceptionXmlSessionMap,
} from "../utils/receptionFilters";
import {
  countReceptionDocuments,
  resolveReceptionDocumentBuckets,
} from "../utils/receptionNormalize";

const PAGE_SIZE = 20;

export type ReceptionDateSortOrder = "desc" | "asc";

export interface ReceptionSupplierOption {
  ruc: string;
  name: string;
}

/**
 * PURCHASES-RECEPTION-FILTERS-01 — estado de filtros, orden y paginación de `/purchases/reception`
 * sobre las filas ya importadas (en memoria). Filtrar → ordenar → paginar; todo cambio de criterio
 * vuelve a la página 1. Los KPI NO dependen de esto: se calculan sobre el lote completo.
 */
export function useReceptionFilters(
  items: readonly PurchaseReceptionItem[],
  xmlSession: ReceptionXmlSessionMap,
) {
  const [filters, setFilters] = useState<ReceptionFilters>(EMPTY_RECEPTION_FILTERS);
  const [dateSortOrder, setDateSortOrderState] = useState<ReceptionDateSortOrder>("desc");
  const [page, setPage] = useState(1);

  const supplierOptions = useMemo<ReceptionSupplierOption[]>(() => {
    const byRuc = new Map<string, string>();
    for (const item of items) {
      if (!byRuc.has(item.supplierRuc)) byRuc.set(item.supplierRuc, item.supplierName);
    }
    return [...byRuc.entries()]
      .map(([ruc, name]) => ({ ruc, name }))
      .sort((a, b) => a.name.localeCompare(b.name) || a.ruc.localeCompare(b.ruc));
  }, [items]);

  // issueDate es fecha de negocio "YYYY-MM-DD" (ADR-034): la comparación de strings es cronológica;
  // el sort estable conserva el orden del TXT en empates.
  const bucketOf = useMemo(() => resolveReceptionDocumentBuckets(items), [items]);
  const visibleItems = useMemo(() => {
    const matches = buildReceptionFilterPredicate(filters, xmlSession, bucketOf);
    const direction = dateSortOrder === "asc" ? 1 : -1;
    return items
      .filter(matches)
      .sort((a, b) => direction * a.issueDate.localeCompare(b.issueDate));
  }, [items, filters, xmlSession, bucketOf, dateSortOrder]);

  const shownDocuments = useMemo(() => countReceptionDocuments(visibleItems), [visibleItems]);
  const allDocuments = useMemo(() => countReceptionDocuments(items), [items]);

  // Si una actualización reduce el resultado, no quedar en una página vacía.
  const lastPage = Math.max(1, Math.ceil(visibleItems.length / PAGE_SIZE));
  const currentPage = Math.min(page, lastPage);
  const pagedItems = useMemo(
    () => visibleItems.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE),
    [visibleItems, currentPage],
  );

  const selectedSupplierOptions = useMemo(() => {
    const byRuc = new Map(supplierOptions.map((o) => [o.ruc, o]));
    return filters.supplierRucs
      .map((ruc) => byRuc.get(ruc))
      .filter((o): o is ReceptionSupplierOption => o !== undefined);
  }, [supplierOptions, filters.supplierRucs]);

  const setFilter = <K extends keyof ReceptionFilters>(key: K, value: ReceptionFilters[K]) => {
    setFilters((prev) => ({ ...prev, [key]: value }));
    setPage(1);
  };

  return {
    filters,
    setFilter,
    advancedFilterCount: countActiveAdvancedFilters(filters),
    /** Tarjeta = misma situación que el selector "Situación ERP"; pulsar la activa la desactiva. */
    toggleSummaryBucket: (bucket: ReceptionSummaryBucket) =>
      setFilter("erpSituation", filters.erpSituation === bucket ? "all" : bucket),
    // ZH-SUPPLIER-SEARCH-REUSABLE-01 — conserva el orden de selección y descarta duplicados/vacíos.
    setSupplierFilter: (rucs: readonly string[]) =>
      setFilter("supplierRucs", [...new Set(rucs.filter(Boolean))]),
    supplierOptions,
    selectedSupplierRucs: filters.supplierRucs,
    selectedSupplierOptions,
    dateSortOrder,
    setDateSortOrder: (order: ReceptionDateSortOrder) => {
      setDateSortOrderState(order);
      setPage(1);
    },
    clearFilters: () => {
      setFilters(EMPTY_RECEPTION_FILTERS);
      setDateSortOrderState("desc");
      setPage(1);
    },
    /** Nueva importación: criterios en blanco (el orden elegido se conserva). */
    resetForImport: () => {
      setFilters(EMPTY_RECEPTION_FILTERS);
      setPage(1);
    },
    items: pagedItems,
    /** Filas filtradas antes de paginar (paginación de la tabla). */
    total: visibleItems.length,
    /** "Mostrando N de M comprobantes": comprobantes únicos (un documentId repetido cuenta una vez). */
    shownDocuments,
    allDocuments,
    /** Situación por comprobante (incluye "review" = En revisión) para la columna Registro ERP. */
    documentBucketOf: bucketOf,
    page: currentPage,
    pageSize: PAGE_SIZE,
    setPage,
  };
}
