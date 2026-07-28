import { apiGet, apiPost } from '../../lib/apiEnvelope';

const BASE = '/api/v1/purchases/reception';

export type ItemMatchStatus = 'PENDING' | 'NEEDS_REVIEW' | 'AUTO_MATCHED' | 'MANUALLY_MATCHED';

export interface ItemMatchCandidate {
  itemId: string;
  sku: string;
  shortName: string;
  description: string;
  matchScore: number;
  matchReason: string;
}

export interface PurchaseReceptionLineMatch {
  lineId: string;
  supplierId: string | null;
  supplierCode: string | null;
  supplierAuxCode: string | null;
  description: string;
  quantity: number;
  unitPrice: number;
  itemId: string | null;
  matchStatus: ItemMatchStatus;
  matchedAt: string | null;
  suggestions: ItemMatchCandidate[];
}

export interface BulkMatchEntry {
  purchaseReceptionLineId: string;
  itemId: string;
}

export interface BulkMatchResultEntry {
  purchaseReceptionLineId: string;
  success: boolean;
  error: string | null;
}

export interface PurchaseReceptionItem {
  supplierRuc: string;
  supplierName: string;
  invoiceNumber: string;
  accessKey: string;
  issueDate: string;
  authorizationDate: string;
  total: number;
  supplierExists: boolean;
  purchaseExists: boolean;
  status: 'IMPORTED' | 'PENDING' | 'NEW_SUPPLIER';
  /** Id del PurchaseReceptionDocument persistido (Fase 2) — ya no es solo una vista en memoria. */
  documentId: string;
  /** Estado de ciclo de vida del documento persistido, no confundir con `status` (verificación proveedor/compra). */
  documentStatus: 'IMPORTED' | 'VERIFIED' | 'PROCESSED' | 'CANCELLED';
}

export interface PurchaseReceptionImportResult {
  items: PurchaseReceptionItem[];
  totalParsed: number;
  parseErrorCount: number;
  skippedUnsupportedCount: number;
}

export interface DownloadXmlResult {
  documentId: string;
  status: PurchaseReceptionItem['documentStatus'];
  xmlDownloaded: boolean;
  authorizationNumber: string | null;
  authorizationDate: string | null;
}

/**
 * Línea del borrador — `itemId`/`warehouseId` siempre null en esta fase (sin matcher de ítems).
 * Los campos desde `supplierCode` en adelante son de solo lectura: exactamente lo que trae el XML,
 * para mostrar el detalle completo antes de emparejar el producto.
 */
export interface PurchaseDraftLineDto {
  itemId: string | null;
  description: string;
  quantity: number;
  unitPrice: number;
  vatCode: string;
  warehouseId: string | null;
  notes: string | null;
  discountPct: number;
  iceCode: string | null;
  supplierCode: string;
  supplierAuxCode: string | null;
  discount: number;
  lineSubtotal: number;
  taxCode: string;
  vatPercentage: number;
  taxValue: number;
  totalLine: number;
}

/** Borrador de compra parseado del XML almacenado — para precargar el formulario de Nueva Compra. */
export interface PurchaseDraftDto {
  supplierId: string | null;
  supplierRuc: string;
  supplierName: string;
  docTypeCode: string;
  invoiceNumber: string;
  issueDate: string;
  accessKey: string | null;
  authorizationNumber: string | null;
  authorizationDate: string | null;
  sriPaymentMethodCode: string | null;
  lines: PurchaseDraftLineDto[];
}

export const purchaseReceptionService = {
  importTxt(file: File, onProgress?: (percent: number) => void): Promise<PurchaseReceptionImportResult> {
    const formData = new FormData();
    formData.append('file', file);

    return apiPost<PurchaseReceptionImportResult>(`${BASE}/import`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (event) => {
        if (!onProgress || !event.total) return;
        onProgress(Math.round((event.loaded / event.total) * 100));
      },
    });
  },

  downloadXml(documentId: string): Promise<DownloadXmlResult> {
    return apiPost<DownloadXmlResult>(`${BASE}/${documentId}/download-xml`, {});
  },

  createDraft(documentId: string): Promise<PurchaseDraftDto> {
    return apiPost<PurchaseDraftDto>(`${BASE}/${documentId}/create-draft`, {});
  },

  getLines(documentId: string): Promise<PurchaseReceptionLineMatch[]> {
    return apiGet<PurchaseReceptionLineMatch[]>(`${BASE}/${documentId}/lines`);
  },

  matchItem(lineId: string, itemId: string): Promise<PurchaseReceptionLineMatch> {
    return apiPost<PurchaseReceptionLineMatch>(`${BASE}/lines/${lineId}/match-item`, { itemId });
  },

  bulkMatch(matches: BulkMatchEntry[]): Promise<{ results: BulkMatchResultEntry[] }> {
    return apiPost<{ results: BulkMatchResultEntry[] }>(`${BASE}/matching/bulk`, matches);
  },
};
