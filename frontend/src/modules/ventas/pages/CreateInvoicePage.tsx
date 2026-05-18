import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { useAuthStore } from '../../../store/authStore';
import { useI18n } from '../../../i18n/i18n';
import { useAsync } from '../../../hooks/useAsync';
import { customerService } from '../../../modules/customers/api/customerService';
import { bodegaService } from '../../../services/bodegaService';
import { formatApiError } from '../../lib/formatApiError';
import { ventasFacturasService } from '../api/ventasFacturasService';
import {
  calcLineAmounts,
  calcInvoiceTotals,
  emptyInvoiceLine,
  newInvoiceLineId,
  type InvoiceLineValues,
} from '../schemas/createInvoiceSchema';

const TODAY = new Date().toISOString().split('T')[0]!;
const VAT_RATE = 0.15; // IVA Ecuador 15%

export function CreateInvoicePage() {
  const { t } = useI18n();
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const role      = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin   = role === 'Admin' || role === 'SuperAdmin';
  const canCreate = isAdmin || hasPerm('sales.invoices.create');

  const [customerId,    setCustomerId]    = useState('');
  const [customerRuc,   setCustomerRuc]   = useState('');
  const [customerAddr,  setCustomerAddr]  = useState('');
  const [customerEmail, setCustomerEmail] = useState('');
  const [warehouseId,   setWarehouseId]   = useState('');
  const [issueDate,     setIssueDate]     = useState(TODAY);
  const [currency,      setCurrency]      = useState('USD');
  const [notes,         setNotes]         = useState('');
  const [lines,         setLines]         = useState<InvoiceLineValues[]>([emptyInvoiceLine()]);
  const [clientSearch,  setClientSearch]  = useState('');

  const [saving,  setSaving]  = useState(false);
  const [error,   setError]   = useState<string | null>(null);
  const [draftId, setDraftId] = useState<string | null>(null);

  const clientsState    = useAsync(() => customerService.getAll());
  const warehousesState = useAsync(() => bodegaService.list('active'));

  useEffect(() => {
    const bodegas = warehousesState.data;
    if (!warehouseId && bodegas && bodegas.length > 0) {
      setWarehouseId(bodegas[0]!.id);
    }
  }, [warehousesState.data, warehouseId]);

  const filteredClients = useMemo(() => {
    const q = clientSearch.trim().toLowerCase();
    if (!q) return clientsState.data ?? [];
    return (clientsState.data ?? []).filter(
      (c) =>
        c.fullName.toLowerCase().includes(q) ||
        c.identificationNumber.toLowerCase().includes(q)
    );
  }, [clientsState.data, clientSearch]);

  const totals = useMemo(() => calcInvoiceTotals(lines), [lines]);

  const handleSelectClient = (id: string) => {
    const c = (clientsState.data ?? []).find((x) => x.id === id);
    if (!c) return;
    setCustomerId(c.id);
    setCustomerRuc(c.identificationNumber);
    setCustomerAddr(c.address ?? '');
    setCustomerEmail(c.email ?? '');
    setClientSearch(c.fullName);
  };

  const updateLine = (localId: string, field: keyof InvoiceLineValues, value: unknown) => {
    setLines((prev) =>
      prev.map((l) => (l.localId === localId ? { ...l, [field]: value } : l))
    );
  };

  const addLine = () => setLines((prev) => [...prev, emptyInvoiceLine()]);

  const removeLine = (localId: string) =>
    setLines((prev) => (prev.length > 1 ? prev.filter((l) => l.localId !== localId) : prev));

  const handleSaveDraft = async () => {
    setError(null);
    if (!customerId)  { setError('Seleccione un cliente.'); return; }
    if (!warehouseId) { setError('Seleccione una bodega.');  return; }
    const validLines = lines.filter((l) => l.description.trim() && l.quantity > 0);
    if (validLines.length === 0) { setError('Agregue al menos un ítem con cantidad mayor a 0.'); return; }

    const warehouse = (warehousesState.data ?? []).find((w) => w.id === warehouseId);
    if (!warehouse) { setError('Bodega no encontrada.'); return; }

    setSaving(true);
    try {
      const id = await ventasFacturasService.create({
        customerId,
        warehouseId,
        branchId: warehouse.branchId,
        items: validLines
          .filter((l) => l.productId)
          .map((l) => ({
            productId: l.productId!,
            quantity:  l.quantity,
            unitPrice: l.unitPrice,
          })),
      });
      setDraftId(id);
    } catch (err) {
      setError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const handleIssue = async () => {
    setError(null);
    setSaving(true);
    try {
      let id = draftId;
      if (!id) {
        if (!customerId)  { setError('Seleccione un cliente.'); setSaving(false); return; }
        if (!warehouseId) { setError('Seleccione una bodega.');  setSaving(false); return; }
        const warehouse = (warehousesState.data ?? []).find((w) => w.id === warehouseId);
        if (!warehouse) { setError('Bodega no encontrada.'); setSaving(false); return; }
        const validLines = lines.filter((l) => l.description.trim() && l.quantity > 0 && l.productId);
        if (validLines.length === 0) { setError('Agregue al menos un ítem con producto seleccionado.'); setSaving(false); return; }
        id = await ventasFacturasService.create({
          customerId, warehouseId,
          branchId: warehouse.branchId,
          items: validLines.map((l) => ({ productId: l.productId!, quantity: l.quantity, unitPrice: l.unitPrice })),
        });
      }
      await ventasFacturasService.validate(id);
      await ventasFacturasService.issue(id);
      navigate('/ventas/facturas');
    } catch (err) {
      setError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  if (!canCreate) return <NoAccessPage title="Nueva Factura" />;

  const folio = draftId ? 'Borrador guardado' : 'FAC-PENDIENTE';

  return (
    <div className="pg-page">

      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Ruta de navegación">
            <span className="pg-breadcrumb-item">Ventas</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Nueva Factura</span>
          </nav>
          <h1 className="pg-title">Nueva Factura</h1>
          <p className="pg-subtitle">Generación de comprobante fiscal electrónico</p>
        </div>

        <div className="pg-header-fields">
          <div className="pg-header-field">
            <label className="pg-header-field-label">Folio</label>
            <input
              className="zh-input mono"
              readOnly
              value={folio}
              style={{ color: 'var(--color-primary)', fontWeight: 500 }}
            />
          </div>
          <div className="pg-header-field">
            <label className="pg-header-field-label">Fecha de Emisión</label>
            <input
              className="zh-input"
              type="date"
              value={issueDate}
              onChange={(e) => setIssueDate(e.target.value)}
            />
          </div>
          <div className="pg-header-field">
            <label className="pg-header-field-label">Moneda</label>
            <select className="zh-input" value={currency} onChange={(e) => setCurrency(e.target.value)}>
              <option value="USD">USD — Dólar</option>
              <option value="EUR">EUR — Euro</option>
            </select>
          </div>
        </div>
      </div>

      {error   && <ZHPageNotice variant="error"   message={t('common.errorPrefix')} detail={error} />}
      {draftId && <ZHPageNotice variant="success" message="Borrador guardado correctamente." />}

      {/* ── Customer section ── */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">person_search</span>
            <p className="pg-section-label">Selección de Cliente</p>
          </div>
        </div>
        <div className="pg-section-body">
          <div className="pg-form-grid pg-form-grid--4">

            <ZHField label="Buscar Cliente" required>
              <div className="pg-search">
                <span className="material-symbols-outlined">search</span>
                <input
                  className="zh-input"
                  type="search"
                  placeholder="Nombre o RUC…"
                  value={clientSearch}
                  onChange={(e) => { setClientSearch(e.target.value); setCustomerId(''); }}
                  list="client-list"
                />
                <datalist id="client-list">
                  {filteredClients.map((c) => (
                    <option
                      key={c.id}
                      value={c.fullName}
                      onClick={() => handleSelectClient(c.id)}
                    />
                  ))}
                </datalist>
              </div>
              {clientSearch && !customerId && filteredClients.length > 0 && (
                <div style={{
                  position: 'absolute', zIndex: 100, background: 'var(--color-surface)',
                  border: '1px solid var(--color-border)', borderRadius: 'var(--radius-md)',
                  boxShadow: 'var(--shadow-md)', width: '100%', maxHeight: 200, overflowY: 'auto',
                }}>
                  {filteredClients.slice(0, 8).map((c) => (
                    <button
                      key={c.id}
                      type="button"
                      style={{
                        display: 'block', width: '100%', textAlign: 'left',
                        padding: '8px 12px', background: 'none', border: 'none',
                        cursor: 'pointer', fontSize: 'var(--text-body-md-size)',
                      }}
                      onMouseDown={() => handleSelectClient(c.id)}
                    >
                      <strong>{c.fullName}</strong>
                      <span style={{ color: 'var(--color-text-secondary)', marginLeft: 8, fontSize: 12 }}>
                        {c.identificationNumber}
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </ZHField>

            <ZHField label="RUC / Cédula">
              <input className="zh-input mono" readOnly value={customerRuc} placeholder="20601234567" />
            </ZHField>

            <ZHField label="Dirección">
              <input
                className="zh-input"
                value={customerAddr}
                onChange={(e) => setCustomerAddr(e.target.value)}
                placeholder="Dirección fiscal"
              />
            </ZHField>

            <ZHField label="Correo">
              <input
                className="zh-input"
                type="email"
                value={customerEmail}
                onChange={(e) => setCustomerEmail(e.target.value)}
                placeholder="cliente@empresa.com"
              />
            </ZHField>
          </div>

          <div className="pg-form-grid pg-form-grid--4" style={{ marginTop: 'var(--space-4)' }}>
            <ZHField label="Bodega" required>
              <select
                className="zh-input"
                value={warehouseId}
                onChange={(e) => setWarehouseId(e.target.value)}
              >
                <option value="">— seleccionar —</option>
                {(warehousesState.data ?? []).map((w) => (
                  <option key={w.id} value={w.id}>{w.name}</option>
                ))}
              </select>
            </ZHField>
          </div>
        </div>
      </div>

      {/* ── Line items section ── */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">list_alt</span>
            <p className="pg-section-label">Detalle de Ítems</p>
          </div>
          <ZHBtn variant="ghost" size="sm" type="button" onClick={addLine}>
            <span className="material-symbols-outlined">add</span>
            Agregar Ítem
          </ZHBtn>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table className="table">
            <thead>
              <tr>
                <th style={{ width: 100 }}>SKU / Código</th>
                <th>Descripción</th>
                <th style={{ width: 90, textAlign: 'right' }}>Cant.</th>
                <th style={{ width: 120, textAlign: 'right' }}>P. Unitario</th>
                <th style={{ width: 110, textAlign: 'right' }}>Descuento</th>
                <th style={{ width: 120, textAlign: 'right' }}>IVA (15%)</th>
                <th style={{ width: 120, textAlign: 'right' }}>Total</th>
                <th style={{ width: 48 }} />
              </tr>
            </thead>
            <tbody>
              {lines.map((line) => {
                const { vatAmount, total } = calcLineAmounts(line);
                return (
                  <tr key={line.localId}>
                    <td>
                      <input
                        className="pg-editable-input pg-editable-input--mono"
                        placeholder="ZH-001"
                        value={line.sku}
                        onChange={(e) => updateLine(line.localId, 'sku', e.target.value)}
                      />
                    </td>
                    <td>
                      <input
                        className="pg-editable-input"
                        placeholder="Descripción del producto o servicio"
                        value={line.description}
                        onChange={(e) => updateLine(line.localId, 'description', e.target.value)}
                      />
                    </td>
                    <td>
                      <input
                        className="pg-editable-input pg-editable-input--right"
                        type="number"
                        min={0.01}
                        step={0.01}
                        value={line.quantity}
                        onChange={(e) => updateLine(line.localId, 'quantity', parseFloat(e.target.value) || 0)}
                      />
                    </td>
                    <td>
                      <input
                        className="pg-editable-input pg-editable-input--right"
                        type="number"
                        min={0}
                        step={0.01}
                        value={line.unitPrice}
                        onChange={(e) => updateLine(line.localId, 'unitPrice', parseFloat(e.target.value) || 0)}
                      />
                    </td>
                    <td style={{ color: 'var(--color-error)', textAlign: 'right', fontSize: 'var(--text-body-md-size)' }}>
                      <input
                        className="pg-editable-input pg-editable-input--right"
                        type="number"
                        min={0}
                        step={0.01}
                        value={line.discountAmount}
                        onChange={(e) => updateLine(line.localId, 'discountAmount', parseFloat(e.target.value) || 0)}
                        style={{ color: 'var(--color-error)' }}
                      />
                    </td>
                    <td style={{ textAlign: 'right', color: 'var(--color-text-secondary)', fontSize: 'var(--text-body-md-size)' }}>
                      {vatAmount.toFixed(2)}
                    </td>
                    <td style={{ textAlign: 'right', fontWeight: 600, fontSize: 'var(--text-body-md-size)' }}>
                      {total.toFixed(2)}
                    </td>
                    <td style={{ textAlign: 'center' }}>
                      <button
                        type="button"
                        className="pg-row-delete"
                        onClick={() => removeLine(line.localId)}
                        aria-label="Eliminar línea"
                        title="Eliminar"
                      >
                        <span className="material-symbols-outlined" style={{ fontSize: 18 }}>delete</span>
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          <button type="button" className="pg-add-row" onClick={addLine}>
            <span className="material-symbols-outlined">add_circle</span>
            Haz clic para añadir una nueva línea de producto o servicio
          </button>
        </div>
      </div>

      {/* ── Notes + Totals ── */}
      <div style={{ display: 'grid', gridTemplateColumns: '7fr 5fr', gap: 'var(--space-4)', alignItems: 'start' }}>

        <div className="pg-section">
          <div className="pg-section-body">
            <ZHField label="Notas Adicionales">
              <textarea
                className="zh-input"
                rows={5}
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="Condiciones de pago, observaciones, etc."
                style={{ resize: 'none' }}
              />
            </ZHField>
          </div>
        </div>

        <div className="pg-section">
          <div className="pg-section-body">
            <div className="pg-totals">
              <div className="pg-totals-row">
                <span className="pg-totals-label">Subtotal</span>
                <span className="pg-totals-value">{currency} {totals.subtotal.toFixed(2)}</span>
              </div>
              <div className="pg-totals-row">
                <span className="pg-totals-label">IVA ({(VAT_RATE * 100).toFixed(0)}%)</span>
                <span className="pg-totals-value">{currency} {totals.vat.toFixed(2)}</span>
              </div>
              {totals.discount > 0 && (
                <div className="pg-totals-row">
                  <span className="pg-totals-label">Descuentos</span>
                  <span className="pg-totals-value--discount">− {currency} {totals.discount.toFixed(2)}</span>
                </div>
              )}
              <div className="pg-totals-sep" />
              <div className="pg-totals-grand">
                <div className="pg-totals-grand-meta">
                  <span className="pg-totals-grand-label">Gran Total</span>
                  <span className="pg-totals-grand-currency">{currency}</span>
                </div>
                <span className="pg-totals-grand-amount">{totals.total.toFixed(2)}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* ── Actions bar ── */}
      <div className="pg-actions-bar">
        <div className="pg-actions-info">
          <span className="material-symbols-outlined">info</span>
          Los comprobantes emitidos son definitivos y se envían a la autoridad tributaria (SRI Ecuador).
        </div>
        <div className="pg-actions-buttons">
          <ZHBtn
            variant="ghost"
            size="md"
            type="button"
            disabled={saving}
            onClick={() => navigate('/ventas/facturas')}
          >
            Cancelar
          </ZHBtn>
          <ZHBtn
            variant="ghost"
            size="md"
            type="button"
            disabled={saving}
            onClick={() => void handleSaveDraft()}
          >
            <span className="material-symbols-outlined">save</span>
            {saving ? t('common.saving') : 'Guardar Borrador'}
          </ZHBtn>
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            disabled={saving}
            onClick={() => void handleIssue()}
          >
            <span className="material-symbols-outlined">send</span>
            {saving ? 'Emitiendo…' : 'Emitir Factura'}
          </ZHBtn>
        </div>
      </div>

    </div>
  );
}
