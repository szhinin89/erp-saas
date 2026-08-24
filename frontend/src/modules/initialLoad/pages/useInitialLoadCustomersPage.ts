import { useCallback, useEffect, useRef, useState } from "react";
import { initialLoadService } from "../api/initialLoadService";
import { downloadBlob } from "../../ride/utils/downloadBlob";
import { formatApiRequestError } from "../../lib/apiError";
import type {
  ImportBatchDto,
  ImportBatchRowPreviewDto,
  PagedResult,
} from "../types/importBatch.types";

type WizardStep =
  | "idle"
  | "uploading"
  | "validating"
  | "validated"
  | "confirming"
  | "done";

const POLL_INTERVAL_MS = 2000;
const PREVIEW_PAGE_SIZE = 25;

export function useInitialLoadCustomersPage() {
  const [batch, setBatch] = useState<ImportBatchDto | null>(null);
  const [step, setStep] = useState<WizardStep>("idle");
  const [uploadProgress, setUploadProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);

  const [preview, setPreview] = useState<PagedResult<ImportBatchRowPreviewDto> | null>(null);
  const [previewPage, setPreviewPage] = useState(1);
  const [severityFilter, setSeverityFilter] = useState<"all" | "errors" | "warnings">("all");
  const [previewLoading, setPreviewLoading] = useState(false);

  const [confirmModalOpen, setConfirmModalOpen] = useState(false);
  const [confirmResult, setConfirmResult] = useState<{
    importedRows: number;
    failedRows: number;
  } | null>(null);

  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const stopPolling = useCallback(() => {
    if (pollRef.current) {
      clearInterval(pollRef.current);
      pollRef.current = null;
    }
  }, []);

  useEffect(() => stopPolling, [stopPolling]);

  const pollUntilSettled = useCallback(
    (batchId: string, onSettled: (b: ImportBatchDto) => void) => {
      stopPolling();
      pollRef.current = setInterval(async () => {
        try {
          const b = await initialLoadService.getStatus(batchId);
          setBatch(b);
          if (b.status !== "Validating" && b.status !== "Confirming") {
            stopPolling();
            onSettled(b);
          }
        } catch (err) {
          stopPolling();
          setError(formatApiRequestError(err, { generic: "No se pudo completar la operación." }));
        }
      }, POLL_INTERVAL_MS);
    },
    [stopPolling],
  );

  const downloadTemplate = useCallback(async () => {
    try {
      const blob = await initialLoadService.downloadTemplate("Customers");
      downloadBlob(blob, "plantilla-clientes.xlsx");
    } catch (err) {
      setError(formatApiRequestError(err, { generic: "No se pudo completar la operación." }));
    }
  }, []);

  const handleFileSelected = useCallback(async (file: File) => {
    setError(null);
    setStep("uploading");
    setUploadProgress(0);
    try {
      const created = await initialLoadService.createBatch("Customers");
      const uploaded = await initialLoadService.uploadFile(created.id, file, setUploadProgress);
      setBatch(uploaded);
      setStep("validating");

      const validated = await initialLoadService.validateBatch(uploaded.id);
      setBatch(validated);
      if (validated.status === "Validating") {
        pollUntilSettled(uploaded.id, (b) => setStep(b.status === "Failed" ? "idle" : "validated"));
      } else {
        setStep(validated.status === "Failed" ? "idle" : "validated");
      }
    } catch (err) {
      setStep("idle");
      setError(formatApiRequestError(err, { generic: "No se pudo completar la operación." }));
    }
  }, [pollUntilSettled]);

  const loadPreview = useCallback(
    async (page: number, filter: "all" | "errors" | "warnings") => {
      if (!batch) return;
      setPreviewLoading(true);
      try {
        const onlyWithBlockingIssue =
          filter === "errors" ? true : filter === "warnings" ? false : undefined;
        const result = await initialLoadService.preview(
          batch.id,
          page,
          PREVIEW_PAGE_SIZE,
          onlyWithBlockingIssue,
        );
        setPreview(result);
        setPreviewPage(page);
      } catch (err) {
        setError(formatApiRequestError(err, { generic: "No se pudo completar la operación." }));
      } finally {
        setPreviewLoading(false);
      }
    },
    [batch],
  );

  useEffect(() => {
    if (step === "validated" && batch) {
      void loadPreview(1, severityFilter);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [step, batch?.id]);

  const changeSeverityFilter = useCallback(
    (filter: "all" | "errors" | "warnings") => {
      setSeverityFilter(filter);
      void loadPreview(1, filter);
    },
    [loadPreview],
  );

  const confirmBatch = useCallback(async () => {
    if (!batch) return;
    setConfirmModalOpen(false);
    setError(null);
    setStep("confirming");
    try {
      const result = await initialLoadService.confirmBatch(batch.id);
      if (result.status === "Confirming") {
        pollUntilSettled(batch.id, (b) => {
          setConfirmResult({ importedRows: b.importedRows, failedRows: b.issueRows });
          setStep("done");
        });
      } else {
        setConfirmResult({
          importedRows: result.importedRows,
          failedRows: result.failedRows,
        });
        const b = await initialLoadService.getStatus(batch.id);
        setBatch(b);
        setStep("done");
      }
    } catch (err) {
      setStep("validated");
      setError(formatApiRequestError(err, { generic: "No se pudo completar la operación." }));
    }
  }, [batch, pollUntilSettled]);

  const reset = useCallback(() => {
    stopPolling();
    setBatch(null);
    setStep("idle");
    setUploadProgress(0);
    setError(null);
    setPreview(null);
    setPreviewPage(1);
    setSeverityFilter("all");
    setConfirmResult(null);
  }, [stopPolling]);

  return {
    batch,
    step,
    uploadProgress,
    error,
    preview,
    previewPage,
    previewLoading,
    severityFilter,
    confirmModalOpen,
    confirmResult,
    downloadTemplate,
    handleFileSelected,
    loadPreview,
    changeSeverityFilter,
    setConfirmModalOpen,
    confirmBatch,
    reset,
  };
}
