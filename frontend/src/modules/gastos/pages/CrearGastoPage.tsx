import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { useAsync } from '../../../hooks/useAsync';
import { supplierService } from '../../compras/suppliers/api/supplierService';
import { gastosService, type ExpenseCategoryDto } from '../api/gastosService';
import './gastos-page.css';
import { usePermissionsUi } from '../../../access/usePermissionsUi';

const TODAY = new Date().toISOString().split('T')[0]!;

export function CrearGastoPage() {
  const { canShow } = usePermissionsUi();
  const navigate  = useNavigate();
  const canCreate = canShow('expenses.invoices.create');

  const suppliersState  = useAsync(() => supplierService.getAll());
  const categoriasState = useAsync(() => gastosService.getCategorias());

  const [supplierId,    setSupplierId]    = useState('');
  const [invoiceNumber, setInvoiceNumber] = useState('');
  const [issueDate,     setIssueDate]     = useState(TODAY);
  const [category,      setCategory]      = useState('');
  const [concept,       setConcept]       = useState('');
  const [subtotal,      setSubtotal]      = useState('');
  const [vatTotal,      setVatTotal]      = useState('0.00');
  const [total,         setTotal]         = useState('');
  const [notes,         setNotes]         = useState('');
  const [saving,        setSaving]        = useState(false);
  const [error,         setError]         = useState<string | null>(null);

  // Auto-calcular total cuando cambia subtotal o IVA
  useEffect(() => {
    const sub = parseFloat(subtotal) || 0;
    const vat = parseFloat(vatTotal) || 0;
    setTotal((sub + vat).toFixed(2));
  }, [subtotal, vatTotal]);

  const handleSubmit = async () => {
    setError(null);
    if (!concept.trim()) { setError('El concepto es obligatorio.'); return; }
    if (!category) { setError('Selecciona una categoría de gasto.'); return; }
    const sub = parseFloat(subtotal);
    const tot = parseFloat(total);
    if (isNaN(sub) || sub <= 0) { setError('El subtotal debe ser mayor a 0.'); return; }
    if (isNaN(tot) || tot < sub) { setError('El total no puede ser menor al subtotal.'); return; }

    setSaving(true);
    try {
      await gastosService.crearManual({
        supplierId: supplierId || null,
        issueDate,
        invoiceNumber: invoiceNumber.trim() || null,
        concept: concept.trim(),
        category,
        subtotal: sub,
        vatTotal: parseFloat(vatTotal) || 0,
        total: tot,
        notes: notes.trim() || null,
      });
      navigate('/gastos');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al guardar el gasto.');
    } finally {
      setSaving(false);
    }
  };

  if (!canCreate) return <NoAccessPage title="Nuevo Gasto" />;

  const categorias: ExpenseCategoryDto[] = categoriasState.data ?? [];

  return (
    <ErpPageTemplate
      kicker="Gastos"
      title="Nuevo Gasto"
      action={
        <>
          <button className="zh-btn zh-btn--ghost zh-btn--md" type="button" onClick={() => navigate('/gastos')}>
            Cancelar
          </button>
          <ZHBtn variant="primary" size="md" onClick={() => void handleSubmit()} disabled={saving}>
            {saving ? 'Guardando...' : 'Guardar Gasto'}
          </ZHBtn>
        </>
      }
    >
      {error && <ZHPageNotice variant="error" message={error} />}

      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">receipt_long</span>
            <span className="pg-section-label">Datos del Gasto</span>
          </div>
        </div>
        <div className="pg-section-body">
          <div className="pg-form-grid pg-form-grid--3">

            <ZHField label="Fecha" required>
              <input className="zh-input" type="date" value={issueDate} onChange={(e) => setIssueDate(e.target.value)} />
            </ZHField>

            <ZHField label="Categoría de Gasto" required>
              <select
                className="zh-input"
                value={category}
                onChange={(e) => setCategory(e.target.value)}
                disabled={categoriasState.loading}
              >
                <option value="">-- Seleccionar categoría --</option>
                {categorias.map((c) => (
                  <option key={c.id} value={c.name}>{c.name}</option>
                ))}
                {categorias.length === 0 && !categoriasState.loading && (
                  <option disabled value="">Sin categorías — configurar en Contabilidad</option>
                )}
              </select>
            </ZHField>

            <ZHField label="Proveedor">
              <select
                className="zh-input"
                value={supplierId}
                onChange={(e) => setSupplierId(e.target.value)}
                disabled={suppliersState.loading}
              >
                <option value="">-- Sin proveedor (opcional) --</option>
                {(suppliersState.data ?? []).map((s) => (
                  <option key={s.id} value={s.id}>{s.legalName} ({s.taxId})</option>
                ))}
              </select>
            </ZHField>

            <ZHField label="Nº Factura / Referencia">
              <input
                className="zh-input"
                value={invoiceNumber}
                onChange={(e) => setInvoiceNumber(e.target.value)}
                placeholder="001-001-000000123 (opcional)"
              />
            </ZHField>

            <ZHField label="Concepto / Descripción" required>
              <input
                className="zh-input"
                value={concept}
                onChange={(e) => setConcept(e.target.value)}
                placeholder="Ej: Arriendo local enero 2026"
              />
            </ZHField>

            <ZHField label="Observaciones">
              <input className="zh-input" value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Opcional" />
            </ZHField>

          </div>

          {/* Totales */}
          <div className="gst-totals-section">
            <div className="pg-form-grid pg-form-grid--3 gst-totals-grid">

              <ZHField label="Subtotal (sin IVA)" required>
                <input
                  className="zh-input"
                  type="number"
                  min="0"
                  step="0.01"
                  value={subtotal}
                  onChange={(e) => setSubtotal(e.target.value)}
                  placeholder="0.00"
                />
              </ZHField>

              <ZHField label="IVA">
                <input
                  className="zh-input"
                  type="number"
                  min="0"
                  step="0.01"
                  value={vatTotal}
                  onChange={(e) => setVatTotal(e.target.value)}
                />
              </ZHField>

              <ZHField label="Total">
                <input
                  className="zh-input gst-total-input"
                  type="number"
                  min="0"
                  step="0.01"
                  value={total}
                  onChange={(e) => setTotal(e.target.value)}
                />
              </ZHField>

            </div>
          </div>
        </div>
      </div>
    </ErpPageTemplate>
  );
}
