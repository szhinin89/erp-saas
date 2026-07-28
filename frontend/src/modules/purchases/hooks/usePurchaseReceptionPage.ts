import { useMemo, useState } from 'react';
import { purchaseReceptionService, type PurchaseReceptionImportResult } from '../api/purchaseReceptionService';

const PAGE_SIZE = 20;

function extractErrorMessage(err: unknown, fallback: string): string {
  const response = (err as { response?: { data?: { message?: { user?: string } } } })?.response;
  return response?.data?.message?.user ?? fallback;
}

export function usePurchaseReceptionPage() {
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<PurchaseReceptionImportResult | null>(null);
  const [fileName, setFileName] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  // Estado por fila de la consulta de XML — no viene del backend (que solo persiste el estado
  // previo cuando falla), es puramente de UI para mostrar "Consultando..."/"Error consulta".
  const [xmlRowState, setXmlRowState] = useState<Record<string, 'loading' | 'error'>>({});
  // Panel de Item Matching (Vincular productos) — documento activo o null si está cerrado.
  const [matchingDocumentId, setMatchingDocumentId] = useState<string | null>(null);
  const [matchingSupplierName, setMatchingSupplierName] = useState<string>('');

  const handleFileSelected = async (file: File) => {
    setUploading(true);
    setProgress(0);
    setError(null);
    setPage(1);
    setXmlRowState({});
    try {
      const importResult = await purchaseReceptionService.importTxt(file, setProgress);
      setResult(importResult);
      setFileName(file.name);
    } catch (err) {
      setError(extractErrorMessage(err, 'No se pudo importar el archivo. Intente nuevamente.'));
      setResult(null);
    } finally {
      setUploading(false);
    }
  };

  const handleDownloadXml = async (documentId: string) => {
    setXmlRowState((prev) => ({ ...prev, [documentId]: 'loading' }));
    try {
      const download = await purchaseReceptionService.downloadXml(documentId);
      setResult((prev) => prev === null ? prev : {
        ...prev,
        items: prev.items.map((item) => item.documentId === documentId
          ? { ...item, documentStatus: download.status }
          : item),
      });
      setXmlRowState((prev) => {
        const next = { ...prev };
        delete next[documentId];
        return next;
      });
    } catch {
      setXmlRowState((prev) => ({ ...prev, [documentId]: 'error' }));
    }
  };

  const items = useMemo(() => result?.items ?? [], [result]);
  const pagedItems = useMemo(
    () => items.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [items, page],
  );

  const summary = useMemo(() => ({
    imported: items.filter((i) => i.status === 'IMPORTED').length,
    pending: items.filter((i) => i.status === 'PENDING').length,
    newSupplier: items.filter((i) => i.status === 'NEW_SUPPLIER').length,
    skipped: result?.skippedUnsupportedCount ?? 0,
  }), [items, result]);

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
    matchingDocumentId,
    matchingSupplierName,
    openMatchingPanel: (documentId: string, supplierName: string) => {
      setMatchingDocumentId(documentId);
      setMatchingSupplierName(supplierName);
    },
    closeMatchingPanel: () => setMatchingDocumentId(null),
  };
}
