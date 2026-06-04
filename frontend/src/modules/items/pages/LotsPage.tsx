import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, type Resolver } from 'react-hook-form';
import { z } from 'zod';
import { useI18n } from '../../../i18n/i18n';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid, ZHPageNotice } from '../../../components/zh/ZHForm';
import { useLots } from '../hooks/useInventoryItems';
import type { LotDto } from '../../../types/items';

const LOT_STATUS_COLORS: Record<string, string> = {
  Active:     'var(--color-success)',
  Quarantine: 'var(--color-warning)',
  Expired:    'var(--color-error)',
  Depleted:   'var(--color-text-tertiary)',
};

const registerLotSchema = z.object({
  lotNumber:       z.string().trim().min(1, 'Número de lote obligatorio').max(100),
  itemId:          z.string().uuid('Ítem obligatorio'),
  warehouseId:     z.string().uuid('Almacén obligatorio'),
  initialQty:      z.number().positive('La cantidad debe ser mayor que cero'),
  manufactureDate: z.string().nullable().optional(),
  expirationDate:  z.string().nullable().optional(),
  notes:           z.string().max(500).nullable().optional(),
});
type RegisterLotForm = z.output<typeof registerLotSchema>;

type Props = {
  itemId:      string;
  warehouseId?: string;
};

export function LotsPage({ itemId, warehouseId }: Props) {
  const { t } = useI18n();
  const [showForm, setShowForm] = useState(false);
  const [statusFilter, setStatusFilter] = useState<string>('');

  const { lots, loading, error, registering, registerError, registerLot } = useLots(
    itemId,
    warehouseId
  );

  const form = useForm<RegisterLotForm>({
    resolver: zodResolver(registerLotSchema) as Resolver<RegisterLotForm>,
    defaultValues: { lotNumber: '', itemId, warehouseId: warehouseId ?? '', initialQty: 1, notes: null },
  });

  const handleRegister = async (values: RegisterLotForm) => {
    const lot = await registerLot({
      ...values,
      initialQty: Number(values.initialQty),
    });
    if (lot) {
      form.reset({ lotNumber: '', itemId, warehouseId: warehouseId ?? '', initialQty: 1 });
      setShowForm(false);
    }
  };

  const filtered = statusFilter
    ? lots.filter(l => l.status === statusFilter)
    : lots;

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
          <option value="">{t('common.allStatuses', 'Todos los estados')}</option>
          <option value="Active">{t('lots.status.active', 'Activo')}</option>
          <option value="Quarantine">{t('lots.status.quarantine', 'Cuarentena')}</option>
          <option value="Expired">{t('lots.status.expired', 'Vencido')}</option>
          <option value="Depleted">{t('lots.status.depleted', 'Agotado')}</option>
        </select>
        <ZHBtn type="button" variant="primary" size="sm" onClick={() => setShowForm(true)}>
          + {t('lots.register', 'Registrar lote')}
        </ZHBtn>
      </div>

      {/* Table */}
      {loading ? (
        <p>{t('common.loading', 'Cargando...')}</p>
      ) : filtered.length === 0 ? (
        <p style={{ color: 'var(--color-text-secondary)' }}>
          {t('lots.empty', 'No hay lotes registrados.')}
        </p>
      ) : (
        <table className="zh-table">
          <thead>
            <tr>
              <th>{t('lots.col.lotNumber', 'Nº Lote')}</th>
              <th>{t('lots.col.initialQty', 'Cant. inicial')}</th>
              <th>{t('lots.col.currentQty', 'Cant. actual')}</th>
              <th>{t('lots.col.manufactureDate', 'Fabricación')}</th>
              <th>{t('lots.col.expirationDate', 'Vencimiento')}</th>
              <th>{t('common.status', 'Estado')}</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((lot) => (
              <LotRow key={lot.id} lot={lot} t={t} />
            ))}
          </tbody>
        </table>
      )}

      {/* Register form modal */}
      {showForm && (
        <div className="zh-modal-backdrop">
          <div className="zh-modal">
            <h3>{t('lots.register', 'Registrar lote')}</h3>
            {registerError && <ZHPageNotice variant="error" message={registerError} style={{ marginBottom: 12 }} />}
            <form onSubmit={form.handleSubmit(handleRegister)}>
              <ZHFormSection>
                <ZHGrid cols={2}>
                  <ZHField label={t('lots.form.lotNumber', 'Número de lote *')} required fieldError={form.formState.errors.lotNumber?.message}>
                    <input {...form.register('lotNumber')} placeholder="LOT-2026-001" disabled={registering} />
                  </ZHField>
                  <ZHField label={t('lots.form.initialQty', 'Cantidad inicial *')} required fieldError={form.formState.errors.initialQty?.message}>
                    <input type="number" step="0.001" min="0.001" {...form.register('initialQty', { valueAsNumber: true })} disabled={registering} />
                  </ZHField>
                  <ZHField label={t('lots.form.warehouseId', 'Almacén *')} required fieldError={form.formState.errors.warehouseId?.message}>
                    <input {...form.register('warehouseId')} placeholder="UUID almacén" disabled={registering} />
                  </ZHField>
                  <ZHField label={t('lots.form.manufactureDate', 'Fecha fabricación')}>
                    <input type="date" {...form.register('manufactureDate')} disabled={registering} />
                  </ZHField>
                  <ZHField label={t('lots.form.expirationDate', 'Fecha vencimiento')}>
                    <input type="date" {...form.register('expirationDate')} disabled={registering} />
                  </ZHField>
                </ZHGrid>
                <ZHField label={t('lots.form.notes', 'Notas')}>
                  <textarea {...form.register('notes')} rows={2} disabled={registering} />
                </ZHField>
              </ZHFormSection>
              <div className="zh-form-actions-row zh-form-actions-row--end">
                <ZHBtn type="button" variant="ghost" size="md" onClick={() => { setShowForm(false); form.reset(); }} disabled={registering}>
                  {t('common.cancel', 'Cancelar')}
                </ZHBtn>
                <ZHBtn type="submit" variant="primary" size="md" disabled={registering}>
                  {registering ? t('common.saving', 'Guardando...') : t('lots.register', 'Registrar')}
                </ZHBtn>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

function LotRow({ lot, t }: { lot: LotDto; t: (k: string, fb?: string) => string }) {
  const isExpiringSoon = lot.expirationDate
    ? new Date(lot.expirationDate) <= new Date(Date.now() + 30 * 86400000)
    : false;

  return (
    <tr>
      <td><strong>{lot.lotNumber}</strong></td>
      <td>{lot.initialQty.toFixed(4)}</td>
      <td style={{ fontWeight: lot.currentQty === 0 ? 400 : 600 }}>
        {lot.currentQty.toFixed(4)}
      </td>
      <td>{lot.manufactureDate ?? '—'}</td>
      <td style={{ color: isExpiringSoon ? 'var(--color-warning)' : undefined }}>
        {lot.expirationDate ?? '—'}
        {isExpiringSoon && <span style={{ marginLeft: 4 }}>⚠</span>}
      </td>
      <td>
        <span style={{ color: LOT_STATUS_COLORS[lot.status], fontWeight: 600 }}>
          {t(`lots.status.${lot.status.toLowerCase()}`, lot.status)}
        </span>
      </td>
    </tr>
  );
}
