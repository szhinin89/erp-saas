import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import {
  receptionSummaryBucket,
  summarizeReceptionItems,
  type ReceptionSummaryBucket,
  type ReceptionSummaryCounts,
} from "./receptionErpRecord";

/**
 * PURCHASES-RECEPTION-FILTERS-01 (B2) — normalización del lote importado por `documentId`.
 *
 * Una clave de acceso repetida en el mismo TXT produce dos filas con el mismo `documentId` (el
 * backend reutiliza el documento; ver `Handle_deduplicates_repeated_access_key_within_the_same_file`).
 * - Filas IDÉNTICAS del mismo documento: se conserva una sola (eran el mismo comprobante).
 * - Mismo `documentId` con datos DISTINTOS: no se decide cuál es la buena — se conservan todas, sin
 *   fusionar, y se informan en `conflictingDocumentIds` para revisión.
 * - `documentId` ausente o vacío: nunca se agrupan entre sí (podrían ser documentos distintos); se
 *   conservan y se informan en `invalidIdCount`.
 * No modifica nada persistido: solo la vista en memoria de la importación.
 */
export interface ReceptionNormalization {
  items: PurchaseReceptionItem[];
  duplicatesRemoved: number;
  conflictingDocumentIds: string[];
  invalidIdCount: number;
}

export function hasValidReceptionDocumentId(row: Pick<PurchaseReceptionItem, "documentId">): boolean {
  return typeof row.documentId === "string" && row.documentId.trim() !== "";
}

/** Firma estable de la fila (claves ordenadas) para comparar duplicados exactos. */
function rowSignature(row: PurchaseReceptionItem): string {
  const record = row as unknown as Record<string, unknown>;
  return JSON.stringify(
    Object.keys(record)
      .sort((a, b) => a.localeCompare(b))
      .map((key) => [key, record[key] ?? null]),
  );
}

export function normalizeReceptionItems(
  items: readonly PurchaseReceptionItem[],
): ReceptionNormalization {
  const signaturesById = new Map<string, string[]>();
  const conflicting = new Set<string>();
  const out: PurchaseReceptionItem[] = [];
  let duplicatesRemoved = 0;
  let invalidIdCount = 0;

  for (const row of items) {
    if (!hasValidReceptionDocumentId(row)) {
      invalidIdCount++;
      out.push(row);
      continue;
    }
    const signature = rowSignature(row);
    const known = signaturesById.get(row.documentId);
    if (!known) {
      signaturesById.set(row.documentId, [signature]);
      out.push(row);
    } else if (known.includes(signature)) {
      duplicatesRemoved++;
    } else {
      known.push(signature);
      conflicting.add(row.documentId);
      out.push(row);
    }
  }

  return {
    items: out,
    duplicatesRemoved,
    conflictingDocumentIds: [...conflicting],
    invalidIdCount,
  };
}

/**
 * Clave estable de fila para la tabla: el `documentId` en la primera aparición; `id#n` si el mismo
 * documento quedó repetido por conflicto; `sin-id#posición` si no tiene identificador válido.
 */
export function buildReceptionRowKeys(
  items: readonly PurchaseReceptionItem[],
): Map<PurchaseReceptionItem, string> {
  const keys = new Map<PurchaseReceptionItem, string>();
  const occurrences = new Map<string, number>();
  items.forEach((row, index) => {
    if (!hasValidReceptionDocumentId(row)) {
      keys.set(row, `sin-id#${index}`);
      return;
    }
    const seen = occurrences.get(row.documentId) ?? 0;
    occurrences.set(row.documentId, seen + 1);
    keys.set(row, seen === 0 ? row.documentId : `${row.documentId}#${seen + 1}`);
  });
  return keys;
}

/** Situación de tarjeta resuelta por comprobante; "review" = documento repetido con datos distintos. */
export type ReceptionDocumentBucket = ReceptionSummaryBucket | "review";

/**
 * PURCHASES-RECEPTION-FILTERS-01 — situación de cada fila resuelta POR COMPROBANTE. Un documentId
 * con filas de datos distintos queda "En revisión": no se elige ninguna fila como correcta y queda
 * fuera de las tarjetas hasta resolver la contradicción. Los repetidos idénticos no cuentan como
 * contradicción. Filas sin identificador se resuelven solas.
 */
export function resolveReceptionDocumentBuckets(
  items: readonly PurchaseReceptionItem[],
): (row: PurchaseReceptionItem) => ReceptionDocumentBucket {
  const keysById = new Map<string, Set<string>>();
  for (const row of items) {
    if (!hasValidReceptionDocumentId(row)) continue;
    const keys = keysById.get(row.documentId) ?? new Set<string>();
    keys.add(rowSignature(row));
    keysById.set(row.documentId, keys);
  }
  return (row) =>
    hasValidReceptionDocumentId(row) && (keysById.get(row.documentId)?.size ?? 0) > 1
      ? "review"
      : receptionSummaryBucket(row);
}

/** Comprobantes únicos: un documentId válido cuenta una vez; cada fila sin identificador, una. */
export function countReceptionDocuments(rows: readonly PurchaseReceptionItem[]): number {
  const ids = new Set<string>();
  let withoutId = 0;
  for (const row of rows) {
    if (hasValidReceptionDocumentId(row)) ids.add(row.documentId);
    else withoutId++;
  }
  return ids.size + withoutId;
}

export interface ReceptionDocumentSummary extends ReceptionSummaryCounts {
  /** Comprobantes únicos "En revisión" (repetidos con datos distintos): fuera de las tarjetas. */
  inReview: number;
  /** Comprobantes únicos del lote (= tarjetas + inReview). */
  documents: number;
}

/** Tarjetas por comprobante único (nunca por fila repetida), sobre la misma partición oficial. */
export function summarizeReceptionDocuments(
  items: readonly PurchaseReceptionItem[],
): ReceptionDocumentSummary {
  const bucketOf = resolveReceptionDocumentBuckets(items);
  const seen = new Set<string>();
  const representatives: PurchaseReceptionItem[] = [];
  let inReview = 0;
  for (const row of items) {
    if (hasValidReceptionDocumentId(row)) {
      if (seen.has(row.documentId)) continue;
      seen.add(row.documentId);
    }
    if (bucketOf(row) === "review") inReview++;
    else representatives.push(row);
  }
  return {
    ...summarizeReceptionItems(representatives),
    inReview,
    documents: representatives.length + inReview,
  };
}
