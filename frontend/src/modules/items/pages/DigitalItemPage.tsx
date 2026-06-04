import { useState } from 'react';
import { useAsync } from '../../../hooks/useAsync';
import { useI18n } from '../../../i18n/i18n';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid, ZHPageNotice } from '../../../components/zh/ZHForm';
import { digitalItemService } from '../api/digitalItemService';
import { formatApiError } from '../../lib/formatApiError';
import type { DigitalItemDto } from '../../../types/items';

type Props = { itemId: string };

export function DigitalItemPage({ itemId }: Props) {
  const { t } = useI18n();
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [showDeliverableForm, setShowDeliverableForm] = useState(false);
  const [createForm, setCreateForm] = useState({ licenseType: 'Perpetual', deliveryMethod: 'Download', maxDownloads: '', validityDays: '' });
  const [deliverableForm, setDeliverableForm] = useState({ name: '', storageObjectId: '', version: '' });

  const state = useAsync(() => digitalItemService.getByItem(itemId), [itemId]);
  const di = state.data as DigitalItemDto | null | undefined;

  const handleCreate = async () => {
    setError(null);
    setSaving(true);
    try {
      await digitalItemService.create({
        itemId,
        licenseType: createForm.licenseType,
        deliveryMethod: createForm.deliveryMethod,
        maxDownloads: createForm.maxDownloads ? Number(createForm.maxDownloads) : null,
        validityDays: createForm.validityDays ? Number(createForm.validityDays) : null,
      });
      state.refetch();
      setShowCreateForm(false);
    } catch (err) { setError(formatApiError(err)); }
    finally { setSaving(false); }
  };

  const handleAddDeliverable = async () => {
    if (!di) return;
    setError(null);
    setSaving(true);
    try {
      await digitalItemService.addDeliverable(di.id, {
        name: deliverableForm.name,
        storageObjectId: deliverableForm.storageObjectId,
        version: deliverableForm.version || undefined,
      });
      state.refetch();
      setDeliverableForm({ name: '', storageObjectId: '', version: '' });
      setShowDeliverableForm(false);
    } catch (err) { setError(formatApiError(err)); }
    finally { setSaving(false); }
  };

  if (state.loading) return <p>{t('common.loading', 'Cargando...')}</p>;

  return (
    <div>
      {error && <ZHPageNotice variant="error" message={error} style={{ marginBottom: 12 }} />}

      {!di ? (
        <div style={{ textAlign: 'center', padding: 32 }}>
          <p style={{ marginBottom: 16, color: 'var(--color-text-secondary)' }}>
            {t('digital.noConfig', 'Este ítem no tiene configuración digital.')}
          </p>
          <ZHBtn type="button" variant="primary" size="md" onClick={() => setShowCreateForm(true)} disabled={saving}>
            {t('digital.configure', 'Configurar ítem digital')}
          </ZHBtn>
        </div>
      ) : (
        <div>
          {/* Config summary */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 16, marginBottom: 24 }}>
            <div style={{ padding: '16px', border: '1px solid var(--color-border)', borderRadius: 8 }}>
              <div style={{ fontSize: 12, color: 'var(--color-text-secondary)' }}>{t('digital.licenseType', 'Licencia')}</div>
              <div style={{ fontWeight: 600, marginTop: 4 }}>{di.licenseType}</div>
            </div>
            <div style={{ padding: '16px', border: '1px solid var(--color-border)', borderRadius: 8 }}>
              <div style={{ fontSize: 12, color: 'var(--color-text-secondary)' }}>{t('digital.deliveryMethod', 'Entrega')}</div>
              <div style={{ fontWeight: 600, marginTop: 4 }}>{di.deliveryMethod}</div>
            </div>
            <div style={{ padding: '16px', border: '1px solid var(--color-border)', borderRadius: 8 }}>
              <div style={{ fontSize: 12, color: 'var(--color-text-secondary)' }}>{t('digital.downloads', 'Descargas máx.')}</div>
              <div style={{ fontWeight: 600, marginTop: 4 }}>{di.maxDownloads ?? '∞'}</div>
            </div>
          </div>

          {/* Deliverables */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
            <h4 style={{ fontWeight: 600 }}>{t('digital.deliverables', 'Archivos descargables')}</h4>
            <ZHBtn type="button" variant="secondary" size="sm" onClick={() => setShowDeliverableForm(true)}>
              + {t('digital.addDeliverable', 'Agregar descargable')}
            </ZHBtn>
          </div>

          {di.deliverables.length === 0 ? (
            <p style={{ color: 'var(--color-text-secondary)' }}>
              {t('digital.noDeliverables', 'No hay archivos configurados.')}
            </p>
          ) : (
            <table className="zh-table">
              <thead>
                <tr>
                  <th>{t('digital.col.name', 'Nombre')}</th>
                  <th>{t('digital.col.version', 'Versión')}</th>
                  <th>{t('common.status', 'Estado')}</th>
                </tr>
              </thead>
              <tbody>
                {di.deliverables.map(d => (
                  <tr key={d.id}>
                    <td>{d.name}</td>
                    <td>{d.version ?? '—'}</td>
                    <td>
                      <span className={d.isActive ? 'zh-badge zh-badge--success' : 'zh-badge zh-badge--neutral'}>
                        {d.isActive ? t('common.active', 'Activo') : t('common.inactive', 'Inactivo')}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Create config form */}
      {showCreateForm && (
        <div className="zh-modal-backdrop">
          <div className="zh-modal">
            <h3>{t('digital.configure', 'Configurar ítem digital')}</h3>
            <ZHFormSection>
              <ZHGrid cols={2}>
                <ZHField label={t('digital.licenseType', 'Tipo de licencia *')} required>
                  <select className="zh-select" value={createForm.licenseType} onChange={e => setCreateForm(p => ({ ...p, licenseType: e.target.value }))} disabled={saving}>
                    <option value="Perpetual">Perpetua</option>
                    <option value="Subscription">Suscripción</option>
                    <option value="OneTime">Una vez</option>
                  </select>
                </ZHField>
                <ZHField label={t('digital.deliveryMethod', 'Método de entrega *')} required>
                  <select className="zh-select" value={createForm.deliveryMethod} onChange={e => setCreateForm(p => ({ ...p, deliveryMethod: e.target.value }))} disabled={saving}>
                    <option value="Download">Descarga</option>
                    <option value="Email">Email</option>
                    <option value="Api">API</option>
                  </select>
                </ZHField>
                <ZHField label={t('digital.maxDownloads', 'Descargas máximas')}>
                  <input type="number" min="1" className="zh-input" value={createForm.maxDownloads} onChange={e => setCreateForm(p => ({ ...p, maxDownloads: e.target.value }))} disabled={saving} />
                </ZHField>
                <ZHField label={t('digital.validityDays', 'Validez (días)')}>
                  <input type="number" min="1" className="zh-input" value={createForm.validityDays} onChange={e => setCreateForm(p => ({ ...p, validityDays: e.target.value }))} disabled={saving} />
                </ZHField>
              </ZHGrid>
            </ZHFormSection>
            <div className="zh-form-actions-row zh-form-actions-row--end">
              <ZHBtn type="button" variant="ghost" size="md" onClick={() => setShowCreateForm(false)} disabled={saving}>
                {t('common.cancel', 'Cancelar')}
              </ZHBtn>
              <ZHBtn type="button" variant="primary" size="md" onClick={handleCreate} disabled={saving}>
                {saving ? t('common.saving', 'Guardando...') : t('common.save', 'Guardar')}
              </ZHBtn>
            </div>
          </div>
        </div>
      )}

      {/* Add deliverable form */}
      {showDeliverableForm && (
        <div className="zh-modal-backdrop">
          <div className="zh-modal">
            <h3>{t('digital.addDeliverable', 'Agregar descargable')}</h3>
            <ZHFormSection>
              <ZHGrid cols={2}>
                <ZHField label={t('digital.form.name', 'Nombre *')} required>
                  <input className="zh-input" value={deliverableForm.name} onChange={e => setDeliverableForm(p => ({ ...p, name: e.target.value }))} disabled={saving} />
                </ZHField>
                <ZHField label={t('digital.form.version', 'Versión')}>
                  <input className="zh-input" value={deliverableForm.version} onChange={e => setDeliverableForm(p => ({ ...p, version: e.target.value }))} placeholder="v1.0.0" disabled={saving} />
                </ZHField>
                <ZHField label={t('digital.form.storageObjectId', 'Storage Object ID *')} required style={{ gridColumn: '1 / -1' }}>
                  <input className="zh-input" value={deliverableForm.storageObjectId} onChange={e => setDeliverableForm(p => ({ ...p, storageObjectId: e.target.value }))} placeholder="UUID del archivo en storage" disabled={saving} />
                </ZHField>
              </ZHGrid>
            </ZHFormSection>
            <div className="zh-form-actions-row zh-form-actions-row--end">
              <ZHBtn type="button" variant="ghost" size="md" onClick={() => setShowDeliverableForm(false)} disabled={saving}>
                {t('common.cancel', 'Cancelar')}
              </ZHBtn>
              <ZHBtn type="button" variant="primary" size="md" onClick={handleAddDeliverable} disabled={saving}>
                {saving ? t('common.saving', 'Guardando...') : t('common.save', 'Agregar')}
              </ZHBtn>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
