import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { useCompanyScopedAsync } from '../../../../hooks/useCompanyScopedAsync';
import { businessPartnerFacade } from '../../../masterData/api/businessPartnerFacade';
import { comprasService, type CompraLineaInput } from '../api/comprasService';
import './compras-facturas-page.css';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';

const TODAY = new Date().toISOString().split('T')[0]!;

interface LineaForm {
  _key: number;
  description: string;
  quantity: string;
  unitPrice: string;
  discountPct: string;
  vatPct: string;
}

let _keySeq = 1;
const newLinea = (): LineaForm => ({
  _key: _keySeq++,
  description: '',
  quantity: '1',
  unitPrice: '0.00',
  discountPct: '0',
  vatPct: '15',
});

function calcLinea(l: LineaForm) {
  const qty  = parseFloat(l.quantity)    || 0;
  const up   = parseFloat(l.unitPrice)   || 0;
  const dPct = parseFloat(l.discountPct) || 0;
  const vPct = parseFloat(l.vatPct)      || 0;
  const sub  = qty * up * (1 - dPct / 100);
  const vat  = sub * vPct / 100;
  return { subtotal: sub, vatAmount: vat, total: sub + vat };
}

export function CrearCompraPage() {
  const { canShow } = usePermissionsUi();
  const navigate  = useNavigate();
  const canCreate = canShow('purchases.invoices.create');

  const suppliersState = useCompanyScopedAsync(() => businessPartnerFacade.searchSuppliersForPicker());

  const [supplierId,    setSupplierId]    = useState('');
  const [invoiceNumber, setInvoiceNumber] = useState('');
  const [invoiceDate,   setInvoiceDate]   = useState(TODAY);
  const [dueDate,       setDueDate]       = useState('');
  const [paymentTerms,  setPaymentTerms]  = useState('');
  const [notes,         setNotes]         = useState('');
  const [lineas,        setLineas]        = useState<LineaForm[]>([newLinea()]);
  const [saving,        setSaving]        = useState(false);
  const [error,         setError]         = useState<string | null>(null);

  const totales = useMemo(() => {
    const calcs = lineas.map(calcLinea);
    return {
      subtotal: calcs.reduce((s, c) => s + c.subtotal, 0),
      vat:      calcs.reduce((s, c) => s + c.vatAmount, 0),
      total:    calcs.reduce((s, c) => s + c.total, 0),
    };
  }, [lineas]);

  const updateLinea = (key: number, field: keyof LineaForm, val: string) =>
    setLineas((prev) => prev.map((l) => l._key === key ? { ...l, [field]: val } : l));

  const removeLinea = (key: number) =>
    setLineas((prev) => prev.length > 1 ? prev.filter((l) => l._key !== key) : prev);

  const handleSubmit = async () => {
    setError(null);
    if (!supplierId) { setError('Selecciona un proveedor.'); return; }
    if (!invoiceNumber.trim()) { setError('El número de factura es obligatorio.'); return; }
    if (lineas.some((l) => !l.description.trim())) { setError('Todas las líneas deben tener descripción.'); return; }

    const lines: CompraLineaInput[] = lineas.map((l) => ({
      description: l.description.trim(),
      productCode: null,
      productId: null,
      quantity: parseFloat(l.quantity) || 1,
      unitPrice: parseFloat(l.unitPrice) || 0,
      discountPct: parseFloat(l.discountPct) || 0,
      vatPct: parseFloat(l.vatPct) || 0,
    }));

    setSaving(true);
    try {
      await comprasService.crearManual({
        supplierId,
        invoiceNumber: invoiceNumber.trim(),
        invoiceDate,
        dueDate: dueDate || null,
        paymentTerms: paymentTerms.trim() || null,
        notes: notes.trim() || null,
        lines,
      });
      navigate('/compras/facturas');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al guardar la compra.');
    } finally {
      setSaving(false);
    }
  };

  if (!canCreate) return <NoAccessPage title="Nueva Factura de Compra" />;

  return (
    <ErpPageTemplate
      kicker="Compras"
      title="Nueva Factura de Compra"
      action={
        <>
          <button className="zh-btn zh-btn--ghost zh-btn--md" type="button" onClick={() => navigate('/compras/facturas')}>
            Cancelar
          </button>
          <ZHBtn variant="primary" size="md" onClick={() => void handleSubmit()} disabled={saving}>
            {saving ? 'Guardando...' : 'Guardar Compra'}
          </ZHBtn>
        </>
      }
    >
      {error && <ZHPageNotice variant="error" message={error} />}

      {/* Cabecera del documento */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">info</span>
            <span className="pg-section-label">Datos de la Factura</span>
          </div>
        </div>
        <div className="pg-section-body">
          <div className="pg-form-grid pg-form-grid--3">
            <ZHField label="Proveedor" required>
              <select
                className="zh-input"
                value={supplierId}
                onChange={(e) => setSupplierId(e.target.value)}
                disabled={suppliersState.loading}
              >
                <option value="">-- Seleccionar proveedor --</option>
                {(suppliersState.data ?? []).map((s) => (
                  <option
                    key={s.pickerMeta.businessPartnerId}
                    value={s.pickerMeta.legacyOperationalId ?? ''}
                    disabled={!s.pickerMeta.selectable}
                  >
                    {s.legalName} ({s.taxId})
                    {!s.pickerMeta.selectable ? ' — sin vínculo operacional' : ''}
                  </option>
                ))}
              </select>
            </ZHField>

            <ZHField label="Nº Factura" required>
              <input
                className="zh-input"
                value={invoiceNumber}
                onChange={(e) => setInvoiceNumber(e.target.value)}
                placeholder="001-001-000000123"
              />
            </ZHField>

            <ZHField label="Fecha de Emisión" required>
              <input className="zh-input" type="date" value={invoiceDate} onChange={(e) => setInvoiceDate(e.target.value)} />
            </ZHField>

            <ZHField label="Fecha de Vencimiento">
              <input className="zh-input" type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
            </ZHField>

            <ZHField label="Condiciones de Pago">
              <input className="zh-input" value={paymentTerms} onChange={(e) => setPaymentTerms(e.target.value)} placeholder="Ej: 30 días" />
            </ZHField>

            <ZHField label="Observaciones">
              <input className="zh-input" value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Opcional" />
            </ZHField>
          </div>
        </div>
      </div>

      {/* Líneas de detalle */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">list</span>
            <span className="pg-section-label">Líneas de Detalle</span>
          </div>
          <button
            className="zh-btn zh-btn--ghost zh-btn--sm"
            type="button"
            onClick={() => setLineas((prev) => [...prev, newLinea()])}
          >
            <span className="material-symbols-outlined">add</span>
            Agregar línea
          </button>
        </div>
        <div className="pg-section-body pg-overflow-x">
          <table className="cf-line-table">
            <thead>
              <tr>
                <th className="cf-th-desc">Descripción</th>
                <th className="cf-th-qty">Cantidad</th>
                <th className="cf-th-price">Precio Unit.</th>
                <th className="cf-th-pct">Desc %</th>
                <th className="cf-th-pct">IVA %</th>
                <th className="cf-th-subtotal">Subtotal</th>
                <th className="cf-th-actions" aria-label="Acciones" />
              </tr>
            </thead>
            <tbody>
              {lineas.map((l) => {
                const { subtotal } = calcLinea(l);
                return (
                  <tr key={l._key}>
                    <td>
                      <input
                        className="zh-input"
                        value={l.description}
                        onChange={(e) => updateLinea(l._key, 'description', e.target.value)}
                        placeholder="Descripción del producto/servicio"
                      />
                    </td>
                    <td className="cf-td-num">
                      <input className="zh-input" type="number" min="0.001" step="any" value={l.quantity} onChange={(e) => updateLinea(l._key, 'quantity', e.target.value)} />
                    </td>
                    <td className="cf-td-num">
                      <input className="zh-input" type="number" min="0" step="0.01" value={l.unitPrice} onChange={(e) => updateLinea(l._key, 'unitPrice', e.target.value)} />
                    </td>
                    <td className="cf-td-num">
                      <input className="zh-input" type="number" min="0" max="100" step="any" value={l.discountPct} onChange={(e) => updateLinea(l._key, 'discountPct', e.target.value)} />
                    </td>
                    <td className="cf-td-num">
                      <select className="zh-input" value={l.vatPct} onChange={(e) => updateLinea(l._key, 'vatPct', e.target.value)}>
                        <option value="0">0%</option>
                        <option value="5">5%</option>
                        <option value="15">15%</option>
                      </select>
                    </td>
                    <td className="cf-td-total">${subtotal.toFixed(2)}</td>
                    <td>
                      <button
                        className="zh-btn zh-btn--ghost zh-btn--sm pg-btn-error-ghost"
                        type="button"
                        onClick={() => removeLinea(l._key)}
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

          {/* Totales */}
          <div className="cf-totals-box">
            <div className="cf-totals-row">
              <strong>Subtotal:</strong>
              <span>${totales.subtotal.toFixed(2)}</span>
            </div>
            <div className="cf-totals-row">
              <strong>IVA:</strong>
              <span>${totales.vat.toFixed(2)}</span>
            </div>
            <div className="cf-totals-row cf-total-final">
              <strong>TOTAL:</strong>
              <span>${totales.total.toFixed(2)}</span>
            </div>
          </div>
        </div>
      </div>
    </ErpPageTemplate>
  );
}
