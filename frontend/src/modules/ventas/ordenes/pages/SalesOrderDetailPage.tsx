import { useNavigate, useParams } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../../i18n/i18n';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import { useSalesOrderActions, useSalesOrderDetail } from '../hooks/useSalesOrders';
import type { SalesOrderStatus } from '../api/salesOrderService';

function statusBadgeClass(status: SalesOrderStatus): string {
  const map: Record<string, string> = {
    draft: 'badge--gray',
    confirmed: 'badge--blue',
    partiallyinvoiced: 'badge--orange',
    invoiced: 'badge--green',
    cancelled: 'badge--red',
  };
  return `badge badge--md ${map[status.toLowerCase()] ?? 'badge--gray'}`;
}

function InfoItem({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="subtle pg-info-item-label">{label}</p>
      <div className="pg-info-item-value">{children}</div>
    </div>
  );
}

export function SalesOrderDetailPage() {
  const { t } = useI18n();
  const { publicId } = useParams<{ publicId: string }>();
  const navigate = useNavigate();
  const { canShow } = usePermissionsUi();
  const canView = canShow('sales.orders.view');
  const canUpdate = canShow('sales.orders.update');
  const canInvoice = canShow('sales.invoices.create');

  const { data, loading, error, refetch } = useSalesOrderDetail(publicId ?? null);
  const actions = useSalesOrderActions(refetch);

  if (!canView) return <NoAccessPage title={t('ventas.pedidos.title')} />;

  const statusLabel = (status: SalesOrderStatus) =>
    t(`ventas.pedidos.status.${status.charAt(0).toLowerCase()}${status.slice(1)}`);

  const canConfirm =
    data?.status === 'Draft' && canUpdate;
  const canCreateInvoice =
    data != null &&
    data.status === 'Confirmed' &&
    canInvoice;

  const canCancelOrder =
    data != null &&
    (data.status === 'Draft' || data.status === 'Confirmed') &&
    canUpdate;

  const handleConfirm = async () => {
    if (!data) return;
    await actions.confirm(data.publicId);
  };

  const handleInvoice = async () => {
    if (!data) return;
    const invoicePublicId = await actions.invoice(data.publicId);
    if (typeof invoicePublicId === 'string' && invoicePublicId) {
      navigate(`/sales/invoices/${invoicePublicId}`);
    }
  };

  const handleCancel = async () => {
    if (!data) return;
    await actions.cancel(data.publicId);
  };

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.sales')}
      title={loading ? t('common.loading') : (data?.orderNumber ?? t('ventas.pedidos.title'))}
      action={
        <>
          {canConfirm && (
            <ZHBtn variant="primary" size="md" disabled={actions.loading} onClick={() => void handleConfirm()}>
              {t('ventas.pedidos.action.confirm')}
            </ZHBtn>
          )}
          {canCreateInvoice && (
            <ZHBtn variant="secondary" size="md" disabled={actions.loading} onClick={() => void handleInvoice()}>
              {t('ventas.pedidos.action.invoice')}
            </ZHBtn>
          )}
          {canCancelOrder && (
            <ZHBtn variant="ghost" size="md" disabled={actions.loading} onClick={() => void handleCancel()}>
              {t('ventas.pedidos.action.cancel')}
            </ZHBtn>
          )}
          <ZHBtn variant="ghost" size="md" onClick={() => navigate('/sales/orders')}>
            {t('common.back')}
          </ZHBtn>
        </>
      }
    >
      {actions.error && <ZHPageNotice variant="error" message={t('common.error')} detail={actions.error} />}
      {error && <ZHPageNotice variant="error" message={t('common.loadError')} detail={error} />}

      {loading ? (
        <div className="pg-pad-40">
          <LoadingState />
        </div>
      ) : !data ? (
        <div className="pg-pad-40">
          <EmptyState message={t('ventas.pedidos.notFound')} />
        </div>
      ) : (
        <>
          <div className="pg-section pg-section--mb-4">
            <div className="pg-section-body">
              <div className="pg-form-grid pg-form-grid--4">
                <InfoItem label={t('ventas.pedidos.col.status')}>
                  <span className={statusBadgeClass(data.status)}>{statusLabel(data.status)}</span>
                </InfoItem>
                <InfoItem label={t('ventas.pedidos.col.customer')}>{data.businessPartnerName}</InfoItem>
                <InfoItem label={t('ventas.pedidos.form.warehouse')}>{data.warehouseName}</InfoItem>
                <InfoItem label={t('ventas.pedidos.col.issueDate')}>{data.issueDate}</InfoItem>
                <InfoItem label={t('ventas.pedidos.col.requiredDate')}>
                  {data.requiredDate ?? '—'}
                </InfoItem>
                <InfoItem label={t('ventas.pedidos.col.total')}>
                  <span className="mono pg-doc-hero-mono">${data.total.toFixed(2)}</span>
                </InfoItem>
                {data.notes && (
                  <div className="oc-doc-notes">
                    <p className="subtle pg-doc-notes-label">{t('ventas.pedidos.form.notes')}</p>
                    <p>{data.notes}</p>
                  </div>
                )}
                {data.relatedInvoicePublicId && (
                  <InfoItem label={t('ventas.pedidos.relatedInvoice')}>
                    <ZHBtn
                      variant="ghost"
                      size="sm"
                      onClick={() => navigate(`/sales/invoices/${data.relatedInvoicePublicId}`)}
                    >
                      {t('ventas.pedidos.viewInvoice')}
                    </ZHBtn>
                  </InfoItem>
                )}
              </div>
            </div>
          </div>

          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">list_alt</span>
                <span className="pg-section-label">{t('ventas.pedidos.form.lines')}</span>
              </div>
            </div>
            <div className="pg-overflow-x">
              <table className="table">
                <thead>
                  <tr>
                    <th>{t('ventas.pedidos.form.product')}</th>
                    <th>{t('ventas.pedidos.form.sku')}</th>
                    <th className="pg-th-right">{t('ventas.pedidos.form.quantity')}</th>
                    <th className="pg-th-right">{t('ventas.pedidos.form.unitPrice')}</th>
                    <th className="pg-th-right">{t('ventas.pedidos.form.taxPct')}</th>
                    <th className="pg-th-right">{t('ventas.pedidos.col.total')}</th>
                  </tr>
                </thead>
                <tbody>
                  {data.lines.map((line) => (
                    <tr key={line.lineNo}>
                      <td>{line.productName}</td>
                      <td className="mono">{line.sku}</td>
                      <td className="mono pg-td-right">{line.quantity}</td>
                      <td className="mono pg-td-right">${line.unitPrice.toFixed(4)}</td>
                      <td className="mono pg-td-right">{line.taxRatePct}%</td>
                      <td className="mono pg-td-right pg-cell-strong">${line.lineTotal.toFixed(2)}</td>
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
