import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, type Resolver } from 'react-hook-form';
import { z } from 'zod';
import { useI18n } from '../../../i18n/i18n';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid, ZHPageNotice } from '../../../components/zh/ZHForm';
import { useSerials } from '../hooks/useInventoryItems';
import type { SerialDto } from '../../../types/items';

const SERIAL_STATUS_COLORS: Record<string, string> = {
  InStock:   'var(--color-success)',
  Sold:      'var(--color-info)',
  Returned:  'var(--color-warning)',
  Defective: 'var(--color-error)',
  Lost:      'var(--color-error)',
  Disposed:  'var(--color-text-tertiary)',
};

const registerSerialsSchema = z.object({
  warehouseId: z.string().uuid('Almacén obligatorio'),
  serials:     z.string().min(1, 'Al menos un serial es obligatorio'),
  lotId:       z.string().uuid().nullable().optional(),
});
type RegisterSerialsForm = z.output<typeof registerSerialsSchema>;

type Props = { itemId: string; warehouseId?: string };

export function SerialsPage({ itemId, warehouseId }: Props) {
  const { t } = useI18n();
  const [showForm, setShowForm] = useState(false);
  const [statusFilter, setStatusFilter] = useState('');

  const { serials, loading, error, registering, registerError, registerSerials } = useSerials(
    itemId,
    warehouseId
  );

  const form = useForm<RegisterSerialsForm>({
    resolver: zodResolver(registerSerialsSchema) as Resolver<RegisterSerialsForm>,
    defaultValues: { warehouseId: warehouseId ?? '', serials: '', lotId: null },
  });

  const handleRegister = async (values: RegisterSerialsForm) => {
    const serialList = values.serials
      .split(/[\n,;]/)
      .map(s => s.trim())
      .filter(Boolean);

    const result = await registerSerials({
      itemId,
      warehouseId: values.warehouseId,
      serials: serialList,
      lotId: values.lotId,
    });

    if (result) {
      form.reset({ warehouseId: warehouseId ?? '', serials: '' });
      setShowForm(false);
    }
  };

  const filtered = statusFilter ? serials.filter(s => s.status === statusFilter) : serials;

  return (
    <div>
      {(error || registerError) && (
        <ZHPageNotice variant="error" message={error ?? registerError ?? ''} style={{ marginBottom: 12 }} />
      )}

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <select
          className="zh-select"
          value={statusFilter}
          onChange={e => setStatusFilter(e.target.value)}
        >
          <option value="">{t('common.allStatuses', 'Todos')}</option>
          <option value="InStock">InStock</option>
          <option value="Sold">Sold</option>
          <option value="Returned">Returned</option>
          <option value="Defective">Defective</option>
          <option value="Lost">Lost</option>
        </select>
        <ZHBtn type="button" variant="primary" size="sm" onClick={() => setShowForm(true)}>
          + {t('serials.register', 'Registrar seriales')}
        </ZHBtn>
      </div>

      {loading ? (
        <p>{t('common.loading', 'Cargando...')}</p>
      ) : filtered.length === 0 ? (
        <p style={{ color: 'var(--color-text-secondary)' }}>{t('serials.empty', 'No hay seriales registrados.')}</p>
      ) : (
        <table className="zh-table">
          <thead>
            <tr>
              <th>{t('serials.col.serial', 'N° Serie')}</th>
              <th>{t('common.status', 'Estado')}</th>
              <th>{t('serials.col.acquiredAt', 'Ingreso')}</th>
              <th>{t('serials.col.soldAt', 'Salida')}</th>
              <th>{t('serials.col.documentRef', 'Documento')}</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map(s => (
              <tr key={s.id}>
                <td><code>{s.serial}</code></td>
                <td>
                  <span style={{ color: SERIAL_STATUS_COLORS[s.status], fontWeight: 600 }}>
                    {s.status}
                  </span>
                </td>
                <td>{new Date(s.acquiredAt).toLocaleDateString()}</td>
                <td>{s.soldAt ? new Date(s.soldAt).toLocaleDateString() : '—'}</td>
                <td>{s.documentRef ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {/* Register form */}
      {showForm && (
        <div className="zh-modal-backdrop">
          <div className="zh-modal">
            <h3>{t('serials.register', 'Registrar números de serie')}</h3>
            {registerError && <ZHPageNotice variant="error" message={registerError} style={{ marginBottom: 12 }} />}
            <form onSubmit={form.handleSubmit(handleRegister)}>
              <ZHFormSection>
                <ZHGrid cols={2}>
                  <ZHField label={t('serials.form.warehouse', 'Almacén *')} required fieldError={form.formState.errors.warehouseId?.message}>
                    <input {...form.register('warehouseId')} placeholder="UUID almacén" disabled={registering} />
                  </ZHField>
                  <ZHField label={t('serials.form.lotId', 'Lote (opcional)')}>
                    <input {...form.register('lotId')} placeholder="UUID lote" disabled={registering} />
                  </ZHField>
                </ZHGrid>
                <ZHField
                  label={t('serials.form.serials', 'Números de serie *')}
                  hint={t('serials.form.serialsHint', 'Un serial por línea, o separados por coma')}
                  required
                  fieldError={form.formState.errors.serials?.message}
                >
                  <textarea
                    {...form.register('serials')}
                    rows={6}
                    placeholder="SN-001&#10;SN-002&#10;SN-003"
                    disabled={registering}
                    style={{ fontFamily: 'monospace', fontSize: 13 }}
                  />
                </ZHField>
              </ZHFormSection>
              <div className="zh-form-actions-row zh-form-actions-row--end">
                <ZHBtn type="button" variant="ghost" size="md" onClick={() => { setShowForm(false); form.reset(); }} disabled={registering}>
                  {t('common.cancel', 'Cancelar')}
                </ZHBtn>
                <ZHBtn type="submit" variant="primary" size="md" disabled={registering}>
                  {registering ? t('common.saving', 'Registrando...') : t('serials.register', 'Registrar')}
                </ZHBtn>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
