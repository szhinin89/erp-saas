import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, type Resolver } from 'react-hook-form';
import { z } from 'zod';
import { useAsync } from '../../../hooks/useAsync';
import { useI18n } from '../../../i18n/i18n';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid, ZHPageNotice } from '../../../components/zh/ZHForm';
import { pricingService } from '../api/pricingService';
import { formatApiError } from '../../lib/formatApiError';
import type { PriceListDto } from '../../../types/items';

const createListSchema = z.object({
  code:            z.string().trim().min(1).max(20),
  name:            z.string().trim().min(1).max(100),
  currencyCode:    z.string().length(3, 'Debe tener 3 caracteres').default('USD'),
  validFrom:       z.string().min(1, 'Fecha de inicio obligatoria'),
  priceIncludesVat: z.boolean().default(false),
  isDefault:       z.boolean().default(false),
  target:          z.enum(['General', 'CustomerGroup', 'Channel', 'Internal']).default('General'),
});
type CreateListForm = z.output<typeof createListSchema>;

export function PriceListsPage() {
  const { t } = useI18n();
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const state = useAsync(pricingService.getPriceLists);
  const lists = state.data ?? [];

  const form = useForm<CreateListForm>({
    resolver: zodResolver(createListSchema) as Resolver<CreateListForm>,
    defaultValues: { code: '', name: '', currencyCode: 'USD', validFrom: '', priceIncludesVat: false, isDefault: false, target: 'General' },
  });

  const handleCreate = async (values: CreateListForm) => {
    setError(null);
    setSaving(true);
    try {
      await pricingService.createPriceList(values);
      state.refetch();
      form.reset();
      setShowForm(false);
    } catch (err) { setError(formatApiError(err)); }
    finally { setSaving(false); }
  };

  return (
    <ErpPageTemplate
      kicker={t('pricing.kicker', 'Precios')}
      title={t('pricing.priceLists.title', 'Listas de Precio')}
      action={
        <ZHBtn variant="primary" size="md" type="button" onClick={() => setShowForm(true)}>
          + {t('pricing.priceLists.new', 'Nueva lista')}
        </ZHBtn>
      }
    >
      {error && <ZHPageNotice variant="error" message={error} />}

      {state.loading ? (
        <p>{t('common.loading', 'Cargando...')}</p>
      ) : lists.length === 0 ? (
        <p style={{ color: 'var(--color-text-secondary)' }}>
          {t('pricing.priceLists.empty', 'No hay listas de precio configuradas.')}
        </p>
      ) : (
        <table className="zh-table">
          <thead>
            <tr>
              <th>{t('common.code', 'Código')}</th>
              <th>{t('common.name', 'Nombre')}</th>
              <th>{t('pricing.col.currency', 'Moneda')}</th>
              <th>{t('pricing.col.target', 'Alcance')}</th>
              <th>{t('pricing.col.validFrom', 'Desde')}</th>
              <th>{t('pricing.col.validTo', 'Hasta')}</th>
              <th>{t('pricing.col.default', 'Default')}</th>
              <th>{t('common.status', 'Estado')}</th>
            </tr>
          </thead>
          <tbody>
            {lists.map(pl => (
              <tr key={pl.id}>
                <td><code>{pl.code}</code></td>
                <td><strong>{pl.name}</strong></td>
                <td>{pl.currencyCode}</td>
                <td>{pl.target}</td>
                <td>{pl.validFrom}</td>
                <td>{pl.validTo ?? '—'}</td>
                <td style={{ textAlign: 'center' }}>
                  {pl.isDefault && <span style={{ color: 'var(--color-success)' }}>★</span>}
                </td>
                <td>
                  <span className={pl.isActive ? 'zh-badge zh-badge--success' : 'zh-badge zh-badge--neutral'}>
                    {pl.isActive ? t('common.active', 'Activo') : t('common.inactive', 'Inactivo')}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {showForm && (
        <div className="zh-modal-backdrop">
          <div className="zh-modal">
            <h3>{t('pricing.priceLists.new', 'Nueva lista de precios')}</h3>
            <form onSubmit={form.handleSubmit(handleCreate)}>
              <ZHFormSection>
                <ZHGrid cols={2}>
                  <ZHField label={t('common.code', 'Código *')} required fieldError={form.formState.errors.code?.message}>
                    <input {...form.register('code')} placeholder="PUB-GENERAL" disabled={saving} />
                  </ZHField>
                  <ZHField label={t('common.name', 'Nombre *')} required fieldError={form.formState.errors.name?.message}>
                    <input {...form.register('name')} placeholder="Público General" disabled={saving} />
                  </ZHField>
                  <ZHField label={t('pricing.form.currency', 'Moneda *')} required fieldError={form.formState.errors.currencyCode?.message}>
                    <input {...form.register('currencyCode')} placeholder="USD" maxLength={3} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('pricing.form.validFrom', 'Válida desde *')} required fieldError={form.formState.errors.validFrom?.message}>
                    <input type="date" {...form.register('validFrom')} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('pricing.form.target', 'Alcance')}>
                    <select {...form.register('target')} disabled={saving}>
                      <option value="General">General</option>
                      <option value="CustomerGroup">Grupo de clientes</option>
                      <option value="Channel">Canal</option>
                      <option value="Internal">Interno</option>
                    </select>
                  </ZHField>
                </ZHGrid>
                <ZHGrid cols={2}>
                  <ZHField label="">
                    <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <input type="checkbox" {...form.register('priceIncludesVat')} disabled={saving} />
                      {t('pricing.form.priceIncludesVat', 'Precio incluye IVA')}
                    </label>
                  </ZHField>
                  <ZHField label="">
                    <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <input type="checkbox" {...form.register('isDefault')} disabled={saving} />
                      {t('pricing.form.isDefault', 'Lista predeterminada')}
                    </label>
                  </ZHField>
                </ZHGrid>
              </ZHFormSection>
              <div className="zh-form-actions-row zh-form-actions-row--end">
                <ZHBtn type="button" variant="ghost" size="md" onClick={() => { setShowForm(false); form.reset(); }} disabled={saving}>
                  {t('common.cancel', 'Cancelar')}
                </ZHBtn>
                <ZHBtn type="submit" variant="primary" size="md" disabled={saving}>
                  {saving ? t('common.saving', 'Guardando...') : t('common.save', 'Guardar')}
                </ZHBtn>
              </div>
            </form>
          </div>
        </div>
      )}
    </ErpPageTemplate>
  );
}
