import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../../i18n/i18n';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import { useSalesOrdersList } from '../hooks/useSalesOrders';
import type { SalesOrderStatus, SalesOrdersFilter } from '../api/salesOrderService';

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

export function SalesOrdersListPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const navigate = useNavigate();
  const canView = canShow('sales.orders.view');
  const canCreate = canShow('sales.orders.create');

  const [filter, setFilter] = useState<SalesOrdersFilter>({ pageNumber: 1, pageSize: 20 });
  const { result, loading, error } = useSalesOrdersList(filter);

  if (!canView) return <NoAccessPage title={t('ventas.pedidos.title')} />;

  const items = result?.items ?? [];
  const total = result?.totalCount ?? 0;
  const page = filter.pageNumber ?? 1;
  const pageSize = filter.pageSize ?? 20;

  const statusLabel = (status: SalesOrderStatus) =>
    t(`ventas.pedidos.status.${status.charAt(0).toLowerCase()}${status.slice(1)}`);

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.sales')}
      title={t('ventas.pedidos.title')}
      subtitle={t('ventas.pedidos.subtitle')}
      action={
        canCreate ? (
          <button
            className="zh-btn zh-btn--primary"
            type="button"
            onClick={() => navigate('/sales/orders/new')}
          >
            <span className="material-symbols-outlined">add</span>
            {t('ventas.pedidos.newOrder')}
          </button>
        ) : undefined
      }
    >
      {error && <ZHPageNotice variant="error" message={t('common.loadError')} detail={error} />}

      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <select
              className="zh-input"
              value={filter.status ?? ''}
              onChange={(e) =>
                setFilter((f) => ({
                  ...f,
                  status: (e.target.value as SalesOrderStatus) || undefined,
                  pageNumber: 1,
                }))
              }
            >
              <option value="">{t('ventas.pedidos.filter.allStatuses')}</option>
              <option value="Draft">{statusLabel('Draft')}</option>
              <option value="Confirmed">{statusLabel('Confirmed')}</option>
              <option value="PartiallyInvoiced">{statusLabel('PartiallyInvoiced')}</option>
              <option value="Invoiced">{statusLabel('Invoiced')}</option>
              <option value="Cancelled">{statusLabel('Cancelled')}</option>
            </select>
            <input
              className="zh-input"
              type="date"
              value={filter.dateFrom ?? ''}
              onChange={(e) =>
                setFilter((f) => ({ ...f, dateFrom: e.target.value || undefined, pageNumber: 1 }))
              }
            />
            <input
              className="zh-input"
              type="date"
              value={filter.dateTo ?? ''}
              onChange={(e) =>
                setFilter((f) => ({ ...f, dateTo: e.target.value || undefined, pageNumber: 1 }))
              }
            />
          </div>
          <div className="pg-table-controls-right">
            <span>{t('ventas.pedidos.records', { count: total })}</span>
          </div>
        </div>

        {loading ? (
          <div className="pg-pad-40">
            <LoadingState />
          </div>
        ) : items.length === 0 ? (
          <div className="pg-pad-40">
            <EmptyState message={t('ventas.pedidos.empty')} />
          </div>
        ) : (
          <div className="pg-overflow-x">
            <table className="table">
              <thead>
                <tr>
                  <th>{t('ventas.pedidos.col.number')}</th>
                  <th>{t('ventas.pedidos.col.customer')}</th>
                  <th>{t('ventas.pedidos.col.issueDate')}</th>
                  <th>{t('ventas.pedidos.col.requiredDate')}</th>
                  <th className="pg-th-right">{t('ventas.pedidos.col.total')}</th>
                  <th>{t('ventas.pedidos.col.status')}</th>
                  <th className="pg-th-right">{t('common.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {items.map((order) => (
                  <tr
                    key={order.publicId}
                    className="pg-row-clickable"
                    onClick={() => navigate(`/sales/orders/${order.publicId}`)}
                  >
                    <td>
                      <strong className="mono">{order.orderNumber}</strong>
                    </td>
                    <td>{order.businessPartnerName}</td>
                    <td>{order.issueDate}</td>
                    <td>{order.requiredDate ?? '—'}</td>
                    <td className="mono pg-td-right">${order.total.toFixed(2)}</td>
                    <td>
                      <span className={statusBadgeClass(order.status)}>{statusLabel(order.status)}</span>
                    </td>
                    <td className="pg-td-right">
                      <ZHBtn
                        variant="ghost"
                        size="sm"
                        onClick={(e) => {
                          e.stopPropagation();
                          navigate(`/sales/orders/${order.publicId}`);
                        }}
                      >
                        {t('common.view')}
                      </ZHBtn>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {total > pageSize && (
          <div className="pg-pagination">
            <ZHBtn
              variant="ghost"
              size="sm"
              disabled={page <= 1}
              onClick={() => setFilter((f) => ({ ...f, pageNumber: page - 1 }))}
            >
              {t('common.prev')}
            </ZHBtn>
            <span>{t('ventas.pedidos.page', { page, total: Math.ceil(total / pageSize) })}</span>
            <ZHBtn
              variant="ghost"
              size="sm"
              disabled={page * pageSize >= total}
              onClick={() => setFilter((f) => ({ ...f, pageNumber: page + 1 }))}
            >
              {t('common.next')}
            </ZHBtn>
          </div>
        )}
      </div>
    </ErpPageTemplate>
  );
}
