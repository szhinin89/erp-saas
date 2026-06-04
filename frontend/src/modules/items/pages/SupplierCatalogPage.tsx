import { useState } from 'react';
import { useAsync } from '../../../hooks/useAsync';
import { useI18n } from '../../../i18n/i18n';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid, ZHPageNotice } from '../../../components/zh/ZHForm';
import { supplierCatalogService } from '../api/supplierCatalogService';
import { formatApiError } from '../../lib/formatApiError';

type Props = { itemId: string };

export function SupplierCatalogPage({ itemId }: Props) {
  const { t } = useI18n();
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({
    supplierId: '', supplierCode: '', defaultUomCode: '',
    supplierName: '', leadTimeDays: '0', minOrderQty: '1', isDefault: false,
  });

  const state = useAsync(() => supplierCatalogService.getByItem(itemId), [itemId]);
  const suppliers = state.data ?? [];

  const handleRegister = async () => {
    setError(null);
    setSaving(true);
    try {
      await supplierCatalogService.register({
        itemId,
        supplierId: form.supplierId,
        supplierCode: form.supplierCode,
        defaultUomCode: form.defaultUomCode,
        supplierName: form.supplierName || null,
        leadTimeDays: Number(form.leadTimeDays),
        minOrderQty: Number(form.minOrderQty),
        isDefault: form.isDefault,
      });
      state.refetch();
      setForm({ supplierId: '', supplierCode: '', defaultUomCode: '', supplierName: '', leadTimeDays: '0', minOrderQty: '1', isDefault: false });
      setShowForm(false);
    } catch (err) { setError(formatApiError(err)); }
    finally { setSaving(false); }
  };

  return (
    <div>
      {error && <ZHPageNotice variant="error" message={error} style={{ marginBottom: 12 }} />}

      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 16 }}>
        <ZHBtn type="button" variant="primary" size="sm" onClick={() => setShowForm(true)}>
          + {t('suppliers.register', 'Registrar proveedor')}
        </ZHBtn>
      </div>

      {state.loading ? (
        <p>{t('common.loading', 'Cargando...')}</p>
      ) : suppliers.length === 0 ? (
        <p style={{ color: 'var(--color-text-secondary)' }}>
          {t('suppliers.empty', 'No hay proveedores registrados para este ítem.')}
        </p>
      ) : (
        <table className="zh-table">
          <thead>
            <tr>
              <th>{t('suppliers.col.code', 'Código proveedor')}</th>
              <th>{t('suppliers.col.name', 'Nombre proveedor')}</th>
              <th>{t('suppliers.col.uom', 'UOM')}</th>
              <th>{t('suppliers.col.leadTime', 'Lead time (días)')}</th>
              <th>{t('suppliers.col.minQty', 'Cant. mínima')}</th>
              <th>{t('suppliers.col.default', 'Principal')}</th>
            </tr>
          </thead>
          <tbody>
            {suppliers.map(s => (
              <tr key={s.id}>
                <td><code>{s.supplierCode}</code></td>
                <td>{s.supplierName ?? '—'}</td>
                <td>{s.defaultUomCode}</td>
                <td>{s.leadTimeDays}</td>
                <td>{s.minOrderQty}</td>
                <td style={{ textAlign: 'center' }}>
                  {s.isDefault && <span style={{ color: 'var(--color-success)' }}>★</span>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {showForm && (
        <div className="zh-modal-backdrop">
          <div className="zh-modal">
            <h3>{t('suppliers.register', 'Registrar proveedor del ítem')}</h3>
            <ZHFormSection>
              <ZHGrid cols={2}>
                <ZHField label={t('suppliers.form.supplierId', 'ID Proveedor *')} required>
                  <input className="zh-input" value={form.supplierId} onChange={e => setForm(p => ({ ...p, supplierId: e.target.value }))} placeholder="UUID proveedor" disabled={saving} />
                </ZHField>
                <ZHField label={t('suppliers.form.supplierCode', 'Código del proveedor *')} required>
                  <input className="zh-input" value={form.supplierCode} onChange={e => setForm(p => ({ ...p, supplierCode: e.target.value }))} disabled={saving} />
                </ZHField>
                <ZHField label={t('suppliers.form.supplierName', 'Nombre del proveedor para el ítem')}>
                  <input className="zh-input" value={form.supplierName} onChange={e => setForm(p => ({ ...p, supplierName: e.target.value }))} disabled={saving} />
                </ZHField>
                <ZHField label={t('suppliers.form.uom', 'UOM *')} required>
                  <input className="zh-input" value={form.defaultUomCode} onChange={e => setForm(p => ({ ...p, defaultUomCode: e.target.value }))} placeholder="CJA" disabled={saving} />
                </ZHField>
                <ZHField label={t('suppliers.form.leadTime', 'Lead time (días)')}>
                  <input type="number" min="0" className="zh-input" value={form.leadTimeDays} onChange={e => setForm(p => ({ ...p, leadTimeDays: e.target.value }))} disabled={saving} />
                </ZHField>
                <ZHField label={t('suppliers.form.minQty', 'Cantidad mínima de orden')}>
                  <input type="number" min="0.001" step="0.001" className="zh-input" value={form.minOrderQty} onChange={e => setForm(p => ({ ...p, minOrderQty: e.target.value }))} disabled={saving} />
                </ZHField>
              </ZHGrid>
              <ZHField label="">
                <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <input type="checkbox" checked={form.isDefault} onChange={e => setForm(p => ({ ...p, isDefault: e.target.checked }))} disabled={saving} />
                  {t('suppliers.form.isDefault', 'Proveedor principal de este ítem')}
                </label>
              </ZHField>
            </ZHFormSection>
            <div className="zh-form-actions-row zh-form-actions-row--end">
              <ZHBtn type="button" variant="ghost" size="md" onClick={() => setShowForm(false)} disabled={saving}>
                {t('common.cancel', 'Cancelar')}
              </ZHBtn>
              <ZHBtn type="button" variant="primary" size="md" onClick={handleRegister} disabled={saving}>
                {saving ? t('common.saving', 'Guardando...') : t('common.save', 'Registrar')}
              </ZHBtn>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
