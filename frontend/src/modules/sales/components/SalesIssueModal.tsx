import { useCallback, useEffect, useRef } from 'react';
import { ZHModal } from '../../../components/zh/ZHModal';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { Badge, type BadgeVariant } from '../../../components/PageShell';
import { formatMoneyWithSymbol } from '../../../lib/sanitizers';
import { getDecimalConfig } from '../../../lib/config/decimal.config';
import { formatDateTime } from '../../../lib/formatters/dateFormatters';
import type { SalesInvoiceDto } from '../api/salesService';
import { ISSUE_STEPS, type IssuePhase, type IssueErrorInfo } from '../hooks/useSalesPage';
import '../styles/sales-invoice.css';

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

  // Error (solo fallas de infraestructura — interno/comunicación)
  error: IssueErrorInfo | null;
  onRetry: () => void;

  // Shared
  onCancel: () => void;
  onConfirm: () => void;
  onNewSale: () => void;
}

function electronicStatusVariant(status: string): BadgeVariant {
  if (status === 'Authorized') return 'green';
  if (status === 'Rejected' || status === 'Failed') return 'red';
  if (status === 'None') return 'gray';
  return 'orange'; // Draft/Signed/Sent/Received — en trámite ante el SRI
}

const ELECTRONIC_STATUS_LABEL: Record<string, string> = {
  None: 'Sin generar',
  Draft: 'Borrador',
  Signed: 'Firmado',
  Sent: 'Enviado',
  Received: 'Recibido',
  Authorized: 'Autorizado',
  Rejected: 'Rechazado',
  Failed: 'Fallido',
};

export function SalesIssueModal({
  phase, isElectronic,
  customerName, lineCount, subtotal, discount, vat, total,
  stepIndex,
  result, ridePending, xmlDownloading, onPrintRide, onDownloadPdf, onDownloadXml,
  error, onRetry,
  onCancel, onConfirm, onNewSale,
}: SalesIssueModalProps) {
  // Durante la emisión no se puede cerrar el flujo (Escape, backdrop y la X
  // del header quedan neutralizados porque onClose ignora la llamada).
  const canClose = phase !== 'processing';
  const confirmBtnRef = useRef<HTMLButtonElement>(null);

  // ZHModal enfoca automáticamente el primer elemento focusable del cuerpo/footer
  // en orden de documento — el cuerpo de 'confirm' no tiene campos, así que ese
  // primer elemento sería "Cancelar". Se sobrescribe a propósito para que Enter
  // confirme la emisión de inmediato (flujo rápido de cajero).
  useEffect(() => {
    if (phase === 'confirm') confirmBtnRef.current?.focus();
  }, [phase]);

  // Memoizado: ZHModal reejecuta su efecto de foco/Escape cuando `onClose`
  // cambia de identidad (dependencia de su useEffect) — sin useCallback se
  // recreaba en cada render y robaba el foco repetidamente durante 'processing'.
  const handleClose = useCallback(() => {
    if (!canClose) return;
    if (phase === 'success') onNewSale();
    else onCancel();
  }, [canClose, phase, onNewSale, onCancel]);

  if (phase === 'idle') return null;
  const dc = getDecimalConfig().totalAmount;

  const title = phase === 'processing' ? 'Emitiendo factura...'
    : phase === 'success' ? '¡Factura emitida!'
    : phase === 'error' ? 'No se pudo emitir la factura'
    : isElectronic ? 'Emitir Factura Electrónica' : 'Emitir Factura';

  const footer = phase === 'confirm' ? (<>
      <ZHBtn type="button" variant="ghost" size="md" onClick={onCancel}>Cancelar</ZHBtn>
      <button ref={confirmBtnRef} type="button" className="zh-btn zh-btn--primary zh-btn--md" onClick={onConfirm}>Emitir Factura</button>
    </>)
    : phase === 'success' ? (<>
      <ZHBtn type="button" variant="ghost" size="md" disabled={ridePending} onClick={onPrintRide}>Imprimir RIDE</ZHBtn>
      <ZHBtn type="button" variant="ghost" size="md" disabled={ridePending} onClick={onDownloadPdf}>Descargar PDF</ZHBtn>
      {result?.emissionType === 'Electronic' && (
        <ZHBtn type="button" variant="ghost" size="md" disabled={xmlDownloading} onClick={onDownloadXml}>
          {xmlDownloading ? 'Descargando...' : 'Descargar XML'}
        </ZHBtn>
      )}
      <ZHBtn type="button" variant="primary" size="md" onClick={onNewSale}>Nueva venta</ZHBtn>
    </>)
    : phase === 'error' ? (<>
      <ZHBtn type="button" variant="ghost" size="md" onClick={onCancel}>Cerrar</ZHBtn>
      <ZHBtn type="button" variant="primary" size="md" onClick={onRetry}>Reintentar</ZHBtn>
    </>)
    : undefined; // 'processing' — sin acciones disponibles

  return (
    <ZHModal open size="sm" title={title} onClose={handleClose} closeOnBackdrop={canClose} footer={footer}>
      {phase === 'confirm' && (
        <div className="sf-authorize-summary">
          <dl className="sf-authorize-summary__grid">
            <dt>Cliente</dt><dd>{customerName || '—'}</dd>
            <dt>Nro. de productos</dt><dd>{lineCount}</dd>
            <dt>Subtotal</dt><dd>{formatMoneyWithSymbol(subtotal, dc)}</dd>
            <dt>Descuento</dt><dd>{formatMoneyWithSymbol(discount, dc)}</dd>
            <dt>IVA</dt><dd>{formatMoneyWithSymbol(vat, dc)}</dd>
            <dt>Total</dt><dd><strong>{formatMoneyWithSymbol(total, dc)}</strong></dd>
          </dl>
          <p className="zh-confirm-message">
            {isElectronic
              ? 'Esta acción emitirá la factura electrónica y no podrá editarse posteriormente.'
              : 'Esta acción emitirá la factura y no podrá editarse posteriormente.'}
          </p>
        </div>
      )}

      {phase === 'processing' && (
        <ul className="sf-issue-steps">
          {ISSUE_STEPS.map((label, i) => (
            <li key={label} className={`sf-issue-step${i < stepIndex ? ' sf-issue-step--done' : i === stepIndex ? ' sf-issue-step--active' : ''}`}>
              <span className="sf-issue-step__num">
                {i < stepIndex ? <span className="material-symbols-outlined zh-icon-sm">check</span> : i + 1}
              </span>
              <span className="sf-issue-step__label">{label}</span>
              {i === stepIndex && <span className="sf-search-spinner" aria-hidden="true" />}
            </li>
          ))}
        </ul>
      )}

      {phase === 'success' && result && (
        <div className="sf-issue-success">
          <div className="sf-issue-success__number">{result.invoiceNumber}</div>
          <dl className="sf-authorize-summary__grid">
            {result.emissionType === 'Electronic' && (<>
              <dt>Estado electrónico</dt>
              <dd>
                <Badge
                  label={ELECTRONIC_STATUS_LABEL[result.electronicStatus] ?? result.electronicStatus}
                  variant={electronicStatusVariant(result.electronicStatus)}
                  size="md" />
              </dd>
            </>)}
            {result.accessKey && (<><dt>Clave de acceso</dt><dd className="sf-issue-success__mono">{result.accessKey}</dd></>)}
            {result.authorizationNumber && (<><dt>Nro. de autorización</dt><dd className="sf-issue-success__mono">{result.authorizationNumber}</dd></>)}
            <dt>Fecha/hora</dt><dd>{formatDateTime(result.authorizationDate ?? result.createdAt)}</dd>
            <dt>Total</dt><dd><strong>{formatMoneyWithSymbol(result.grandTotal, dc)}</strong></dd>
          </dl>
          {(result.electronicIssueError || (result.emissionType === 'Electronic' && result.electronicStatus !== 'Authorized')) && (
            <ZHPageNotice variant="warning" message={
              result.electronicIssueError
                ? `El documento electrónico quedó pendiente de autorización: ${result.electronicIssueError} Puede reintentarlo desde el Monitor de Documentos Electrónicos.`
                : `El documento electrónico quedó en estado "${ELECTRONIC_STATUS_LABEL[result.electronicStatus] ?? result.electronicStatus}", pendiente de autorización. Puede reintentarlo desde el Monitor de Documentos Electrónicos.`
            } />
          )}
        </div>
      )}

      {phase === 'error' && error && (
        <ZHPageNotice variant="error" message={error.message}
          detail="La factura conserva su número asignado — puede reintentar sin riesgo de duplicarlo." />
      )}
    </ZHModal>
  );
}
