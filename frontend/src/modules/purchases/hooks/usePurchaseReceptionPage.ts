import { useMemo, useState } from "react";
import {
  purchaseReceptionService,
  type PurchaseReceptionImportResult,
  type PurchaseReceptionItem,
  type PurchaseReceptionXmlView,
} from "../api/purchaseReceptionService";
import { message } from "../../../lib/messages";

const PAGE_SIZE = 20;

function extractErrorMessage(err: unknown, fallback: string): string {
  const response = (
    err as { response?: { data?: { message?: { user?: string } } } }
  )?.response;
  return response?.data?.message?.user ?? fallback;
}

export function usePurchaseReceptionPage() {
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<PurchaseReceptionImportResult | null>(
    null,
  );
  const [fileName, setFileName] = useState<string | null>(null);
  const [page, setPage] = useState(1);
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

  const handleFileSelected = async (file: File) => {
    setUploading(true);
    setProgress(0);
    setError(null);
    setPage(1);
    setXmlRowState({});
    try {
      const importResult = await purchaseReceptionService.importTxt(
        file,
        setProgress,
      );
      // El TXT del SRI nunca trae nombreComercial — solo llega al consultar el XML (ver
      // handleDownloadXml). Se normaliza a null aquí para que el tipo del item sea siempre
      // consistente, en vez de dejarlo `undefined` hasta esa consulta.
      setResult({
        ...importResult,
        items: importResult.items.map((item) => ({
          ...item,
          supplierExists: item.sourceDocType === "INVOICE" ? item.supplierExists && !!item.supplierId : item.supplierExists,
          supplierTradeName: item.supplierTradeName ?? null,
          supplierIsActive: item.supplierIsActive ?? null,
        })),
      });
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
    } finally {
      setUploading(false);
    }
  };

  const handleDownloadXml = async (documentId: string) => {
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
                      purchaseExists: download.purchaseExists,
                      purchaseId: download.purchaseId,
                      supplierIsActive: download.supplierIsActive,
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

  const items = useMemo(() => result?.items ?? [], [result]);
  const pagedItems = useMemo(
    () => items.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [items, page],
  );

  const summary = useMemo(
    () => ({
      imported: items.filter((i) => i.status === "IMPORTED").length,
      pending: items.filter((i) => i.status === "PENDING").length,
      newSupplier: items.filter((i) => i.status === "NEW_SUPPLIER").length,
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
    items: pagedItems,
    total: items.length,
    page,
    pageSize: PAGE_SIZE,
    setPage,
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
                      status: item.purchaseExists ? item.status : "PENDING",
                    }
                  : item,
              ),
            },
      );
      setNewSupplierRow(null);
    },
  };
}
