import type {
  BatchDownloadXmlItemResult,
  BatchDownloadXmlResult,
} from "../api/purchaseReceptionService";
import { chunkIds } from "./receptionRefresh";

/**
 * PURCHASES-RECEPTION-FILTERS-01 (B1) — descarga de XML pendientes en lotes secuenciales. El
 * backend rechaza más de 50 documentos por solicitud (`BatchDownloadPurchaseReceptionXmlValidator`);
 * se envían de a 50, uno tras otro (nunca solicitudes simultáneas), y se agregan los resultados.
 * Si una solicitud falla, sus documentos cuentan como fallidos y NINGUNO se da por descargado.
 */
export const RECEPTION_XML_BATCH_SIZE = 50;

export type ReceptionXmlBatchTotals = Pick<
  BatchDownloadXmlResult,
  "total" | "processed" | "downloaded" | "skipped" | "failed"
>;

export interface ReceptionXmlBatchChunk {
  ids: string[];
  /** Resultados por documento confirmados por el backend; vacío si la solicitud falló. */
  items: BatchDownloadXmlItemResult[];
  /** Error de la solicitud completa; null si respondió. */
  error: unknown;
}

export interface ReceptionXmlBatchOutcome extends ReceptionXmlBatchTotals {
  /** Ids de las solicitudes que fallaron por completo (sin respuesta por documento). */
  failedRequestIds: string[];
  requestErrors: unknown[];
}

export const isResolvedXmlBatchItem = (item: BatchDownloadXmlItemResult) =>
  item.status === "Downloaded" || item.status === "SkippedAlreadyHasXml";

export async function runReceptionXmlBatches(
  ids: readonly string[],
  download: (chunk: string[]) => Promise<BatchDownloadXmlResult>,
  callbacks: {
    onChunkStart?: (ids: string[]) => void;
    onChunkDone?: (chunk: ReceptionXmlBatchChunk, totals: ReceptionXmlBatchTotals) => void;
  } = {},
): Promise<ReceptionXmlBatchOutcome> {
  const outcome: ReceptionXmlBatchOutcome = {
    total: ids.length,
    processed: 0,
    downloaded: 0,
    skipped: 0,
    failed: 0,
    failedRequestIds: [],
    requestErrors: [],
  };

  for (const chunk of chunkIds(ids, RECEPTION_XML_BATCH_SIZE)) {
    callbacks.onChunkStart?.(chunk);
    let done: ReceptionXmlBatchChunk;
    try {
      const response = await download(chunk);
      outcome.processed += response.processed;
      outcome.downloaded += response.downloaded;
      outcome.skipped += response.skipped;
      outcome.failed += response.failed;
      done = { ids: chunk, items: response.items, error: null };
    } catch (error) {
      outcome.processed += chunk.length;
      outcome.failed += chunk.length;
      outcome.failedRequestIds.push(...chunk);
      outcome.requestErrors.push(error);
      done = { ids: chunk, items: [], error };
    }
    callbacks.onChunkDone?.(done, {
      total: outcome.total,
      processed: outcome.processed,
      downloaded: outcome.downloaded,
      skipped: outcome.skipped,
      failed: outcome.failed,
    });
  }

  return outcome;
}

/** Mismo criterio de estado final que la descarga en una sola solicitud. */
export function receptionXmlBatchStatus(
  totals: ReceptionXmlBatchTotals,
): "success" | "warning" | "error" {
  if (totals.failed === 0) return "success";
  return totals.downloaded > 0 ? "warning" : "error";
}
