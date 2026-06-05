import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHField } from '../../../components/zh/ZHForm';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { useAsync } from '../../../hooks/useAsync';
import { salesInvoicesService } from '../api/salesInvoicesService';
import { withholdingReceivedService } from '../api/withholdingReceivedService';
import { formatApiError } from '../../lib/formatApiError';

export function WithholdingReceivedNewPage() {
  const { canShow } = usePermissionsUi();
  const navigate    = useNavigate();

  const canCreate = canShow('sales.withholding-received.create');

  const [salesBillId, setSalesBillId] = useState('');
  const [xmlContent,  setXmlContent]  = useState('');
  const [saving,      setSaving]      = useState(false);
  const [error,       setError]       = useState<string | null>(null);

  const invoicesState = useAsync(
    () => salesInvoicesService.list({ pageSize: 500, estado: 'Autorizado' }),
    canCreate,
  );
  const authorizedInvoices = invoicesState.data?.items ?? [];

  if (!canCreate) return <NoAccessPage title="Registrar Retención Recibida" />;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!salesBillId.trim()) { setError('Seleccione una factura de venta.'); return; }
    if (!xmlContent.trim())  { setError('Pegue o cargue el XML de la retención.'); return; }

    setSaving(true);
    setError(null);
    try {
      await withholdingReceivedService.create({ salesBillId, xmlContent });
      navigate('/sales/withholding-received');
    } catch (err) {
      setError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  }

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (ev) => setXmlContent(ev.target?.result as string ?? '');
    reader.readAsText(file, 'UTF-8');
  }

  return (
    <ErpPageTemplate
      kicker="Ventas"
      title="Registrar Retención Recibida"
      subtitle="Registre el comprobante de retención en la fuente emitido por un cliente."
      action={
        <button
          type="button"
          className="zh-btn zh-btn--ghost"
          onClick={() => navigate('/sales/withholding-received')}
        >
          <span className="material-symbols-outlined">arrow_back</span>
          Volver
        </button>
      }
    >
      {error && <ZHPageNotice variant="error" message={error} />}

      <form className="pg-section pg-form" onSubmit={(e) => void handleSubmit(e)}>
        <div className="pg-form-grid">
          <ZHField label="Factura de venta autorizada" required>
            <select
              className="zh-input"
              value={salesBillId}
              onChange={(e) => setSalesBillId(e.target.value)}
              disabled={saving || invoicesState.loading}
              required
            >
              <option value="">
                {invoicesState.loading ? 'Cargando facturas…' : '— Seleccione una factura —'}
              </option>
              {authorizedInvoices.map((inv) => (
                <option key={inv.id} value={inv.id}>
                  {inv.establecimiento}-{inv.puntoEmision}-{inv.secuencial} — {inv.clienteNombre} — ${inv.total.toFixed(2)}
                </option>
              ))}
            </select>
          </ZHField>

          <ZHField label="Archivo XML de la retención">
            <input
              type="file"
              accept=".xml,text/xml"
              className="zh-input"
              onChange={handleFileChange}
              disabled={saving}
            />
            <p className="zh-field-hint">También puede pegar el XML directamente en el área de texto.</p>
          </ZHField>
        </div>

        <ZHField label="Contenido XML" required>
          <textarea
            className="zh-input"
            style={{ fontFamily: 'monospace', fontSize: '12px', resize: 'vertical' }}
            rows={14}
            placeholder={'<?xml version="1.0" encoding="UTF-8"?>\n<comprobanteRetencion>…</comprobanteRetencion>'}
            value={xmlContent}
            onChange={(e) => setXmlContent(e.target.value)}
            disabled={saving}
            required
          />
        </ZHField>

        <div className="pg-form-actions">
          <button
            type="button"
            className="zh-btn zh-btn--ghost"
            onClick={() => navigate('/sales/withholding-received')}
            disabled={saving}
          >
            Cancelar
          </button>
          <button
            type="submit"
            className="zh-btn zh-btn--primary"
            disabled={saving || !salesBillId || !xmlContent.trim()}
          >
            {saving ? 'Registrando…' : 'Registrar retención'}
          </button>
        </div>
      </form>
    </ErpPageTemplate>
  );
}
