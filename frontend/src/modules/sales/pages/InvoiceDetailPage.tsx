import { useNavigate, useParams } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { invoiceNumber } from ../api/salesInvoicesMapper';
import { salesInvoicesService } from '../api/salesInvoicesService';
import { openSalesInvoicePrint } from '../utils/openSalesInvoicePrint';
import { useSalesInvoiceActions, useSalesInvoiceDetail } from '../hooks/useSalesInvoices';
import { formatDate } from '../../../lib/formatters/dateFormatters';

function statusBadgeClass(estado: string): string {
  const e = estado.toLowerCase();
  const base = 'badge badge--md badge--upper';
  if (e === 'autorizado') return `${base} badge--green`;
  if (e === 'borrador' || e === 'validado' || e === 'procesando') return `${base} badge--orange`;
  if (e === 'anulado' || e === 'rechazado' || e === 'errorenvio') return `${base} badge--red`;
  return `${base} badge--gray`;
}

function InfoItem({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="subtle pg-info-item-label">{label}</p>
      <div className="pg-info-item-value">{children}</div>
    </div>
  );
}

export function InvoiceDetailPage() {
  const { t } = useI18n();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { canShow } = usePermissionsUi();
  const canView = canShow('sales.invoices.view');
  const canUpdate = canShow('sales.invoices.update');
  const canVoid = canShow('sales.invoices.void');

  const { data, loading, error, refetch } = useSalesInvoiceDetail(id ?? null);
  const actions = useSalesInvoiceActions(refetch);

  if (!canView) return <NoAccessPage title={t('ventas.facturas.title')} />;

  const estado = data?.status.toLowerCase() ?? '';
  const numero = data ? invoiceNumber(data) : '';
  const isDraft = estado === 'borrador';
  const isValidated = estado === 'validado';
  const isAuthorized = estado === 'autorizado';
  const isError = estado === 'errorenvio' || estado === 'rechazado';
  const canValidate = isDraft && canUpdate;
  const canEmit = isValidated && canUpdate;
  const canRetry = isError && canUpdate;
  const canVoidInvoice = (isDraft || isValidated || isError) && canVoid;

  const handlePrint = async () => {
    if (!data) return;
    await openSalesInvoicePrint(data.id);
  };

  const handleRide = async () => {
    if (!data) return;
    const blob = await salesInvoicesService.downloadRide(data.id);
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `RIDE-${numero}.pdf`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.sales')}
      title={loading ? t('common.loading') : (data ? numero : t('ventas.facturas.title'))}
      action={
        <>
          {canValidate && (
            <ZHBtn variant="secondary" size="md" disabled={actions.loading} onClick={() => void actions.validate(data!.id)}>
              {t('ventas.facturas.action.validate')}
            </ZHBtn>
          )}
          {canEmit && (
            <ZHBtn variant="primary" size="md" disabled={actions.loading} onClick={() => void actions.issue(data!.id)}>
              {t('ventas.facturas.action.emit')}
            </ZHBtn>
          )}
          {canRetry && (
            <ZHBtn variant="secondary" size="md" disabled={actions.loading} onClick={() => void actions.retry(data!.id)}>
              {t('ventas.facturas.action.retry')}
            </ZHBtn>
          )}
          {canVoidInvoice && (
            <ZHBtn variant="ghost" size="md" disabled={actions.loading} onClick={() => void actions.voidInvoice(data!.id)}>
              {t('ventas.facturas.action.void')}
            </ZHBtn>
          )}
          {isAuthorized && (
            <>
              <ZHBtn variant="secondary" size="md" onClick={() => void handleRide()}>
                RIDE
              </ZHBtn>
              <ZHBtn variant="primary" size="md" onClick={() => void handlePrint()}>
                {t('ventas.facturas.print')}
              </ZHBtn>
            </>
          )}
          <ZHBtn variant="ghost" size="md" onClick={() => navigate('/sales/invoices')}>
            {t('common.back')}
          </ZHBtn>
        </>
      }
    >
      {actions.error && <ZHPageNotice variant="error" message={t('common.error')} detail={actions.error} />}
      {error && <ZHPageNotice variant="error" message={t('common.loadError')} detail={error} />}

      {loading ? (
        <div className="pg-pad-40"><LoadingState /></div>
      ) : !data ? (
        <div className="pg-pad-40"><EmptyState message={t('ventas.facturas.notFound')} /></div>
      ) : (
        <>
          <div className="pg-section pg-section--mb-4">
            <div className="pg-section-body">
              <div className="pg-form-grid pg-form-grid--4">
                <InfoItem label={t('ventas.facturas.col.status')}>
                  <span className={statusBadgeClass(data.status)}>{data.status}</span>
                </InfoItem>
                <InfoItem label={t('ventas.facturas.col.customer')}>{data.clienteNombre}</InfoItem>
                <InfoItem label={t('ventas.facturas.col.date')}>
                  {formatDate(data.issueDate)}
                </InfoItem>
                <InfoItem label={t('ventas.facturas.col.total')}>
                  <span className="mono pg-doc-hero-mono">${data.total.toFixed(2)}</span>
                </InfoItem>
                {data.numeroAutorizacion && (
                  <InfoItem label={t('ventas.facturas.col.authNumber')}>{data.numeroAutorizacion}</InfoItem>
                )}
                {data.mensajeError && (
                  <div className="oc-doc-notes">
                    <p className="subtle pg-doc-notes-label">{t('ventas.facturas.col.error')}</p>
                    <p>{data.mensajeError}</p>
                  </div>
                )}
              </div>
            </div>
          </div>

          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">list_alt</span>
                <span className="pg-section-label">{t('ventas.facturas.lines')}</span>
              </div>
            </div>
            <div className="pg-overflow-x">
              <table className="table">
                <thead>
                  <tr>
                    <th>{t('ventas.facturas.col.description')}</th>
                    <th className="pg-th-right">{t('ventas.facturas.col.quantity')}</th>
                    <th className="pg-th-right">{t('ventas.facturas.col.unitPrice')}</th>
                    <th className="pg-th-right">{t('ventas.facturas.col.subtotal')}</th>
                    <th className="pg-th-right">{t('ventas.facturas.col.total')}</th>
                  </tr>
                </thead>
                <tbody>
                  {data.lines.map((line) => (
                    <tr key={line.id}>
                      <td>{line.description}</td>
                      <td className="mono pg-td-right">{line.quantity}</td>
                      <td className="mono pg-td-right">${line.unitPrice.toFixed(4)}</td>
                      <td className="mono pg-td-right">${line.subtotal.toFixed(2)}</td>
                      <td className="mono pg-td-right pg-cell-strong">${line.total.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </ErpPageTemplate>
  );
}
