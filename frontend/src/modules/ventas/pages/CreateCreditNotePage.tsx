import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZhDecimalInput } from '../../../components/zh/inputs';
import { useI18n } from '../../../i18n/i18n';
import { useAsync } from '../../../hooks/useAsync';
import { formatApiError } from '../../lib/formatApiError';
import { ventasFacturasService, type VentasFacturaDto } from '../api/ventasFacturasService';
import { creditNotesService } from '../api/creditNotesService';
import { productService } from '../../products/api/productService';
import type { Product } from '../../../types/product';
import './credit-notes-page.css';
import { usePermissionsUi } from '../../../access/usePermissionsUi';

interface NoteLine {
  localId: string;
  productId: string;
  quantity: number;
  unitPrice: number;
}

let _lineCounter = 0;
function newLine(): NoteLine {
  return { localId: `nl-${++_lineCounter}`, productId: '', quantity: 1, unitPrice: 0 };
}

function calcTotals(lines: NoteLine[]) {
  const subtotal = lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0);
  const vat      = subtotal * 0.15;
  return { subtotal, vat, total: subtotal + vat };
}

export function CreateCreditNotePage() {
  const { canShow } = usePermissionsUi();
  const { t }     = useI18n();
  const navigate  = useNavigate();
  const canCreate = canShow('sales.credit-notes.create');

  const [invoiceId, setInvoiceId]   = useState('');
  const [noteType,  setNoteType]    = useState<'CREDITO' | 'DEBITO'>('CREDITO');
  const [reason,    setReason]      = useState('');
  const [lines,     setLines]       = useState<NoteLine[]>([newLine()]);
  const [saving,    setSaving]      = useState(false);
  const [error,     setError]       = useState<string | null>(null);

  const invoicesState  = useAsync(() => ventasFacturasService.list({ pageSize: 500, estado: 'Autorizado' }));
  const productsState  = useAsync(() => productService.getAll() as Promise<Product[]>);

  const authorizedInvoices = useMemo(
    () => invoicesState.data?.items ?? [],
    [invoicesState.data],
  );

  const selectedInvoice = useMemo(
    () => authorizedInvoices.find((i) => i.id === invoiceId),
    [authorizedInvoices, invoiceId],
  );

  const updateLine = (localId: string, field: keyof NoteLine, value: unknown) =>
    setLines((prev) => prev.map((l) => (l.localId === localId ? { ...l, [field]: value } : l)));

  const addLine    = () => setLines((prev) => [...prev, newLine()]);
  const removeLine = (localId: string) =>
    setLines((prev) => (prev.length > 1 ? prev.filter((l) => l.localId !== localId) : prev));

  const onSelectProduct = (localId: string, productId: string) => {
    updateLine(localId, 'productId', productId);
  };

  const totals = useMemo(() => calcTotals(lines), [lines]);

  const handleSubmit = async () => {
    setError(null);
    if (!invoiceId)    { setError('Seleccione la factura original.'); return; }
    if (!reason.trim()) { setError('El motivo es obligatorio.'); return; }
    const validLines = lines.filter((l) => l.productId && l.quantity > 0);
    if (validLines.length === 0) { setError('Agregue al menos un ítem con producto y cantidad mayor a 0.'); return; }

    setSaving(true);
    try {
      await creditNotesService.create({
        originalBillId: invoiceId,
        noteType,
        reason: reason.trim(),
        items: validLines.map((l) => ({
          productId: l.productId,
          quantity:  l.quantity,
          unitPrice: l.unitPrice,
        })),
      });
      navigate('/sales/credit-notes');
    } catch (err) {
      setError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  if (!canCreate) return <NoAccessPage title={t('ventas.notas.form.title')} />;

  const invoiceLabel = (inv: VentasFacturaDto) =>
    `${inv.establecimiento}-${inv.puntoEmision}-${inv.secuencial} · ${inv.clienteNombre}`;

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.sales')}
      title={t('ventas.notas.form.title')}
      subtitle="Sobre una factura autorizada. El comprobante se crea en estado Borrador."
      action={
        <>
          <button className="zh-btn zh-btn--ghost" type="button" onClick={() => navigate('/sales/credit-notes')}>
            Cancelar
          </button>
          <ZHBtn variant="primary" size="md" type="button" disabled={saving} onClick={handleSubmit}>
            {saving ? 'Guardando...' : 'Crear nota'}
          </ZHBtn>
        </>
      }
    >
      {error && <ZHPageNotice variant="error" message="Error" detail={error} />}

      {/* ── Main Form ── */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">info</span>
            <span className="pg-section-label">Datos de la nota</span>
          </div>
        </div>
        <div className="pg-section-body">
          <div className="pg-form-grid pg-form-grid--2">

            {/* Invoice selector */}
            <ZHField label={t('ventas.notas.form.invoice')} required>
              <select
                className="zh-input"
                value={invoiceId}
                onChange={(e) => setInvoiceId(e.target.value)}
                disabled={invoicesState.loading}
              >
                <option value="">{invoicesState.loading ? 'Cargando facturas...' : '— Seleccione una factura —'}</option>
                {authorizedInvoices.map((inv) => (
                  <option key={inv.id} value={inv.id}>{invoiceLabel(inv)}</option>
                ))}
              </select>
            </ZHField>

            {/* Note type */}
            <ZHField label={t('ventas.notas.form.noteType')} required>
              <div className="pg-radio-group">
                <label className="zh-inline-check">
                  <input
                    type="radio"
                    name="noteType"
                    value="CREDITO"
                    checked={noteType === 'CREDITO'}
                    onChange={() => setNoteType('CREDITO')}
                  />
                  {t('ventas.notas.typeCredit')}
                </label>
                <label className="zh-inline-check">
                  <input
                    type="radio"
                    name="noteType"
                    value="DEBITO"
                    checked={noteType === 'DEBITO'}
                    onChange={() => setNoteType('DEBITO')}
                  />
                  {t('ventas.notas.typeDebit')}
                </label>
              </div>
            </ZHField>

            {/* Reason */}
            <div className="pg-form-span-full">
              <ZHField label={t('ventas.notas.form.reason')} required>
                <input
                  className="zh-input"
                  type="text"
                  maxLength={200}
                  placeholder="Ej: Devolución de mercadería defectuosa"
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              </ZHField>
            </div>

            {/* Selected invoice summary */}
            {selectedInvoice && (
              <div className="pg-form-span-full">
                <div className="pg-summary-box">
                  <span><strong>Cliente:</strong> {selectedInvoice.clienteNombre}</span>
                  <span><strong>Total factura:</strong> ${selectedInvoice.total.toFixed(2)}</span>
                  <span><strong>Fecha:</strong> {new Date(selectedInvoice.fechaEmision).toLocaleDateString('es')}</span>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ── Line Items ── */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">list</span>
            <span className="pg-section-label">{t('ventas.notas.form.items')}</span>
          </div>
        </div>
        <div className="pg-section-body">
          <div className="cn-items-scroll">
            <table className="cn-items-table">
              <thead>
                <tr>
                  <th className="cn-th-product">Producto</th>
                  <th className="cn-th-qty">Cantidad</th>
                  <th className="cn-th-price">Precio unit.</th>
                  <th className="cn-th-subtotal">Subtotal</th>
                  <th className="cn-th-actions" aria-label={t('common.actions')} />
                </tr>
              </thead>
              <tbody>
                {lines.map((line) => {
                  const lineTotal = line.quantity * line.unitPrice;
                  return (
                    <tr key={line.localId}>
                      <td>
                        <select
                          className="zh-input"
                          value={line.productId}
                          onChange={(e) => onSelectProduct(line.localId, e.target.value)}
                          disabled={productsState.loading}
                        >
                          <option value="">— Producto —</option>
                          {(productsState.data ?? [])
                            .filter((p) => p.isActive && p.isForSale)
                            .map((p) => (
                              <option key={p.id} value={p.id}>
                                {p.saleCode} · {p.shortName}
                              </option>
                            ))}
                        </select>
                      </td>
                      <td>
                        <ZhDecimalInput
                          className="zh-input"
                          decimals={4}
                          positiveOnly
                          value={line.quantity}
                          onChange={(e) => updateLine(line.localId, 'quantity', parseFloat(e.target.value) || 0)}
                        />
                      </td>
                      <td>
                        <ZhDecimalInput
                          className="zh-input"
                          decimals={4}
                          positiveOnly
                          value={line.unitPrice}
                          onChange={(e) => updateLine(line.localId, 'unitPrice', parseFloat(e.target.value) || 0)}
                        />
                      </td>
                      <td className="cn-cell-subtotal">
                        ${lineTotal.toFixed(2)}
                      </td>
                      <td>
                        <button
                          className="cn-remove-btn"
                          type="button"
                          onClick={() => removeLine(line.localId)}
                          title="Eliminar línea"
                        >
                          <span className="material-symbols-outlined">delete</span>
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <button
            className="zh-btn zh-btn--ghost zh-btn--sm cn-add-row-btn"
            type="button"
            onClick={addLine}
          >
            <span className="material-symbols-outlined">add</span>
            Agregar línea
          </button>

          {/* Totals */}
          <div className="cn-totals-row">
            <div className="cn-total-line">
              <span className="cn-total-label">Subtotal:</span>
              <span className="cn-total-value">${totals.subtotal.toFixed(2)}</span>
            </div>
            <div className="cn-total-line">
              <span className="cn-total-label">IVA 15%:</span>
              <span className="cn-total-value">${totals.vat.toFixed(2)}</span>
            </div>
            <div className="cn-total-line cn-total-grand">
              <span className="cn-total-label">Total:</span>
              <span className="cn-total-value">${totals.total.toFixed(2)}</span>
            </div>
          </div>
        </div>
      </div>

    </ErpPageTemplate>
  );
}
