import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZhDateInput } from '../../../../components/zh/inputs';
import { useI18n } from '../../../../i18n/i18n';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import { useQuotesList } from '../hooks/useQuotes';
import type { QuoteStatus, QuotesFilter } from '../api/quoteService';

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

export function QuotesListPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const navigate = useNavigate();
  const canView = canShow('sales.quotes.view');
  const canCreate = canShow('sales.quotes.create');

  const [filter, setFilter] = useState<QuotesFilter>({ pageNumber: 1, pageSize: 20 });
  const { result, loading, error } = useQuotesList(filter);

  if (!canView) return <NoAccessPage title={t('ventas.cotizaciones.title')} />;

  const items = result?.items ?? [];
  const total = result?.totalCount ?? 0;
  const page = filter.pageNumber ?? 1;
  const pageSize = filter.pageSize ?? 20;

  const statusLabel = (status: QuoteStatus) =>
    t(`ventas.cotizaciones.status.${status.charAt(0).toLowerCase()}${status.slice(1)}`);

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.sales')}
      title={t('ventas.cotizaciones.title')}
      subtitle={t('ventas.cotizaciones.subtitle')}
      action={
        canCreate ? (
          <button
            className="zh-btn zh-btn--primary"
            type="button"
            onClick={() => navigate('/sales/quotes/new')}
          >
            <span className="material-symbols-outlined">add</span>
            {t('ventas.cotizaciones.newQuote')}
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
                  status: (e.target.value as QuoteStatus) || undefined,
                  pageNumber: 1,
                }))
              }
            >
              <option value="">{t('ventas.cotizaciones.filter.allStatuses')}</option>
              <option value="Draft">{statusLabel('Draft')}</option>
              <option value="Sent">{statusLabel('Sent')}</option>
              <option value="Approved">{statusLabel('Approved')}</option>
              <option value="Converted">{statusLabel('Converted')}</option>
              <option value="Rejected">{statusLabel('Rejected')}</option>
              <option value="Expired">{statusLabel('Expired')}</option>
              <option value="Cancelled">{statusLabel('Cancelled')}</option>
            </select>
            <ZhDateInput
              value={filter.dateFrom ?? ''}
              onChange={(e) =>
                setFilter((f) => ({ ...f, dateFrom: e.target.value || undefined, pageNumber: 1 }))
              }
            />
            <ZhDateInput
              value={filter.dateTo ?? ''}
              onChange={(e) =>
                setFilter((f) => ({ ...f, dateTo: e.target.value || undefined, pageNumber: 1 }))
              }
            />
          </div>
          <div className="pg-table-controls-right">
            <span>{t('ventas.cotizaciones.records', { count: total })}</span>
          </div>
        </div>

        {loading ? (
          <div className="pg-pad-40">
            <LoadingState />
          </div>
        ) : items.length === 0 ? (
          <div className="pg-pad-40">
            <EmptyState message={t('ventas.cotizaciones.empty')} />
          </div>
        ) : (
          <div className="pg-overflow-x">
            <table className="table">
              <thead>
                <tr>
                  <th>{t('ventas.cotizaciones.col.number')}</th>
                  <th>{t('ventas.cotizaciones.col.customer')}</th>
                  <th>{t('ventas.cotizaciones.col.issueDate')}</th>
                  <th>{t('ventas.cotizaciones.col.validUntil')}</th>
                  <th className="pg-th-right">{t('ventas.cotizaciones.col.total')}</th>
                  <th>{t('ventas.cotizaciones.col.status')}</th>
                  <th className="pg-th-right">{t('common.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {items.map((quote) => (
                  <tr
                    key={quote.publicId}
                    className="pg-row-clickable"
                    onClick={() => navigate(`/sales/quotes/${quote.publicId}`)}
                  >
                    <td>
                      <strong className="mono">{quote.quoteNumber}</strong>
                    </td>
                    <td>{quote.businessPartnerName}</td>
                    <td>{quote.issueDate}</td>
                    <td>{quote.validUntil}</td>
                    <td className="mono pg-td-right">${quote.total.toFixed(2)}</td>
                    <td>
                      <span className={statusBadgeClass(quote.status)}>{statusLabel(quote.status)}</span>
                    </td>
                    <td className="pg-td-right">
                      <ZHBtn
                        variant="ghost"
                        size="sm"
                        onClick={(e) => {
                          e.stopPropagation();
                          navigate(`/sales/quotes/${quote.publicId}`);
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
            <span>{t('ventas.cotizaciones.page', { page, total: Math.ceil(total / pageSize) })}</span>
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
