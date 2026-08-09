import { apiGet, apiPost } from "../../lib/apiEnvelope";

const BASE = "/api/v1/purchases/reception";

export type ItemMatchStatus =
  "PENDING" | "NEEDS_REVIEW" | "AUTO_MATCHED" | "MANUALLY_MATCHED";

/**
 * Eje independiente del estado fiscal (`documentStatus`): mide si pudimos interpretar el
 * CONTENIDO del XML ya autorizado. Un documento `VERIFIED` puede tener `processingStatus` `FAILED`
 * — el comprobante es fiscalmente válido aunque su detalle no se haya podido interpretar.
 */
export type ProcessingStatus =
  "PENDING" | "PROCESSED" | "PROCESSED_WITH_WARNINGS" | "FAILED";

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

export type PurchaseReceptionSourceDocType =
  | "INVOICE"
  | "CREDIT_NOTE"
  | "DEBIT_NOTE"
  | "RETENTION"
  | "UNKNOWN";

export interface PurchaseReceptionItem {
  supplierRuc: string;
  supplierName: string;
  sourceDocType: PurchaseReceptionSourceDocType;
  invoiceNumber: string;
  /** Factura afectada (`NUMERO_DOCUMENTO_MODIFICADO`) — solo en notas de crédito/débito, null en Factura. */
  modifiedDocumentNumber: string | null;
  accessKey: string;
  issueDate: string;
  authorizationDate: string;
  /** Base imponible del comprobante (TXT SRI) — ya se venía persistiendo, ahora se expone aquí. */
  subtotal: number;
  /** IVA del comprobante (TXT SRI) — ídem. */
  vatAmount: number;
  total: number;
  supplierExists: boolean;
  purchaseExists: boolean;
  /** Solo notas de crédito: si `modifiedDocumentNumber` ya existe como compra del mismo proveedor. False en Factura. */
  affectedPurchaseExists: boolean;
  /** Id de la compra afectada, para abrirla en `/purchases?invoiceId=<id>` — null si no existe o no aplica. */
  affectedPurchaseId: string | null;
  status: "IMPORTED" | "PENDING" | "NEW_SUPPLIER";
  /** Id del PurchaseReceptionDocument persistido (Fase 2) — ya no es solo una vista en memoria. */
  documentId: string;
  /** Estado de ciclo de vida del documento persistido, no confundir con `status` (verificación proveedor/compra). */
  documentStatus: "IMPORTED" | "VERIFIED" | "PROCESSED" | "CANCELLED";
  /** Estado de interpretación del detalle XML — independiente de `documentStatus`. */
  processingStatus: ProcessingStatus;
  /** Resumen legible de advertencias/errores de procesamiento — null si no hay ninguna. */
  processingNotes: string | null;
  /** Nombre comercial del emisor (infoTributaria/nombreComercial) — solo disponible después de
   * "Consultar XML" (el TXT del SRI no lo trae); null hasta entonces o si el XML no lo declara. */
  supplierTradeName: string | null;
}

export interface PurchaseReceptionImportResult {
  items: PurchaseReceptionItem[];
  totalParsed: number;
  parseErrorCount: number;
  skippedUnsupportedCount: number;
}

export interface DownloadXmlResult {
  documentId: string;
  status: PurchaseReceptionItem["documentStatus"];
  xmlDownloaded: boolean;
  authorizationNumber: string | null;
  authorizationDate: string | null;
  processingStatus: ProcessingStatus;
  linesDetectedCount: number;
  linesProcessedCount: number;
  processingNotes: string | null;
  /** infoTributaria/nombreComercial del emisor — null si el XML no lo declara. */
  supplierTradeName: string | null;
}

/**
 * Línea del borrador — proviene 1:1 de la `PurchaseReceptionLine` ya persistida y conciliada
 * (Item Matching): `itemId`/`itemMatchStatus` reflejan el estado real de conciliación, nunca se
 * pierden al pasar de Recepción a Compra. `warehouseId`/`notes` siguen null porque no existen en
 * la línea de recepción — el usuario los completa manualmente. Los campos desde `supplierCode` en
 * adelante son de solo lectura: exactamente lo que trae el XML, para mostrar el detalle completo.
 */
export interface PurchaseDraftLineDto {
  purchaseReceptionLineId: string;
  itemId: string | null;
  itemMatchStatus: ItemMatchStatus;
  description: string;
  quantity: number;
  unitPrice: number;
  vatCode: string;
  warehouseId: string | null;
  notes: string | null;
  discountPct: number;
  iceCode: string | null;
  supplierCode: string | null;
  supplierAuxCode: string | null;
  discount: number;
  lineSubtotal: number;
  taxCode: string;
  vatPercentage: number;
  taxValue: number;
  totalLine: number;
}

/** Borrador de compra armado desde el PurchaseReceptionDocument ya verificado — para precargar el formulario de Nueva Compra. */
export interface PurchaseDraftDto {
  supplierId: string | null;
  supplierRuc: string;
  supplierName: string;
  docTypeCode: string | null;
  invoiceNumber: string;
  issueDate: string;
  accessKey: string | null;
  authorizationNumber: string | null;
  authorizationDate: string | null;
  sriPaymentMethodCode: string | null;
  lines: PurchaseDraftLineDto[];
  /** Siempre PROCESSED o PROCESSED_WITH_WARNINGS — un draft FAILED nunca llega aquí (createDraft lo rechaza). */
  processingStatus: ProcessingStatus;
  processingNotes: string | null;
}

export const purchaseReceptionService = {
  importTxt(
    file: File,
    onProgress?: (percent: number) => void,
  ): Promise<PurchaseReceptionImportResult> {
    const formData = new FormData();
    formData.append("file", file);

    return apiPost<PurchaseReceptionImportResult>(`${BASE}/import`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
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

  /** Estado actual de una única línea, por su Id — usado por /purchases al reabrir una compra ya guardada. */
  getLineMatch(lineId: string): Promise<PurchaseReceptionLineMatch> {
    return apiGet<PurchaseReceptionLineMatch>(`${BASE}/lines/${lineId}`);
  },

  matchItem(
    lineId: string,
    itemId: string,
  ): Promise<PurchaseReceptionLineMatch> {
    return apiPost<PurchaseReceptionLineMatch>(
      `${BASE}/lines/${lineId}/match-item`,
      { itemId },
    );
  },

  unmatchItem(lineId: string): Promise<PurchaseReceptionLineMatch> {
    return apiPost<PurchaseReceptionLineMatch>(
      `${BASE}/lines/${lineId}/unmatch-item`,
      {},
    );
  },

  bulkMatch(
    matches: BulkMatchEntry[],
  ): Promise<{ results: BulkMatchResultEntry[] }> {
    return apiPost<{ results: BulkMatchResultEntry[] }>(
      `${BASE}/matching/bulk`,
      matches,
    );
  },
};
