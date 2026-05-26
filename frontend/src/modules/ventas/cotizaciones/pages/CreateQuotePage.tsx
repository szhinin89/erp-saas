import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { ZhNumberInput, ZhDecimalInput } from '../../../../components/zh/inputs';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../../i18n/i18n';
import { useAuthStore } from '../../../../store/authStore';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import { businessPartnerFacade } from '../../../masterData/api/businessPartnerFacade';
import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';
import { useQuoteActions } from '../hooks/useQuotes';
import type { CreateQuoteRequest, QuoteLineRequest } from '../api/quoteService';

interface ProductOption {
  id: string;
  shortName: string;
  isActive: boolean;
}

interface LineRow {
  productId: string;
  quantity: string;
  unitPrice: string;
  taxRatePct: string;
}

const emptyLine = (): LineRow => ({
  productId: '',
  quantity: '',
  unitPrice: '',
  taxRatePct: '15',
});

function defaultValidUntil(): string {
  const d = new Date();
  d.setDate(d.getDate() + 30);
  return d.toISOString().slice(0, 10);
}

export function CreateQuotePage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const navigate = useNavigate();
  const canCreate = canShow('sales.quotes.create');

  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);
  const [businessPartnerId, setBusinessPartnerId] = useState('');
  const [customers, setCustomers] = useState<Awaited<ReturnType<typeof businessPartnerFacade.searchCustomersForPicker>>>([]);
  const [products, setProducts] = useState<ProductOption[]>([]);
  const [validUntil, setValidUntil] = useState(defaultValidUntil());
  const [paymentTermDays, setPaymentTermDays] = useState('0');
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<LineRow[]>([emptyLine()]);
  const [localError, setLocalError] = useState<string | null>(null);

  const loadPickers = useCallback(() => {
    setBusinessPartnerId('');
    void businessPartnerFacade.searchCustomersForPicker().then(setCustomers);
    void api
      .get<ApiResponse<ProductOption[]>>('/api/inventory/products')
      .then((r) => setProducts((r.data.responseObject ?? []).filter((p) => p.isActive)));
  }, []);

  useEffect(() => {
    loadPickers();
  }, [loadPickers, companySessionVersion]);

  const { loading, error, create } = useQuoteActions();

  const updateLine = (idx: number, field: keyof LineRow, value: string) =>
    setLines((prev) => prev.map((line, i) => (i === idx ? { ...line, [field]: value } : line)));

  const total = lines.reduce((sum, line) => {
    const qty = parseFloat(line.quantity) || 0;
    const price = parseFloat(line.unitPrice) || 0;
    const tax = parseFloat(line.taxRatePct) || 0;
    const sub = qty * price;
    return sum + sub + sub * (tax / 100);
  }, 0);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLocalError(null);
    if (!businessPartnerId) return setLocalError(t('ventas.cotizaciones.form.customerRequired'));
    if (!validUntil) return setLocalError(t('ventas.cotizaciones.form.validUntilRequired'));
    if (lines.some((line) => !line.productId || !line.quantity || !line.unitPrice)) {
      return setLocalError(t('ventas.cotizaciones.form.linesIncomplete'));
    }

    const parsedLines: QuoteLineRequest[] = lines.map((line) => ({
      productId: line.productId,
      quantity: parseFloat(line.quantity),
      unitPrice: parseFloat(line.unitPrice),
      taxRatePct: parseFloat(line.taxRatePct) || 0,
    }));

    const payload: CreateQuoteRequest = {
      businessPartnerId,
      validUntil,
      paymentTermDays: parseInt(paymentTermDays, 10) || 0,
      notes: notes.trim() || null,
      lines: parsedLines,
    };

    const created = await create(payload);
    if (created && typeof created === 'object' && 'publicId' in created) {
      navigate(`/sales/quotes/${(created as { publicId: string }).publicId}`);
    }
  };

  if (!canCreate) return <NoAccessPage title={t('ventas.cotizaciones.newQuote')} />;

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.sales')}
      title={t('ventas.cotizaciones.newQuote')}
      subtitle={t('ventas.cotizaciones.newQuoteSubtitle')}
      action={
        <ZHBtn variant="ghost" size="md" onClick={() => navigate('/sales/quotes')}>
          {t('common.back')}
        </ZHBtn>
      }
    >
      {(localError || error) && (
        <ZHPageNotice variant="error" message={t('common.error')} detail={localError ?? error ?? ''} />
      )}

      <form onSubmit={(e) => void handleSubmit(e)}>
        <div className="pg-section pg-section--mb-4">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">info</span>
              <span className="pg-section-label">{t('ventas.cotizaciones.form.general')}</span>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label={t('ventas.cotizaciones.form.customer')} required>
                <select
                  className="zh-input"
                  value={businessPartnerId}
                  onChange={(e) => setBusinessPartnerId(e.target.value)}
                  required
                >
                  <option value="">{t('ventas.cotizaciones.form.selectCustomer')}</option>
                  {customers
                    .filter((c) => c.pickerMeta.selectable && c.pickerMeta.businessPartnerId)
                    .map((c) => (
                      <option key={c.id} value={c.pickerMeta.businessPartnerId!}>
                        {c.fullName}
                      </option>
                    ))}
                </select>
              </ZHField>
              <ZHField label={t('ventas.cotizaciones.form.validUntil')} required>
                <input
                  className="zh-input"
                  type="date"
                  value={validUntil}
                  onChange={(e) => setValidUntil(e.target.value)}
                  required
                />
              </ZHField>
              <ZHField label={t('ventas.cotizaciones.form.paymentTermDays')}>
                <ZhNumberInput
                  className="zh-input"
                  positiveOnly
                  value={paymentTermDays}
                  onChange={(e) => setPaymentTermDays(e.target.value)}
                />
              </ZHField>
              <div className="pg-form-grid-span-2">
                <ZHField label={t('ventas.cotizaciones.form.notes')}>
                  <textarea
                    className="zh-input"
                    rows={2}
                    value={notes}
                    onChange={(e) => setNotes(e.target.value)}
                  />
                </ZHField>
              </div>
            </div>
          </div>
        </div>

        <div className="pg-section pg-section--mb-4">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">list_alt</span>
              <span className="pg-section-label">{t('ventas.cotizaciones.form.lines')}</span>
            </div>
            <ZHBtn variant="secondary" size="sm" type="button" onClick={() => setLines((prev) => [...prev, emptyLine()])}>
              {t('ventas.cotizaciones.form.addLine')}
            </ZHBtn>
          </div>
          <div className="pg-section-body pg-overflow-x">
            <table className="table">
              <thead>
                <tr>
                  <th>{t('ventas.cotizaciones.form.product')}</th>
                  <th className="pg-th-right">{t('ventas.cotizaciones.form.quantity')}</th>
                  <th className="pg-th-right">{t('ventas.cotizaciones.form.unitPrice')}</th>
                  <th className="pg-th-right">{t('ventas.cotizaciones.form.taxPct')}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {lines.map((line, idx) => (
                  <tr key={idx}>
                    <td>
                      <select
                        className="zh-input"
                        value={line.productId}
                        onChange={(e) => updateLine(idx, 'productId', e.target.value)}
                        required
                      >
                        <option value="">{t('ventas.cotizaciones.form.selectProduct')}</option>
                        {products.map((p) => (
                          <option key={p.id} value={p.id}>
                            {p.shortName}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <ZhDecimalInput
                        className="zh-input pg-input-right"
                        decimals={4}
                        positiveOnly
                        value={line.quantity}
                        onChange={(e) => updateLine(idx, 'quantity', e.target.value)}
                        required
                      />
                    </td>
                    <td>
                      <ZhDecimalInput
                        className="zh-input pg-input-right"
                        decimals={4}
                        positiveOnly
                        value={line.unitPrice}
                        onChange={(e) => updateLine(idx, 'unitPrice', e.target.value)}
                        required
                      />
                    </td>
                    <td>
                      <ZhDecimalInput
                        className="zh-input pg-input-right"
                        decimals={4}
                        positiveOnly
                        value={line.taxRatePct}
                        onChange={(e) => updateLine(idx, 'taxRatePct', e.target.value)}
                      />
                    </td>
                    <td>
                      {lines.length > 1 && (
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          type="button"
                          onClick={() => setLines((prev) => prev.filter((_, i) => i !== idx))}
                        >
                          {t('common.remove')}
                        </ZHBtn>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <p className="pg-total-line mono">
              {t('ventas.cotizaciones.form.estimatedTotal')}: ${total.toFixed(2)}
            </p>
          </div>
        </div>

        <div className="pg-form-actions">
          <ZHBtn variant="primary" size="md" type="submit" disabled={loading}>
            {loading ? t('common.saving') : t('ventas.cotizaciones.form.submit')}
          </ZHBtn>
        </div>
      </form>
    </ErpPageTemplate>
  );
}
