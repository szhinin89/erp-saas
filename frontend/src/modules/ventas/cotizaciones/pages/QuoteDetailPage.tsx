import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZhDateInput } from '../../../../components/zh/inputs';
import { useI18n } from '../../../../i18n/i18n';
import { useAuthStore } from '../../../../store/authStore';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import { warehouseService, type WarehouseDto } from '../../../inventario/warehouses/api/warehouseService';
import { useQuoteActions, useQuoteDetail } from '../hooks/useQuotes';
import type { QuoteStatus } from '../api/quoteService';

function statusBadgeClass(status: QuoteStatus): string {
  const map: Record<string, string> = {
    draft: 'badge--gray',
    sent: 'badge--blue',
    approved: 'badge--green',
    rejected: 'badge--red',
    expired: 'badge--orange',
    cancelled: 'badge--red',
    converted: 'badge--blue',
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

export function QuoteDetailPage() {
  const { t } = useI18n();
  const { publicId } = useParams<{ publicId: string }>();
  const navigate = useNavigate();
  const { canShow } = usePermissionsUi();
  const canView = canShow('sales.quotes.view');
  const canUpdate = canShow('sales.quotes.update');
  const canCreateOrder = canShow('sales.orders.create');

  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);
  const { data, loading, error, refetch } = useQuoteDetail(publicId ?? null);
  const actions = useQuoteActions(refetch);

  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [warehouseId, setWarehouseId] = useState('');
  const [requiredDate, setRequiredDate] = useState('');
  const [convertError, setConvertError] = useState<string | null>(null);

  const loadWarehouses = useCallback(() => {
    void warehouseService.list('active').then((items) => {
      setWarehouses(items);
      if (items.length > 0) setWarehouseId(items[0]!.id);
    });
  }, []);

  useEffect(() => {
    loadWarehouses();
  }, [loadWarehouses, companySessionVersion]);

  useEffect(() => {
    if (data?.validUntil) setRequiredDate(data.validUntil);
  }, [data?.validUntil]);

  if (!canView) return <NoAccessPage title={t('ventas.cotizaciones.title')} />;

  const statusLabel = (status: QuoteStatus) =>
    t(`ventas.cotizaciones.status.${status.charAt(0).toLowerCase()}${status.slice(1)}`);

  const canApprove =
    data != null &&
    (data.status === 'Draft' || data.status === 'Sent') &&
    canUpdate;

  const canConvert =
    data?.status === 'Approved' && canCreateOrder;

  const canCancelQuote =
    data != null &&
    (data.status === 'Draft' || data.status === 'Sent' || data.status === 'Approved') &&
    canUpdate;

  const handleApprove = async () => {
    if (!data) return;
    await actions.approve(data.publicId);
  };

  const handleConvert = async () => {
    if (!data) return;
    setConvertError(null);
    if (!warehouseId) {
      setConvertError(t('ventas.cotizaciones.form.warehouseRequired'));
      return;
    }

    const order = await actions.convertToOrder({
      quotePublicId: data.publicId,
      warehouseId,
      requiredDate: requiredDate || null,
    });

    if (order && typeof order === 'object' && 'publicId' in order) {
      navigate(`/sales/orders/${(order as { publicId: string }).publicId}`);
    }
  };

  const handleCancel = async () => {
    if (!data) return;
    await actions.cancel(data.publicId);
  };

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.sales')}
      title={loading ? t('common.loading') : (data?.quoteNumber ?? t('ventas.cotizaciones.title'))}
      action={
        <>
          {canApprove && (
            <ZHBtn variant="primary" size="md" disabled={actions.loading} onClick={() => void handleApprove()}>
              {t('ventas.cotizaciones.action.approve')}
            </ZHBtn>
          )}
          {canCancelQuote && (
            <ZHBtn variant="ghost" size="md" disabled={actions.loading} onClick={() => void handleCancel()}>
              {t('ventas.cotizaciones.action.cancel')}
            </ZHBtn>
          )}
          <ZHBtn variant="ghost" size="md" onClick={() => navigate('/sales/quotes')}>
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
          <EmptyState message={t('ventas.cotizaciones.notFound')} />
        </div>
      ) : (
        <>
          <div className="pg-section pg-section--mb-4">
            <div className="pg-section-body">
              <div className="pg-form-grid pg-form-grid--4">
                <InfoItem label={t('ventas.cotizaciones.col.status')}>
                  <span className={statusBadgeClass(data.status)}>{statusLabel(data.status)}</span>
                </InfoItem>
                <InfoItem label={t('ventas.cotizaciones.col.customer')}>{data.businessPartnerName}</InfoItem>
                <InfoItem label={t('ventas.cotizaciones.col.issueDate')}>{data.issueDate}</InfoItem>
                <InfoItem label={t('ventas.cotizaciones.col.validUntil')}>{data.validUntil}</InfoItem>
                <InfoItem label={t('ventas.cotizaciones.col.total')}>
                  <span className="mono pg-doc-hero-mono">${data.total.toFixed(2)}</span>
                </InfoItem>
                {data.notes && (
                  <div className="oc-doc-notes">
                    <p className="subtle pg-doc-notes-label">{t('ventas.cotizaciones.form.notes')}</p>
                    <p>{data.notes}</p>
                  </div>
                )}
              </div>
            </div>
          </div>

          <div className="pg-section pg-section--mb-4">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">list_alt</span>
                <span className="pg-section-label">{t('ventas.cotizaciones.form.lines')}</span>
              </div>
            </div>
            <div className="pg-overflow-x">
              <table className="table">
                <thead>
                  <tr>
                    <th>{t('ventas.cotizaciones.form.product')}</th>
                    <th>{t('ventas.cotizaciones.form.sku')}</th>
                    <th className="pg-th-right">{t('ventas.cotizaciones.form.quantity')}</th>
                    <th className="pg-th-right">{t('ventas.cotizaciones.form.unitPrice')}</th>
                    <th className="pg-th-right">{t('ventas.cotizaciones.form.taxPct')}</th>
                    <th className="pg-th-right">{t('ventas.cotizaciones.col.total')}</th>
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

          {canConvert && (
            <div className="pg-section">
              <div className="pg-section-header">
                <div className="pg-section-header-left">
                  <span className="material-symbols-outlined pg-section-icon">shopping_cart</span>
                  <span className="pg-section-label">{t('ventas.cotizaciones.convert.title')}</span>
                </div>
              </div>
              <div className="pg-section-body">
                {(convertError || actions.error) && (
                  <ZHPageNotice
                    variant="error"
                    message={t('common.error')}
                    detail={convertError ?? actions.error ?? ''}
                  />
                )}
                <div className="pg-form-grid pg-form-grid--3">
                  <ZHField label={t('ventas.cotizaciones.form.warehouse')} required>
                    <select
                      className="zh-input"
                      value={warehouseId}
                      onChange={(e) => setWarehouseId(e.target.value)}
                      required
                    >
                      {warehouses.map((w) => (
                        <option key={w.id} value={w.id}>
                          {w.name}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                  <ZHField label={t('ventas.cotizaciones.form.requiredDate')}>
                    <ZhDateInput
                      value={requiredDate}
                      onChange={(e) => setRequiredDate(e.target.value)}
                    />
                  </ZHField>
                  <div className="pg-form-actions">
                    <ZHBtn
                      variant="primary"
                      size="md"
                      disabled={actions.loading}
                      onClick={() => void handleConvert()}
                    >
                      {t('ventas.cotizaciones.action.convertToOrder')}
                    </ZHBtn>
                  </div>
                </div>
              </div>
            </div>
          )}

          {data.status === 'Converted' && (
            <div className="pg-section">
              <ZHPageNotice variant="info" message={t('ventas.cotizaciones.convert.alreadyConverted')} />
              {data.relatedSalesOrderPublicId && (
                <div className="pg-form-actions">
                  <ZHBtn
                    variant="secondary"
                    size="md"
                    onClick={() => navigate(`/sales/orders/${data.relatedSalesOrderPublicId}`)}
                  >
                    {t('ventas.cotizaciones.viewOrder')}
                  </ZHBtn>
                </div>
              )}
            </div>
          )}
        </>
      )}
    </ErpPageTemplate>
  );
}
