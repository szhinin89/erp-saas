import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import {
  receptionPurchaseView,
  type ReceptionPurchaseBadgeVariant,
} from "./receptionPurchaseState";

/**
 * FASE 5 RECEPTION-UX — "Registro ERP" de un comprobante recibido: qué registro ACTIVO generó en el
 * ERP (compra, gasto o NC de compra) y el badge de la columna "Registro ERP". SSOT de la columna y
 * de las tarjetas de resumen; solo usa campos que ya devuelve el backend.
 */
export type ReceptionErpRecordKind = "purchase" | "expense" | "creditNote";

export interface ReceptionErpRecordView {
  /** Registro ERP activo; null = ninguno (incluye anulados/descartados, reprocesables). */
  active: ReceptionErpRecordKind | null;
  /** Sin registro activo pero con historial anulado/descartado (compra, gasto o NC). */
  history: boolean;
  badge: { key: string; fallback: string; variant: ReceptionPurchaseBadgeVariant };
}

type ErpRecordRow = Pick<
  PurchaseReceptionItem,
  | "sourceDocType"
  | "purchaseState"
  | "expenseExists"
  | "cancelledExpenseId"
  | "creditNoteExists"
  | "cancelledCreditNoteId"
>;

const NOT_PROCESSED: ReceptionErpRecordView["badge"] = {
  key: "purchases.reception.erpRecord.notProcessed",
  fallback: "Sin procesar",
  variant: "warning",
};

export function receptionErpRecordView(row: ErpRecordRow): ReceptionErpRecordView {
  if (row.sourceDocType === "CREDIT_NOTE") {
    if (row.creditNoteExists) {
      return {
        active: "creditNote",
        history: false,
        badge: {
          key: "purchases.reception.erpRecord.creditNoteProcessed",
          fallback: "NC procesada",
          variant: "success",
        },
      };
    }
    if (row.cancelledCreditNoteId) {
      return {
        active: null,
        history: true,
        badge: {
          key: "purchases.reception.erpRecord.creditNoteCancelled",
          fallback: "NC anulada",
          variant: "error",
        },
      };
    }
    return { active: null, history: false, badge: NOT_PROCESSED };
  }

  const purchase = receptionPurchaseView(row.purchaseState);
  if (purchase.hasActivePurchase)
    return { active: "purchase", history: false, badge: purchase.badge };
  if (row.expenseExists) {
    return {
      active: "expense",
      history: false,
      badge: {
        key: "purchases.reception.erpRecord.expenseRegistered",
        fallback: "Gasto registrado",
        variant: "success",
      },
    };
  }
  // Compra anulada / borrador descartado: historial visible, sigue siendo reprocesable.
  if (purchase.history) return { active: null, history: true, badge: purchase.badge };
  if (row.cancelledExpenseId) {
    return {
      active: null,
      history: true,
      badge: {
        key: "purchases.reception.erpRecord.expenseCancelled",
        fallback: "Gasto anulado",
        variant: "error",
      },
    };
  }
  return { active: null, history: false, badge: NOT_PROCESSED };
}

/**
 * PURCHASES-RECEPTION-FILTERS-01 — tarjeta de resumen a la que pertenece un comprobante. Partición
 * EXCLUSIVA (cada fila cae en una sola): es la misma regla que cuentan las tarjetas y que aplican al
 * pulsarlas, para que el filtro muestre exactamente las filas del contador.
 */
export type ReceptionSummaryBucket = "registered" | "toProcess" | "pendingSupplier";

export function receptionSummaryBucket(
  row: ErpRecordRow & Pick<PurchaseReceptionItem, "status">,
): ReceptionSummaryBucket {
  if (receptionErpRecordView(row).active !== null) return "registered";
  return row.status === "NEW_SUPPLIER" ? "pendingSupplier" : "toProcess";
}

export interface ReceptionSummaryCounts {
  /** Comprobantes con registro ERP activo (compra borrador/confirmada, gasto o NC). */
  registered: number;
  purchases: number;
  expenses: number;
  creditNotes: number;
  /** Proveedor disponible y sin registro ERP activo (incluye anulados/descartados). */
  toProcess: number;
  /** Comprobantes (no proveedores distintos) cuyo proveedor no existe todavía. */
  pendingSupplier: number;
}

export function summarizeReceptionItems(
  items: ReadonlyArray<ErpRecordRow & Pick<PurchaseReceptionItem, "status">>,
): ReceptionSummaryCounts {
  const counts: ReceptionSummaryCounts = {
    registered: 0,
    purchases: 0,
    expenses: 0,
    creditNotes: 0,
    toProcess: 0,
    pendingSupplier: 0,
  };
  for (const row of items) {
    const { active } = receptionErpRecordView(row);
    if (active === "purchase") counts.purchases++;
    else if (active === "expense") counts.expenses++;
    else if (active === "creditNote") counts.creditNotes++;
    else if (receptionSummaryBucket(row) === "pendingSupplier") counts.pendingSupplier++;
    else counts.toProcess++;
  }
  counts.registered = counts.purchases + counts.expenses + counts.creditNotes;
  return counts;
}
