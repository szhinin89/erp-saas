import { useCallback, useEffect, useMemo, useState } from 'react';
import { kardexService } from '../../stock/api/kardexService';
import type { KardexMovementDetailDto } from '../../stock/api/kardexService';
import { stockService } from '../../stock/api/stockService';
import type { CurrentStockDto, StockMovementDto } from '../../stock/api/stockService';
import { warehouseService } from '../../warehouses/api/warehouseService';
import type { WarehouseDto } from '../../warehouses/api/warehouseService';
import { itemService } from '../../../items/api/itemService';
import type { ItemDto } from '../../../../types/items';
import { purchaseService } from '../../../purchases/api/purchaseService';
import { salesService } from '../../../sales/api/salesService';
import { message } from '../../../../lib/messages';

export type InvestigationTab = 'kardex' | 'analisis' | 'conteo-fisico';
export type SearchMode = 'product' | 'document';
export type DocumentSubType = 'PurchaseInvoice' | 'SalesInvoice';

export type DocumentMatch = { id: string; docType: DocumentSubType; number: string; label: string };

export type InitialDocument = { id: string; docType: DocumentSubType };

export type InitialFilters = {
  warehouseId?: string;
  dateFrom?: string;
  dateTo?: string;
  movementTypeFilter?: string;
};

export function useInventoryInvestigationPage(
  initialProductId?: string, initialDocument?: InitialDocument, initialFilters?: InitialFilters,
) {
  const [tab] = useState<InvestigationTab>('kardex');

  // ── Modo de búsqueda explícito (nunca heurística) ──────────────────────
  const [searchMode, setSearchMode] = useState<SearchMode>(initialDocument ? 'document' : 'product');

  // ── Modo Producto ───────────────────────────────────────────────────────
  const [productQuery, setProductQuery] = useState('');
  const [productResults, setProductResults] = useState<ItemDto[]>([]);
  const [productSearching, setProductSearching] = useState(false);
  const [selectedProduct, setSelectedProduct] = useState<ItemDto | null>(null);
  const [selectedProductId, setSelectedProductId] = useState<string | undefined>(initialProductId);
  const [selectedWarehouseId, setSelectedWarehouseId] = useState<string>(initialFilters?.warehouseId ?? '');
  const [dateFrom, setDateFrom] = useState(initialFilters?.dateFrom ?? '');
  const [dateTo, setDateTo] = useState(initialFilters?.dateTo ?? '');

  // ── Modo Documento ───────────────────────────────────────────────────────
  const [docSubType, setDocSubType] = useState<DocumentSubType>('PurchaseInvoice');
  const [docNumberQuery, setDocNumberQuery] = useState('');
  const [docMatches, setDocMatches] = useState<DocumentMatch[]>([]);
  const [docSearching, setDocSearching] = useState(false);
  const [selectedDoc, setSelectedDoc] = useState<DocumentMatch | null>(null);

  // ── Resultados ───────────────────────────────────────────────────────────
  const [movements, setMovements] = useState<StockMovementDto[]>([]);
  const [movementsLoading, setMovementsLoading] = useState(false);
  const [movementTypeFilter, setMovementTypeFilter] = useState<string>(initialFilters?.movementTypeFilter ?? '');

  // ── Resumen superior (solo modo Producto) ──────────────────────────────
  const [summary, setSummary] = useState<CurrentStockDto | null>(null);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);

  // ── Expediente ───────────────────────────────────────────────────────────
  const [detail, setDetail] = useState<KardexMovementDetailDto | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);

  // Entrada desde la ficha de producto (?productId=...): precarga el nombre visible.
  useEffect(() => {
    if (!initialProductId) return;
    itemService.getById(initialProductId)
      .then(item => setSelectedProduct(item))
      .catch(() => { /* el Id sigue siendo válido para buscar aunque no se muestre el nombre */ });
  }, [initialProductId]);

  // Entrada desde Compras/Ventas (?docId=&docType=): resuelve el número en su dominio dueño.
  useEffect(() => {
    if (!initialDocument) return;
    setDocSubType(initialDocument.docType);
    const resolve = initialDocument.docType === 'PurchaseInvoice'
      ? purchaseService.getById(initialDocument.id)
      : salesService.getById(initialDocument.id);
    resolve
      .then(inv => setSelectedDoc({
        id: initialDocument.id, docType: initialDocument.docType,
        number: inv.invoiceNumber, label: `${inv.invoiceNumber} — ${inv.status}`,
      }))
      .catch(() => setSelectedDoc({
        id: initialDocument.id, docType: initialDocument.docType,
        number: initialDocument.id, label: initialDocument.id,
      }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialDocument?.id, initialDocument?.docType]);

  const loadWarehouses = useCallback(async () => {
    try {
      const list = await warehouseService.list('active');
      setWarehouses(list);
    } catch { /* no bloquea la búsqueda */ }
  }, []);

  // ── Búsqueda rápida: Producto ────────────────────────────────────────────
  const searchProducts = useCallback(async (q: string) => {
    setProductQuery(q);
    if (q.trim().length < 2) { setProductResults([]); return; }
    setProductSearching(true);
    try {
      const res = await itemService.getAll({ search: q.trim(), isActive: true, pageSize: 10 });
      setProductResults(res.items);
    } catch { setProductResults([]); }
    setProductSearching(false);
  }, []);

  const selectProduct = useCallback((item: ItemDto) => {
    setSelectedProduct(item);
    setSelectedProductId(item.id);
    setProductQuery('');
    setProductResults([]);
  }, []);

  const clearSelectedProduct = useCallback(() => {
    setSelectedProduct(null);
    setSelectedProductId(undefined);
  }, []);

  // ── Búsqueda rápida: Documento — resuelve SIEMPRE en el dominio dueño ──
  const searchDocuments = useCallback(async () => {
    if (!docNumberQuery.trim()) return;
    setDocSearching(true);
    setDocMatches([]);
    try {
      if (docSubType === 'PurchaseInvoice') {
        const res = await purchaseService.list(docNumberQuery.trim(), undefined, 1, 10);
        setDocMatches(res.items.map(i => ({
          id: i.id, docType: 'PurchaseInvoice' as const,
          number: i.invoiceNumber, label: `${i.invoiceNumber} — ${i.status}`,
        })));
      } else {
        const res = await salesService.list(docNumberQuery.trim(), undefined, 1, 10);
        setDocMatches(res.items.map(i => ({
          id: i.id, docType: 'SalesInvoice' as const,
          number: i.invoiceNumber, label: `${i.invoiceNumber} — ${i.status}`,
        })));
      }
    } catch {
      message.error('No se pudo buscar el documento.');
    }
    setDocSearching(false);
  }, [docNumberQuery, docSubType]);

  const selectDocument = useCallback((doc: DocumentMatch) => {
    setSelectedDoc(doc);
    setDocMatches([]);
  }, []);

  const clearSelectedDocument = useCallback(() => {
    setSelectedDoc(null);
  }, []);

  // ── Ejecutar búsqueda (según modo activo) ───────────────────────────────
  const runSearch = useCallback(async () => {
    setMovementsLoading(true);
    setMovements([]);
    setSummary(null);
    try {
      if (searchMode === 'product') {
        if (!selectedProductId) { message.error('Seleccione un producto.'); return; }
        const list = await kardexService.getByProduct(selectedProductId, {
          warehouseId: selectedWarehouseId || undefined,
          from: dateFrom || undefined,
          to: dateTo || undefined,
        });
        setMovements(list);

        if (selectedWarehouseId) {
          const stocks = await stockService.getStock(selectedProductId, selectedWarehouseId);
          setSummary(stocks[0] ?? null);
        } else {
          const agg = await stockService.getAggregated(selectedProductId);
          setSummary({
            id: '', itemId: selectedProductId, warehouseId: '',
            quantity: agg.totalQuantity, reservedQuantity: 0, availableQuantity: agg.totalQuantity,
            totalStockValue: agg.totalStockValue, averageCost: agg.averageCost, lastUpdatedAt: '',
          });
        }
      } else {
        if (!selectedDoc) { message.error('Seleccione un documento.'); return; }
        const list = await kardexService.getByDocument(selectedDoc.id, selectedDoc.docType);
        setMovements(list);
      }
    } catch {
      message.error('No se pudo consultar el Kardex.');
    }
    setMovementsLoading(false);
  }, [searchMode, selectedProductId, selectedWarehouseId, dateFrom, dateTo, selectedDoc]);

  const lastMovement = useMemo(() => {
    if (movements.length === 0) return null;
    return movements.reduce((latest, m) => (m.createdAt > latest.createdAt ? m : latest), movements[0]);
  }, [movements]);

  const visibleMovements = useMemo(
    () => movementTypeFilter ? movements.filter(m => m.movementTypeName === movementTypeFilter) : movements,
    [movements, movementTypeFilter],
  );

  // ── Expediente ───────────────────────────────────────────────────────────
  const openDetail = useCallback(async (movementId: string) => {
    setDetailOpen(true);
    setDetailLoading(true);
    setDetail(null);
    try {
      const d = await kardexService.getMovementDetail(movementId);
      setDetail(d);
    } catch {
      message.error('No se pudo cargar el expediente del movimiento.');
      setDetailOpen(false);
    }
    setDetailLoading(false);
  }, []);

  const closeDetail = useCallback(() => {
    setDetailOpen(false);
    setDetail(null);
  }, []);

  const goToRelated = useCallback((movementId: string) => {
    void openDetail(movementId);
  }, [openDetail]);

  return {
    tab,
    searchMode, setSearchMode,
    // producto
    productQuery, productResults, productSearching, searchProducts, selectProduct, clearSelectedProduct,
    selectedProduct, selectedProductId, setSelectedProductId,
    selectedWarehouseId, setSelectedWarehouseId, warehouses, loadWarehouses,
    dateFrom, setDateFrom, dateTo, setDateTo,
    // documento
    docSubType, setDocSubType, docNumberQuery, setDocNumberQuery,
    docMatches, docSearching, searchDocuments, selectedDoc, selectDocument, clearSelectedDocument,
    // resultados
    movements: visibleMovements, movementsLoading, runSearch,
    movementTypeFilter, setMovementTypeFilter,
    summary, lastMovement,
    // expediente
    detail, detailLoading, detailOpen, openDetail, closeDetail, goToRelated,
  };
}
