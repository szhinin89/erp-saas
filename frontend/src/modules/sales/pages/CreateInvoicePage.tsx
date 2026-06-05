import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZhDecimalInput, ZhDateInput } from '../../../components/zh/inputs';
import { useI18n } from '../../../i18n/i18n';
import { useCompanyScopedAsync } from '../../../hooks/useCompanyScopedAsync';
import { businessPartnerFacade } from '../../masterData/api/businessPartnerFacade';
import { warehouseService, type WarehouseDto } from '../../inventario/warehouses/api/warehouseService';
import { formatApiError } from '../../lib/formatApiError';
import { salesInvoicesService } from '../api/salesInvoicesService';
import {
  calcLineAmounts,
  calcInvoiceTotals,
  emptyInvoiceLine,
  type InvoiceLineValues,
} from '../schemas/createInvoiceSchema';
import './create-invoice-page.css';
import { useAuthStore } from '../../../store/authStore';
import { usePermissionsUi } from '../../../access/usePermissionsUi';

const TODAY = new Date().toISOString().split('T')[0]!;
const VAT_RATE = 0.15; // IVA Ecuador 15%

export function CreateInvoicePage() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const navigate  = useNavigate();
  const canCreate = canShow('sales.invoices.create');

  
  const [businessPartnerId, setBusinessPartnerId] = useState<string | null>(null);
  const [customerRuc,       setCustomerRuc]       = useState('');
  const [customerAddr,      setCustomerAddr]      = useState('');
  const [customerEmail,     setCustomerEmail]     = useState('');
  const [warehouseId,   setWarehouseId]   = useState('');
  const [issueDate,     setIssueDate]     = useState(TODAY);
  const [currency,      setCurrency]      = useState('USD');
  const [notes,         setNotes]         = useState('');
  const [lines,         setLines]         = useState<InvoiceLineValues[]>([emptyInvoiceLine()]);
  const [clientSearch,  setClientSearch]  = useState('');

  const [saving,  setSaving]  = useState(false);
  const [error,   setError]   = useState<string | null>(null);
  const [draftId, setDraftId] = useState<string | null>(null);

  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);
  const clientsState    = useCompanyScopedAsync(() => businessPartnerFacade.searchCustomersForPicker());
  const warehousesState = useCompanyScopedAsync<WarehouseDto[]>(() => warehouseService.list('active'));

  useEffect(() => {
    setCustomerId('');
    setBusinessPartnerId(null);
    setCustomerRuc('');
    setCustomerAddr('');
    setCustomerEmail('');
    setClientSearch('');
  }, [companySessionVersion]);

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
    setBusinessPartnerId(c.id);  // c.id IS the businessPartnerId in V2
    setCustomerRuc(c.identificationNumber);
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
    if (!businessPartnerId) { setError('Seleccione un cliente.'); return; }
    if (!warehouseId) { setError('Seleccione una bodega.');  return; }
    const validLines = lines.filter((l) => l.description.trim() && l.quantity > 0);
    if (validLines.length === 0) { setError('Agregue al menos un ítem con cantidad mayor a 0.'); return; }

    const warehouse = (warehousesState.data ?? []).find((w) => w.id === warehouseId);
    if (!warehouse) { setError('Bodega no encontrada.'); return; }

    setSaving(true);
    try {
      const id = await salesInvoicesService.create({
        businessPartnerId: businessPartnerId!,
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
        if (!businessPartnerId) { setError('Seleccione un cliente.'); setSaving(false); return; }
        if (!warehouseId) { setError('Seleccione una bodega.');  setSaving(false); return; }
        const warehouse = (warehousesState.data ?? []).find((w) => w.id === warehouseId);
        if (!warehouse) { setError('Bodega no encontrada.'); setSaving(false); return; }
        const validLines = lines.filter((l) => l.description.trim() && l.quantity > 0 && l.productId);
        if (validLines.length === 0) { setError('Agregue al menos un ítem con producto seleccionado.'); setSaving(false); return; }
        id = await salesInvoicesService.create({
          businessPartnerId: businessPartnerId!, warehouseId,
          branchId: warehouse.branchId,
          items: validLines.map((l) => ({ productId: l.productId!, quantity: l.quantity, unitPrice: l.unitPrice })),
        });
      }
      await salesInvoicesService.validate(id);
      await salesInvoicesService.issue(id);
      navigate(`/sales/invoices/${id}`);
    } catch (err) {
      setError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  if (!canCreate) return <NoAccessPage title="Nueva Factura" />;

  const folio = draftId ? 'Borrador guardado' : 'FAC-PENDIENTE';

  return (
    <ErpPageTemplate
      kicker="Ventas"
      title="Nueva Factura"
      subtitle="Generación de comprobante fiscal electrónico"
    >
      <div className="pg-header-fields">
        <div className="pg-header-field">
          <label className="pg-header-field-label">Folio</label>
          <input
            className="zh-input mono vf-folio-readonly"
            readOnly
            value={folio}
          />
        </div>
        <div className="pg-header-field">
          <label className="pg-header-field-label">Fecha de Emisión</label>
          <ZhDateInput
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
              <div className="vf-create-client-search-wrap">
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
                {clientSearch && !businessPartnerId && filteredClients.length > 0 && (
                  <div className="vf-create-client-dropdown" role="listbox">
                    {filteredClients.slice(0, 8).map((c) => (
                      <button
                        key={c.id}
                        type="button"
                        className="vf-create-client-option"
                        role="option"
                        onMouseDown={() => handleSelectClient(c.id)}
                      >
                        <strong>{c.fullName}</strong>
                        <span className="vf-create-client-option-id">
                          {c.identificationNumber}
                          
                        </span>
                      </button>
                    ))}
                  </div>
                )}
              </div>
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

          <div className="pg-form-grid pg-form-grid--4 vf-create-form-grid-offset">
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

        <div className="vf-create-line-items-wrap">
          <table className="table vf-create-line-items-table">
            <thead>
              <tr>
                <th className="vf-create-th vf-create-th--sku">SKU / Código</th>
                <th className="vf-create-th">Descripción</th>
                <th className="vf-create-th vf-create-th--qty">Cant.</th>
                <th className="vf-create-th vf-create-th--price">P. Unitario</th>
                <th className="vf-create-th vf-create-th--discount">Descuento</th>
                <th className="vf-create-th vf-create-th--vat">IVA (15%)</th>
                <th className="vf-create-th vf-create-th--total">Total</th>
                <th className="vf-create-th vf-create-th--actions" aria-label="Acciones" />
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
                      <ZhDecimalInput
                        className="pg-editable-input pg-editable-input--right"
                        decimals={4}
                        positiveOnly
                        value={line.quantity}
                        onChange={(e) => updateLine(line.localId, 'quantity', parseFloat(e.target.value) || 0)}
                      />
                    </td>
                    <td>
                      <ZhDecimalInput
                        className="pg-editable-input pg-editable-input--right"
                        decimals={4}
                        positiveOnly
                        value={line.unitPrice}
                        onChange={(e) => updateLine(line.localId, 'unitPrice', parseFloat(e.target.value) || 0)}
                      />
                    </td>
                    <td className="vf-create-cell vf-create-cell--discount">
                      <ZhDecimalInput
                        className="pg-editable-input pg-editable-input--right vf-create-input--discount"
                        decimals={2}
                        positiveOnly
                        value={line.discountAmount}
                        onChange={(e) => updateLine(line.localId, 'discountAmount', parseFloat(e.target.value) || 0)}
                      />
                    </td>
                    <td className="vf-create-cell vf-create-cell--vat">
                      {vatAmount.toFixed(2)}
                    </td>
                    <td className="vf-create-cell vf-create-cell--line-total">
                      {total.toFixed(2)}
                    </td>
                    <td className="vf-create-cell vf-create-cell--actions">
                      <button
                        type="button"
                        className="pg-row-delete"
                        onClick={() => removeLine(line.localId)}
                        aria-label="Eliminar línea"
                        title="Eliminar"
                      >
                        <span className="material-symbols-outlined vf-create-icon--sm">delete</span>
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
      <div className="vf-create-summary-grid">

        <div className="pg-section vf-create-summary-notes">
          <div className="pg-section-body">
            <ZHField label="Notas Adicionales">
              <textarea
                className="zh-input vf-create-notes-input"
                rows={5}
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="Condiciones de pago, notes, etc."
              />
            </ZHField>
          </div>
        </div>

        <div className="pg-section vf-create-summary-totals">
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
            onClick={() => navigate('/sales/invoices')}
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

    </ErpPageTemplate>
  );
}

