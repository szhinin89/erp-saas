import { useCallback, useEffect, useRef, useState } from "react";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { salesService, type SalesInvoiceDto } from "../api/salesService";
import {
  buildReceiptPrintJobRequest,
  PrintAgentError,
  printAgentUserMessage,
  retryReceiptPrintJob,
  submitReceiptPrintJob,
  type PrintJobResponse,
} from "../api/printAgentClient";
import { operationalPreferencesService } from "../../configuracion/operaciones/api/operationalPreferencesService";
import {
  ISSUE_STEPS,
  type IssuePhase,
  type IssueErrorInfo,
} from "../hooks/useSalesPage";
import "../styles/sales-invoice.css";

interface SalesIssueModalProps {
  phase: IssuePhase;
  isElectronic: boolean;

  // Confirm
  customerName: string;
  lineCount: number;
  subtotal: number;
  discount: number;
  vat: number;
  total: number;

  // Processing
  stepIndex: number;

  // Success
  result: SalesInvoiceDto | null;
  ridePending: boolean;
  xmlDownloading: boolean;
  onPrintRide: () => void;
  onDownloadPdf: () => void;
  onDownloadXml: () => void;
  /** Pago en efectivo de esta venta — solo se muestran si hubo cobro en efectivo (cashDue > 0). */
  cashDue: number;
  cashReceived: number;
  cashChange: number;

  // Error (solo fallas de infraestructura — interno/comunicación)
  error: IssueErrorInfo | null;
  onRetry: () => void;

  // Shared
  onCancel: () => void;
  onConfirm: () => void;
  onNewSale: () => void;
}

function electronicStatusVariant(status: string): BadgeVariant {
  if (status === "Authorized") return "green";
  if (status === "Rejected" || status === "Failed") return "red";
  if (status === "None") return "gray";
  return "orange"; // Draft/Signed/Sent/Received — en trámite ante el SRI
}

const ELECTRONIC_STATUS_LABEL: Record<string, string> = {
  None: "Sin generar",
  Draft: "Borrador",
  Signed: "Firmado",
  Sent: "Enviado",
  Received: "Recibido",
  Authorized: "Autorizado",
  Rejected: "Rechazado",
  Failed: "Fallido",
};

type ReceiptPrintState = "idle" | "printing" | "success" | "error";

export function SalesIssueModal({
  phase,
  isElectronic,
  customerName,
  lineCount,
  subtotal,
  discount,
  vat,
  total,
  stepIndex,
  result,
  ridePending,
  xmlDownloading,
  onPrintRide,
  onDownloadPdf,
  onDownloadXml,
  cashDue,
  cashReceived,
  cashChange,
  error,
  onRetry,
  onCancel,
  onConfirm,
  onNewSale,
}: SalesIssueModalProps) {
  // Durante la emisión no se puede cerrar el flujo (Escape, backdrop y la X
  // del header quedan neutralizados porque onClose ignora la llamada).
  const canClose = phase !== "processing";
  const confirmBtnRef = useRef<HTMLButtonElement>(null);
  const [receiptPrintState, setReceiptPrintState] =
    useState<ReceiptPrintState>("idle");
  const [receiptPrintMessage, setReceiptPrintMessage] = useState<string | null>(
    null,
  );
  const [receiptPrintDetail, setReceiptPrintDetail] = useState<string | null>(
    null,
  );
  const [lastFailedReceiptJobId, setLastFailedReceiptJobId] = useState<
    string | null
  >(null);
  // Preferencia operativa printing.salesReceipt.mode/copies (CONFIG-DYNAMIC-OPERATIONS-01).
  // Falla abierta al comportamiento actual (AskBeforePrint, 1 copia) si no se puede cargar —
  // la impresión de tirilla no debe bloquearse por un problema al leer esta preferencia.
  const [printingPrefs, setPrintingPrefs] = useState<{
    mode: "AskBeforePrint" | "AlwaysPrint" | "NeverAutoPrint";
    copies: number;
  }>({ mode: "AskBeforePrint", copies: 1 });

  useEffect(() => {
    let cancelled = false;
    operationalPreferencesService
      .getPreferences()
      .then((dto) => {
        if (cancelled) return;
        setPrintingPrefs({
          mode: dto.printing.salesReceiptMode,
          copies: dto.printing.salesReceiptCopies,
        });
      })
      .catch(() => {
        // Mantener el default (AskBeforePrint, 1 copia) — ver comentario arriba.
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // ZHModal enfoca automáticamente el primer elemento focusable del cuerpo/footer
  // en orden de documento — el cuerpo de 'confirm' no tiene campos, así que ese
  // primer elemento sería "Cancelar". Se sobrescribe a propósito para que Enter
  // confirme la emisión de inmediato (flujo rápido de cajero).
  useEffect(() => {
    if (phase === "confirm") confirmBtnRef.current?.focus();
  }, [phase]);

  useEffect(() => {
    setReceiptPrintState("idle");
    setReceiptPrintMessage(null);
    setReceiptPrintDetail(null);
    setLastFailedReceiptJobId(null);
  }, [result?.id]);

  // Memoizado: ZHModal reejecuta su efecto de foco/Escape cuando `onClose`
  // cambia de identidad (dependencia de su useEffect) — sin useCallback se
  // recreaba en cada render y robaba el foco repetidamente durante 'processing'.
  const handleClose = useCallback(() => {
    if (!canClose) return;
    if (phase === "success") onNewSale();
    else onCancel();
  }, [canClose, phase, onNewSale, onCancel]);

  const handlePrintReceipt = useCallback(async () => {
    if (!result) return;

    const deterministicJobId = `invoice-${result.id}-receipt`;
    setReceiptPrintState("printing");
    setReceiptPrintMessage("Imprimiendo...");
    setReceiptPrintDetail(null);

    try {
      const payload = await salesService.getReceiptPrintPayload(result.id);
      const request = buildReceiptPrintJobRequest(
        payload,
        undefined,
        printingPrefs.copies,
      );
      const response: PrintJobResponse =
        lastFailedReceiptJobId === request.jobId
          ? await retryReceiptPrintJob(request.jobId)
          : await submitReceiptPrintJob(request);

      setReceiptPrintState("success");
      setReceiptPrintMessage(
        response.duplicate
          ? "La tirilla ya estaba registrada en el agente de impresión."
          : "Tirilla enviada a impresión.",
      );
      setReceiptPrintDetail(null);
      setLastFailedReceiptJobId(null);
    } catch (err) {
      setReceiptPrintState("error");
      setReceiptPrintMessage(printAgentUserMessage(err));
      setReceiptPrintDetail(
        err instanceof PrintAgentError
          ? (err.detail ?? null)
          : "Error de impresión; puede reintentar.",
      );
      setLastFailedReceiptJobId(
        err instanceof PrintAgentError && err.kind === "job-failed"
          ? deterministicJobId
          : null,
      );
    }
  }, [lastFailedReceiptJobId, result, printingPrefs.copies]);

  // printing.salesReceipt.mode = AlwaysPrint: imprime automáticamente al llegar a "success",
  // sin esperar el clic manual — una sola vez por factura (guardado por receiptPrintState).
  useEffect(() => {
    if (
      phase === "success" &&
      result &&
      printingPrefs.mode === "AlwaysPrint" &&
      receiptPrintState === "idle"
    ) {
      void handlePrintReceipt();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [phase, result, printingPrefs.mode, receiptPrintState]);

  if (phase === "idle") return null;
  const dc = getDecimalConfig().totalAmount;

  const title =
    phase === "processing"
      ? "Emitiendo factura..."
      : phase === "success"
        ? "¡Factura emitida!"
        : phase === "error"
          ? "No se pudo emitir la factura"
          : isElectronic
            ? "Emitir Factura Electrónica"
            : "Emitir Factura";

  const footer =
    phase === "confirm" ? (
      <>
        <ZHBtn type="button" variant="ghost" size="md" onClick={onCancel}>
          Cancelar
        </ZHBtn>
        <ZHBtn
          ref={confirmBtnRef}
          type="button"
          variant="primary"
          size="md"
          onClick={onConfirm}
        >
          Emitir Factura
        </ZHBtn>
      </>
    ) : phase === "success" ? (
      <>
        {printingPrefs.mode !== "NeverAutoPrint" && (
          <ZHBtn
            type="button"
            variant="ghost"
            size="md"
            disabled={receiptPrintState === "printing"}
            onClick={() => void handlePrintReceipt()}
          >
            <span className="material-symbols-outlined zh-icon-md">
              receipt_long
            </span>
            {receiptPrintState === "printing"
              ? "Imprimiendo..."
              : receiptPrintState === "success"
                ? "Reimprimir tirilla"
                : "Imprimir tirilla"}
          </ZHBtn>
        )}
        <ZHBtn
          type="button"
          variant="ghost"
          size="md"
          disabled={ridePending}
          onClick={onPrintRide}
        >
          Imprimir RIDE
        </ZHBtn>
        <ZHBtn
          type="button"
          variant="ghost"
          size="md"
          disabled={ridePending}
          onClick={onDownloadPdf}
        >
          Descargar PDF
        </ZHBtn>
        {result?.emissionType === "Electronic" && (
          <ZHBtn
            type="button"
            variant="ghost"
            size="md"
            disabled={xmlDownloading}
            onClick={onDownloadXml}
          >
            {xmlDownloading ? "Descargando..." : "Descargar XML"}
          </ZHBtn>
        )}
        <ZHBtn type="button" variant="primary" size="md" onClick={onNewSale}>
          Nueva venta
        </ZHBtn>
      </>
    ) : phase === "error" ? (
      <>
        <ZHBtn type="button" variant="ghost" size="md" onClick={onCancel}>
          Cerrar
        </ZHBtn>
        <ZHBtn type="button" variant="primary" size="md" onClick={onRetry}>
          Reintentar
        </ZHBtn>
      </>
    ) : undefined; // 'processing' — sin acciones disponibles

  return (
    <ZHModal
      open
      size="sm"
      title={title}
      onClose={handleClose}
      closeOnBackdrop={canClose}
      footer={footer}
    >
      {phase === "confirm" && (
        <div className="sf-authorize-summary">
          <dl className="sf-authorize-summary__grid">
            <dt>Cliente</dt>
            <dd>{customerName || "—"}</dd>
            <dt>Nro. de productos</dt>
            <dd>{lineCount}</dd>
            <dt>Subtotal</dt>
            <dd>
              <ZHMoneyValue value={subtotal} decimals={dc} />
            </dd>
            <dt>Descuento</dt>
            <dd>
              <ZHMoneyValue value={discount} decimals={dc} />
            </dd>
            <dt>IVA</dt>
            <dd>
              <ZHMoneyValue value={vat} decimals={dc} />
            </dd>
            <dt>Total</dt>
            <dd>
              <ZHMoneyValue value={total} decimals={dc} emphasis="total" />
            </dd>
          </dl>
          <p className="zh-confirm-message">
            {isElectronic
              ? "Esta acción emitirá la factura electrónica y no podrá editarse posteriormente."
              : "Esta acción emitirá la factura y no podrá editarse posteriormente."}
          </p>
        </div>
      )}

      {phase === "processing" && (
        <ul className="sf-issue-steps">
          {ISSUE_STEPS.map((label, i) => (
            <li
              key={label}
              className={`sf-issue-step${i < stepIndex ? " sf-issue-step--done" : i === stepIndex ? " sf-issue-step--active" : ""}`}
            >
              <span className="sf-issue-step__num">
                {i < stepIndex ? (
                  <span className="material-symbols-outlined zh-icon-sm">
                    check
                  </span>
                ) : (
                  i + 1
                )}
              </span>
              <span className="sf-issue-step__label">{label}</span>
              {i === stepIndex && (
                <span className="sf-search-spinner" aria-hidden="true" />
              )}
            </li>
          ))}
        </ul>
      )}

      {phase === "success" && result && (
        <div className="sf-issue-success">
          <div className="sf-issue-success__number zh-code-value">{result.invoiceNumber}</div>
          <dl className="sf-authorize-summary__grid">
            {result.emissionType === "Electronic" && (
              <>
                <dt>Estado electrónico</dt>
                <dd>
                  <Badge
                    label={
                      ELECTRONIC_STATUS_LABEL[result.electronicStatus] ??
                      result.electronicStatus
                    }
                    variant={electronicStatusVariant(result.electronicStatus)}
                    size="md"
                  />
                </dd>
              </>
            )}
            {result.accessKey && (
              <>
                <dt>Clave de acceso</dt>
                <dd className="sf-issue-success__mono zh-code-value">{result.accessKey}</dd>
              </>
            )}
            {result.authorizationNumber && (
              <>
                <dt>Nro. de autorización</dt>
                <dd className="sf-issue-success__mono zh-code-value">
                  {result.authorizationNumber}
                </dd>
              </>
            )}
            <dt>Fecha/hora</dt>
            <dd>
              {formatDateTime(result.authorizationDate ?? result.createdAt)}
            </dd>
            <dt>Total</dt>
            <dd>
              <ZHMoneyValue value={result.grandTotal} decimals={dc} emphasis="total" />
            </dd>
            {cashDue > 0 && (
              <>
                <dt>Dinero entregado</dt>
                <dd>
                  <ZHMoneyValue value={cashReceived} decimals={dc} />
                </dd>
                <dt>Vuelto</dt>
                <dd>
                  <ZHMoneyValue value={cashChange} decimals={dc} emphasis="total" />
                </dd>
              </>
            )}
          </dl>
          {(result.electronicIssueError ||
            (result.emissionType === "Electronic" &&
              result.electronicStatus !== "Authorized")) && (
            <ZHPageNotice
              variant="warning"
              message={
                result.electronicIssueError
                  ? `El documento electrónico quedó pendiente de autorización: ${result.electronicIssueError} Puede reintentarlo desde el Monitor de Documentos Electrónicos.`
                  : `El documento electrónico quedó en estado "${ELECTRONIC_STATUS_LABEL[result.electronicStatus] ?? result.electronicStatus}", pendiente de autorización. Puede reintentarlo desde el Monitor de Documentos Electrónicos.`
              }
            />
          )}
          {receiptPrintState !== "idle" && receiptPrintMessage && (
            <ZHPageNotice
              variant={receiptPrintState === "error" ? "error" : "info"}
              message={receiptPrintMessage}
              detail={
                receiptPrintState === "error"
                  ? (receiptPrintDetail ?? undefined)
                  : undefined
              }
            />
          )}
        </div>
      )}

      {phase === "error" && error && (
        <ZHPageNotice
          variant="error"
          message={error.message}
          detail="La factura conserva su número asignado — puede reintentar sin riesgo de duplicarlo."
        />
      )}
    </ZHModal>
  );
}
