import { useState } from 'react';
import { useAsync } from '../../../hooks/useAsync';
import { useI18n } from '../../../i18n/i18n';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid, ZHPageNotice } from '../../../components/zh/ZHForm';
import { kitService } from '../api/kitService';
import { formatApiError } from '../../lib/formatApiError';
import type { KitDto } from '../../../types/items';

type Props = { itemId: string };

export function KitDetailPage({ itemId }: Props) {
  const { t } = useI18n();
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [showLineForm, setShowLineForm] = useState(false);
  const [lineForm, setLineForm] = useState({ componentItemId: '', uomCode: '', quantity: '', isOptional: false });

  const kitState = useAsync(() => kitService.getByItem(itemId), [itemId]);
  const kit = kitState.data as KitDto | null | undefined;

  const handleCreateKit = async (kitType: string) => {
    setError(null);
    setSaving(true);
    try {
      await kitService.create(itemId, kitType);
      kitState.refetch();
    } catch (err) { setError(formatApiError(err)); }
    finally { setSaving(false); }
  };

  const handleAddLine = async () => {
    if (!kit) return;
    setError(null);
    setSaving(true);
    try {
      await kitService.addLine(kit.id, {
        componentItemId: lineForm.componentItemId,
        uomCode: lineForm.uomCode,
        quantity: Number(lineForm.quantity),
        isOptional: lineForm.isOptional,
      });
      kitState.refetch();
      setLineForm({ componentItemId: '', uomCode: '', quantity: '', isOptional: false });
      setShowLineForm(false);
    } catch (err) { setError(formatApiError(err)); }
    finally { setSaving(false); }
  };

  if (kitState.loading) return <p>{t('common.loading', 'Cargando...')}</p>;

  return (
    <div>
      {error && <ZHPageNotice variant="error" message={error} style={{ marginBottom: 12 }} />}

      {!kit ? (
        <div style={{ textAlign: 'center', padding: 32 }}>
          <p style={{ marginBottom: 16, color: 'var(--color-text-secondary)' }}>
            {t('kits.noKit', 'Este ítem no tiene una receta/kit configurada.')}
          </p>
          <div style={{ display: 'flex', gap: 12, justifyContent: 'center' }}>
            <ZHBtn type="button" variant="secondary" size="md" onClick={() => handleCreateKit('Fixed')} disabled={saving}>
              {t('kits.createFixed', 'Crear Kit Fijo')}
            </ZHBtn>
            <ZHBtn type="button" variant="secondary" size="md" onClick={() => handleCreateKit('Bundle')} disabled={saving}>
              {t('kits.createBundle', 'Crear Bundle')}
            </ZHBtn>
          </div>
        </div>
      ) : (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <div>
              <span className="zh-badge zh-badge--info" style={{ marginRight: 8 }}>{kit.kitType}</span>
              <span style={{ color: 'var(--color-text-secondary)', fontSize: 13 }}>
                {kit.lines.filter(l => l.isActive).length} {t('kits.components', 'componentes activos')}
              </span>
            </div>
            <ZHBtn type="button" variant="primary" size="sm" onClick={() => setShowLineForm(true)}>
              + {t('kits.addLine', 'Agregar componente')}
            </ZHBtn>
          </div>

          <table className="zh-table">
            <thead>
              <tr>
                <th>{t('kits.col.component', 'Componente (ID)')}</th>
                <th>{t('kits.col.qty', 'Cantidad')}</th>
                <th>{t('kits.col.uom', 'UOM')}</th>
                <th>{t('kits.col.optional', 'Opcional')}</th>
                <th>{t('common.status', 'Estado')}</th>
              </tr>
            </thead>
            <tbody>
              {kit.lines.map(line => (
                <tr key={line.id} style={{ opacity: line.isActive ? 1 : 0.5 }}>
                  <td><code style={{ fontSize: 12 }}>{line.componentItemId.slice(0, 8)}...</code></td>
                  <td>{line.quantity}</td>
                  <td>{line.uomCode}</td>
                  <td>{line.isOptional ? '✓' : '—'}</td>
                  <td>
                    <span className={line.isActive ? 'zh-badge zh-badge--success' : 'zh-badge zh-badge--neutral'}>
                      {line.isActive ? t('common.active', 'Activo') : t('common.inactive', 'Inactivo')}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Add line form */}
          {showLineForm && (
            <div className="zh-modal-backdrop">
              <div className="zh-modal">
                <h3>{t('kits.addLine', 'Agregar componente')}</h3>
                <ZHFormSection>
                  <ZHGrid cols={2}>
                    <ZHField label={t('kits.form.componentId', 'ID del componente *')} required>
                      <input
                        className="zh-input"
                        value={lineForm.componentItemId}
                        onChange={e => setLineForm(prev => ({ ...prev, componentItemId: e.target.value }))}
                        placeholder="UUID del ítem componente"
                        disabled={saving}
                      />
                    </ZHField>
                    <ZHField label={t('kits.form.uomCode', 'UOM *')} required>
                      <input
                        className="zh-input"
                        value={lineForm.uomCode}
                        onChange={e => setLineForm(prev => ({ ...prev, uomCode: e.target.value }))}
                        placeholder="UND"
                        disabled={saving}
                      />
                    </ZHField>
                    <ZHField label={t('kits.form.quantity', 'Cantidad *')} required>
                      <input
                        type="number"
                        step="0.001"
                        min="0.001"
                        className="zh-input"
                        value={lineForm.quantity}
                        onChange={e => setLineForm(prev => ({ ...prev, quantity: e.target.value }))}
                        disabled={saving}
                      />
                    </ZHField>
                    <ZHField label="">
                      <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                        <input
                          type="checkbox"
                          checked={lineForm.isOptional}
                          onChange={e => setLineForm(prev => ({ ...prev, isOptional: e.target.checked }))}
                          disabled={saving}
                        />
                        {t('kits.form.isOptional', 'Componente opcional')}
                      </label>
                    </ZHField>
                  </ZHGrid>
                </ZHFormSection>
                <div className="zh-form-actions-row zh-form-actions-row--end">
                  <ZHBtn type="button" variant="ghost" size="md" onClick={() => setShowLineForm(false)} disabled={saving}>
                    {t('common.cancel', 'Cancelar')}
                  </ZHBtn>
                  <ZHBtn type="button" variant="primary" size="md" onClick={handleAddLine} disabled={saving}>
                    {saving ? t('common.saving', 'Guardando...') : t('kits.addLine', 'Agregar')}
                  </ZHBtn>
                </div>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
