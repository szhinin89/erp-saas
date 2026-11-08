import { useMemo, useRef, useState } from "react";
import {
  purchaseReceptionService,
  type PurchaseReceptionImportResult,
  type PurchaseReceptionItem,
  type PurchaseReceptionXmlView,
} from "../api/purchaseReceptionService";
import { message } from "../../../lib/messages";
import { chunkIds, mergeRefreshedReceptionItems } from "../utils/receptionRefresh";
import {
  buildReceptionRowKeys,
  hasValidReceptionDocumentId,
  normalizeReceptionItems,
  summarizeReceptionDocuments,
  type ReceptionNormalization,
} from "../utils/receptionNormalize";
import {
  isResolvedXmlBatchItem,
  receptionXmlBatchStatus,
  runReceptionXmlBatches,
} from "../utils/receptionBatchXml";
import { useReceptionFilters } from "./useReceptionFilters";
import { useI18n } from "../../../i18n/i18n";
import type { ZhBatchProgressStatus } from "../../../components/zh/progress/ZhBatchProgress";

export interface BatchXmlProgressState {
  status: ZhBatchProgressStatus;
  total: number;
  processed: number;
  downloaded: number;
  skipped: number;
  failed: number;
}

export type { ReceptionDateSortOrder, ReceptionSupplierOption } from "./useReceptionFilters";

function extractErrorMessage(err: unknown, fallback: string): string {
  const response = (
    err as { response?: { data?: { message?: { user?: string } } } }
  )?.response;
  return response?.data?.message?.user ?? fallback;
}

export function usePurchaseReceptionPage() {
  const { t } = useI18n();
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<PurchaseReceptionImportResult | null>(
    null,
  );
  const [fileName, setFileName] = useState<string | null>(null);
  // PURCHASES-RECEPTION-FILTERS-01 (B2) — resumen de la normalización por documentId del lote.
  const [normalization, setNormalization] = useState<Omit<ReceptionNormalization, "items"> | null>(
    null,
  );
  // Estado por fila de la consulta de XML — no viene del backend (que solo persiste el estado
  // previo cuando falla), es puramente de UI para mostrar "Consultando..."/"Error consulta".
  const [xmlRowState, setXmlRowState] = useState<
    Record<string, "loading" | "error">
  >({});
  // Modal "Crear proveedor" — fila activa (RUC/nombre/nombre comercial del documento) o null si
  // está cerrado.
  const [newSupplierRow, setNewSupplierRow] = useState<{
    ruc: string;
    name: string;
    tradeName: string | null;
  } | null>(null);

  // Modal "Ver XML" (FLOW-READY-02E.1) — read-only, nunca dispara descarga/reprocesamiento.
  const [xmlViewOpen, setXmlViewOpen] = useState(false);
  const [xmlViewLoading, setXmlViewLoading] = useState(false);
  const [xmlViewError, setXmlViewError] = useState<string | null>(null);
  const [xmlViewData, setXmlViewData] = useState<PurchaseReceptionXmlView | null>(
    null,
  );

  // Lote normalizado completo: fuente de la tabla (vía filtros) y de los KPI (sin filtros).
  const items = useMemo(() => result?.items ?? [], [result]);
  const rowKeys = useMemo(() => buildReceptionRowKeys(items), [items]);
  // ZH-PURCHASES-RECEPTION-FILTER-SORT-01 / PURCHASES-RECEPTION-FILTERS-01 — filtros, orden y
  // paginación solo de UI sobre las filas en memoria (el TXT se importa completo).
  const filters = useReceptionFilters(items, xmlRowState);
  // B1 — bloquea consultas individuales mientras corre el lote (evita solicitudes en conflicto).
  const batchXmlRunningRef = useRef(false);

  const handleFileSelected = async (file: File) => {
    setUploading(true);
    setProgress(0);
    setError(null);
    setXmlRowState({});
    filters.resetForImport();
    try {
      const importResult = await purchaseReceptionService.importTxt(
        file,
        setProgress,
      );
      // El TXT del SRI nunca trae nombreComercial — solo llega al consultar el XML (ver
      // handleDownloadXml). Se normaliza a null aquí para que el tipo del item sea siempre
      // consistente, en vez de dejarlo `undefined` hasta esa consulta.
      const { items: normalizedItems, ...normalizationSummary } = normalizeReceptionItems(
        importResult.items.map((item) => ({
          ...item,
          supplierExists: item.sourceDocType === "INVOICE" ? item.supplierExists && !!item.supplierId : item.supplierExists,
          supplierTradeName: item.supplierTradeName ?? null,
          supplierIsActive: item.supplierIsActive ?? null,
        })),
      );
      setResult({ ...importResult, items: normalizedItems });
      setNormalization(normalizationSummary);
      setFileName(file.name);
      message.success(
        importResult.items.length > 0
          ? `Archivo TXT importado correctamente. Se cargaron ${importResult.items.length} líneas.`
          : "Archivo TXT importado correctamente.",
      );
    } catch (err) {
      setError(
        extractErrorMessage(
          err,
          "No se pudo importar el archivo. Intente nuevamente.",
        ),
      );
      setResult(null);
      setNormalization(null);
    } finally {
      setUploading(false);
    }
  };

  const handleDownloadXml = async (documentId: string) => {
    if (batchXmlRunningRef.current) return;
    setXmlRowState((prev) => ({ ...prev, [documentId]: "loading" }));
    try {
      const download = await purchaseReceptionService.downloadXml(documentId);
      setResult((prev) =>
        prev === null
          ? prev
          : {
              ...prev,
              items: prev.items.map((item) =>
                item.documentId === documentId
                  ? {
                      ...item,
                      documentStatus: download.status,
                      processingStatus: download.processingStatus,
                      processingNotes: download.processingNotes,
                      purchaseId: download.purchaseId,
                      // FASE 5 PURCHASE-UX — la descarga refresca el estado real de la compra.
                      purchaseState: download.purchaseState ?? item.purchaseState,
                      cancelledPurchaseId: download.cancelledPurchaseId ?? null,
                      // H4 — la descarga (handler congelado) solo evalúa el socio; la fila ya trae
                      // socio + rol del verificador. Solo puede bajar a inactivo, nunca reactivar:
                      // reactivar lo decide "Actualizar estados" con la regla completa.
                      supplierIsActive:
                        download.supplierIsActive === false ? false : item.supplierIsActive,
                      expenseExists: download.expenseExists,
                      supplierTradeName:
                        download.supplierTradeName ?? item.supplierTradeName,
                    }
                  : item,
              ),
            },
      );
      setXmlRowState((prev) => {
        const next = { ...prev };
        delete next[documentId];
        return next;
      });
    } catch {
      setXmlRowState((prev) => ({ ...prev, [documentId]: "error" }));
    }
  };

  const openXmlView = async (documentId: string) => {
    setXmlViewOpen(true);
    setXmlViewLoading(true);
    setXmlViewError(null);
    setXmlViewData(null);
    try {
      const data = await purchaseReceptionService.getXmlView(documentId);
      setXmlViewData(data);
    } catch (err) {
      setXmlViewError(
        extractErrorMessage(err, "No se pudo cargar el XML del comprobante."),
      );
    } finally {
      setXmlViewLoading(false);
    }
  };

  const closeXmlView = () => {
    setXmlViewOpen(false);
    setXmlViewError(null);
    setXmlViewData(null);
  };

  // PURCHASE-CREDIT-NOTE-AFFECTED-INVOICE-RESOLVES-CANCELLED-01 — affectedPurchaseId en `result`
  // se calculó UNA sola vez, al importar el TXT; si la factura afectada se anula y se reprocesa
  // después (sin volver a importar el mismo archivo), ese valor queda obsoleto. xml-view sí se
  // puede consultar en cualquier momento y siempre resuelve la Confirmed vigente — se vuelve a
  // consultar aquí, justo antes de navegar, en vez de confiar en el valor ya cargado en la fila.
  const [resolvingCreditNoteId, setResolvingCreditNoteId] = useState<
    string | null
  >(null);

  // FASE 5 PURCHASE-UX — "Actualizar estados": refresca en el lugar las filas ya importadas.
  const [refreshingStatuses, setRefreshingStatuses] = useState(false);

  // PURCHASE-RECEPTION-BULK-SRI-XML-DOWNLOAD-01 — descarga en lote, un único request batch.
  const [batchXmlRunning, setBatchXmlRunning] = useState(false);
  const [batchXmlProgress, setBatchXmlProgress] =
    useState<BatchXmlProgressState | null>(null);

  /**
   * FASE 5 PURCHASE-UX — recalcula los estados de TODAS las filas importadas (no solo la página
   * visible), en lotes de 200, sin reimportar el TXT ni descargar XML. La fusión es por documentId:
   * filtros, orden, página y estados locales (XML por fila, nombre comercial) no cambian. Si falla,
   * las filas quedan exactamente como estaban.
   */
  const handleRefreshStatuses = async () => {
    const ids = (result?.items ?? []).map((item) => item.documentId);
    if (ids.length === 0 || refreshingStatuses || batchXmlRunning || uploading) return;
    setRefreshingStatuses(true);
    try {
      const refreshed: PurchaseReceptionItem[] = [];
      for (const chunk of chunkIds(ids))
        refreshed.push(...(await purchaseReceptionService.refreshStatus(chunk)));
      setResult((prev) =>
        prev ? { ...prev, items: mergeRefreshedReceptionItems(prev.items, refreshed) } : prev,
      );
      message.success(
        t("purchases.reception.messages.statusesRefreshed", { count: refreshed.length }),
      );
    } catch {
      message.error(
        t(
          "purchases.reception.errors.refreshStatusesFailed",
          "No se pudieron actualizar los estados. Intente nuevamente.",
        ),
      );
    } finally {
      setRefreshingStatuses(false);
    }
  };

  /**
   * PURCHASE-RECEPTION-BULK-SRI-XML-DOWNLOAD-01 / PURCHASES-RECEPTION-FILTERS-01 (B1) — descarga los
   * XML pendientes en lotes secuenciales de 50 (máximo del backend). Cada lote marca sus filas como
   * "Consultando" y, al responder, aplica el resumen por documento; un lote fallido deja sus filas en
   * error sin dar ninguna por descargada.
   */
  const handleDownloadPendingXml = async () => {
    if (batchXmlRunningRef.current || refreshingStatuses || uploading) return;
    const pendingIds = [
      ...new Set(
        items
          .filter(
            (item) =>
              item.documentStatus === "IMPORTED" &&
              hasValidReceptionDocumentId(item) &&
              xmlRowState[item.documentId] !== "loading",
          )
          .map((item) => item.documentId),
      ),
    ];

    if (pendingIds.length === 0) {
      message.info(
        t(
          "purchases.reception.messages.noPendingXml",
          "No hay XML pendientes por descargar.",
        ),
      );
      return;
    }

    batchXmlRunningRef.current = true;
    setBatchXmlRunning(true);
    setBatchXmlProgress({
      status: "running",
      total: pendingIds.length,
      processed: 0,
      downloaded: 0,
      skipped: 0,
      failed: 0,
    });

    try {
      const outcome = await runReceptionXmlBatches(
        pendingIds,
        (chunk) => purchaseReceptionService.downloadXmlPending(chunk),
        {
          onChunkStart: (chunk) =>
            setXmlRowState((prev) => {
              const next = { ...prev };
              for (const id of chunk) next[id] = "loading";
              return next;
            }),
          onChunkDone: (chunk, totals) => {
            const itemsById = new Map(chunk.items.map((item) => [item.documentId, item] as const));
            // Refresca los badges (documento/proceso/compra/gasto/NC) de cada fila con el resumen
            // mínimo que ya trae el lote — sin abrir "Ver XML" fila por fila.
            setResult((prev) => {
              if (prev === null || itemsById.size === 0) return prev;
              return {
                ...prev,
                items: prev.items.map((item) => {
                  const batchItem = itemsById.get(item.documentId);
                  if (batchItem?.documentStatus == null) return item;
                  return {
                    ...item,
                    documentStatus: batchItem.documentStatus,
                    processingStatus: batchItem.processingStatus ?? item.processingStatus,
                    purchaseId: batchItem.purchaseId,
                    purchaseState: batchItem.purchaseState ?? item.purchaseState,
                    cancelledPurchaseId: batchItem.cancelledPurchaseId ?? null,
                    expenseExists: batchItem.expenseExists,
                    creditNoteExists: batchItem.creditNoteExists,
                    creditNoteId: batchItem.creditNoteId,
                    cancelledCreditNoteId: batchItem.cancelledCreditNoteId,
                  };
                }),
              };
            });
            // Solo se resuelve lo que el backend confirmó; todo lo demás del lote queda en error
            // de esta sesión (incluida una solicitud fallida por completo).
            setXmlRowState((prev) => {
              const next = { ...prev };
              for (const id of chunk.ids) {
                const confirmed = itemsById.get(id);
                if (confirmed && isResolvedXmlBatchItem(confirmed)) delete next[id];
                else next[id] = "error";
              }
              return next;
            });
            setBatchXmlProgress({ status: "running", ...totals });
          },
        },
      );

      const status = receptionXmlBatchStatus(outcome);
      setBatchXmlProgress({
        status,
        total: outcome.total,
        processed: outcome.processed,
        downloaded: outcome.downloaded,
        skipped: outcome.skipped,
        failed: outcome.failed,
      });

      if (outcome.failedRequestIds.length === outcome.total) {
        message.error(
          extractErrorMessage(
            outcome.requestErrors[0],
            "No se pudo completar la descarga de XML pendientes.",
          ),
        );
      } else if (status === "success") {
        message.success(
          t("purchases.reception.batchXml.summarySuccess", {
            downloaded: outcome.downloaded,
            skipped: outcome.skipped,
          }),
        );
      } else if (status === "warning") {
        message.warning(
          t("purchases.reception.batchXml.summaryWarning", {
            downloaded: outcome.downloaded,
            skipped: outcome.skipped,
            failed: outcome.failed,
          }),
        );
      } else {
        message.error(
          t("purchases.reception.batchXml.summaryError", {
            failed: outcome.failed,
            total: outcome.total,
          }),
        );
      }
    } finally {
      batchXmlRunningRef.current = false;
      setBatchXmlRunning(false);
    }
  };

  const processCreditNote = async (row: PurchaseReceptionItem) => {
    setResolvingCreditNoteId(row.documentId);
    try {
      const fresh = await purchaseReceptionService.getXmlView(row.documentId);
      if (!fresh.affectedPurchaseExists || !fresh.affectedPurchaseId) {
        message.error(
          "La factura afectada ya no está disponible (anulada o no encontrada). Actualice la recepción e intente nuevamente.",
        );
        return;
      }
      window.open(
        `/purchases/credit-notes/new?invoiceId=${fresh.affectedPurchaseId}&receptionDocumentId=${row.documentId}`,
        "_blank",
        "noopener,noreferrer",
      );
    } catch (err) {
      setError(
        extractErrorMessage(
          err,
          "No se pudo validar la factura afectada. Intente nuevamente.",
        ),
      );
    } finally {
      setResolvingCreditNoteId(null);
    }
  };

  const summary = useMemo(
    () => ({
      // FASE 5 RECEPTION-UX — Compra/Gasto/NC por registro ERP activo, por comprobante único
      // (PURCHASES-RECEPTION-FILTERS-01: un documentId repetido nunca cuenta dos veces).
      ...summarizeReceptionDocuments(items),
      skipped: result?.skippedUnsupportedCount ?? 0,
    }),
    [items, result],
  );

  return {
    uploading,
    progress,
    error,
    result,
    fileName,
    ...filters,
    rowKeys,
    normalization,
    summary,
    handleFileSelected,
    xmlRowState,
    handleDownloadXml,
    newSupplierRow,
    openCreateSupplier: (ruc: string, name: string, tradeName: string | null) =>
      setNewSupplierRow({ ruc, name, tradeName }),
    closeCreateSupplier: () => setNewSupplierRow(null),
    xmlViewOpen,
    xmlViewLoading,
    xmlViewError,
    xmlViewData,
    openXmlView: (documentId: string) => void openXmlView(documentId),
    closeXmlView,
    resolvingCreditNoteId,
    processCreditNote,
    batchXmlRunning,
    batchXmlProgress,
    refreshingStatuses,
    handleRefreshStatuses,
    handleDownloadPendingXml,
    // El TXT SRI no expone un endpoint de "reverificar proveedor" — tras crearlo, marcamos
    // localmente las filas con ese RUC como existentes (mismo criterio que el backend: proveedor
    // existe + compra no existe todavía => PENDING).
    handleSupplierCreated: (ruc: string, supplierId: string) => {
      setResult((prev) =>
        prev === null
          ? prev
          : {
              ...prev,
              items: prev.items.map((item) =>
                item.supplierRuc === ruc
                  ? {
                      ...item,
                      supplierId: item.sourceDocType === "INVOICE" ? supplierId || null : item.supplierId,
                      supplierExists: item.sourceDocType === "INVOICE" ? !!supplierId : true,
                      supplierIsActive: true,
                      status: item.purchaseState === "DRAFT" || item.purchaseState === "CONFIRMED" ? item.status : "PENDING",
                    }
                  : item,
              ),
            },
      );
      setNewSupplierRow(null);
    },
  };
}
