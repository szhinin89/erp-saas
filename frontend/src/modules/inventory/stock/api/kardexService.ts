import { apiGet } from "../../../lib/apiEnvelope";
import type { StockMovementDto } from "./stockService";

/** Coincide 1:1 con StockMovement.SourceDocType en el backend — nunca inventar valores nuevos aquí. */
export type KardexSourceDocType =
  "PurchaseInvoice" | "SalesInvoice" | "StockAdjustment" | "StockTransfer";

export type KardexSourceDocumentDto = {
  docType: string;
  docNumber: string | null;
  partnerName: string | null;
  unitPrice: number | null;
  discountPct: number | null;
  vatCode: string | null;
  vatRate: number | null;
  reason: string | null;
  notes: string | null;
};

export type KardexActorDto = {
  userId: string;
  userName: string;
};

export type KardexDocumentChainLinkDto = {
  relationType: string;
  docType: string;
  docNumber: string | null;
  docId: string | null;
  isCurrent: boolean;
};

export type KardexDocumentChainDto = {
  links: KardexDocumentChainLinkDto[];
};

export type KardexRelatedMovementRef = {
  movementId: string;
  sequenceNumber: number;
  movementTypeName: string;
  effectiveDate: string;
};

export type KardexMovementRelationsDto = {
  current: KardexRelatedMovementRef;
  previous: KardexRelatedMovementRef | null;
  next: KardexRelatedMovementRef | null;
};

export type KardexMovementDetailDto = {
  movement: StockMovementDto;
  sourceDocument: KardexSourceDocumentDto | null;
  actor: KardexActorDto;
  documentChain: KardexDocumentChainDto;
  relations: KardexMovementRelationsDto;
};

const BASE = "/api/v1/inventory/kardex";

export const kardexService = {
  getByProduct(
    productId: string,
    params?: { warehouseId?: string; from?: string; to?: string },
  ) {
    const qs = new URLSearchParams();
    if (params?.warehouseId) qs.set("warehouseId", params.warehouseId);
    if (params?.from) qs.set("from", params.from);
    if (params?.to) qs.set("to", params.to);
    const suffix = qs.toString() ? `?${qs.toString()}` : "";
    return apiGet<StockMovementDto[]>(`${BASE}/${productId}${suffix}`);
  },

  /** Búsqueda exacta por documento origen ya resuelto (Id + Tipo) — nunca por texto libre. */
  getByDocument(sourceDocId: string, sourceDocType: KardexSourceDocType) {
    const qs = new URLSearchParams({ sourceDocType });
    return apiGet<StockMovementDto[]>(
      `${BASE}/by-document/${sourceDocId}?${qs.toString()}`,
    );
  },

  getMovementDetail(movementId: string) {
    return apiGet<KardexMovementDetailDto>(`${BASE}/movement/${movementId}`);
  },
};
