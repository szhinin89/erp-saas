import { useCallback, useEffect, useMemo, useState } from 'react';
import { PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge, NoAccessPage } from '../components/PageShell';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { useAuthStore } from '../store/authStore';
import { ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHBtn } from '../components/zh/ZHForm';

export type CatalogRow = { id: string; code: string; name: string; isActive: boolean };

type LoadFn = () => Promise<CatalogRow[]>;
type CreateFn = (payload: Record<string, unknown>) => Promise<unknown>;

type Field =
  | { key: 'code' | 'name'; labelKey: string; placeholderKey: string; type?: 'text' }
  | { key: string; labelKey: string; placeholderKey: string; type: 'number' | 'text' | 'select'; options?: { value: string; label: string }[] };

interface Props {
  titleKey: string;
  load: LoadFn;
  create: CreateFn;
  fields?: Field[]; // default: code+name
  viewPermissionKey: string;
  createPermissionKey: string;
}

export function CatalogSimplePage({ titleKey, load, create, fields, viewPermissionKey, createPermissionKey }: Props) {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const canView = isAdmin || hasPerm(viewPermissionKey);
  const canCreate = isAdmin || hasPerm(createPermissionKey);

  const actualFields = useMemo<Field[]>(
    () =>
      fields ?? [
        { key: 'code', labelKey: 'common.code', placeholderKey: 'common.codePlaceholder', type: 'text' },
        { key: 'name', labelKey: 'common.name', placeholderKey: 'common.namePlaceholder', type: 'text' },
      ],
    [fields]
  );

  const [items, setItems] = useState<CatalogRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const [form, setForm] = useState<Record<string, string>>(() => {
    const init: Record<string, string> = {};
    for (const f of actualFields) init[f.key] = '';
    return init;
  });

  const refresh = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      const data = await load();
      setItems(data ?? []);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric');
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [load, t]);

  useEffect(() => {
    Promise.resolve().then(refresh);
  }, [refresh]);

  if (!canView) {
    return <NoAccessPage title={t(titleKey)} />;
  }

  const onCreate = async () => {
    setError('');
    setSaving(true);
    try {
      const payload: Record<string, unknown> = {};
      for (const f of actualFields) {
        const v = form[f.key]?.trim();
        if (f.type === 'number') payload[f.key] = v === '' ? null : Number(v);
        else payload[f.key] = v;
      }
      await create(payload);
      setForm(() => {
        const next: Record<string, string> = {};
        for (const f of actualFields) next[f.key] = '';
        return next;
      });
      await refresh();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric');
      setError(msg);
    } finally {
      setSaving(false);
    }
  };

  return (
    <PageShell
      kicker={t('app.nav.group.catalog')}
      title={t(titleKey)}
      action={
        canCreate ? (
          <ZHBtn variant="primary" type="button" onClick={onCreate} disabled={saving || loading}>
            {saving ? t('common.saving') : t('common.create')}
          </ZHBtn>
        ) : undefined
      }
    >
      <TableCard>
        {!canCreate && (
          <div className="empty-state zh-mb-12">
            {t('common.readOnly')}
          </div>
        )}
        <div className="zh-form">
          <ZHFormBody>
            <ZHFormSection title={t(titleKey)}>
              <ZHGrid cols={2}>
                {actualFields.map((f) => (
                  <ZHField key={f.key} label={t(f.labelKey)} required={f.key === 'code' || f.key === 'name'}>
                    {f.type === 'select' ? (
                      <select
                        value={form[f.key] ?? ''}
                        onChange={(e) => setForm((s) => ({ ...s, [f.key]: e.target.value }))}
                        disabled={!canCreate || saving || loading}
                      >
                        <option value="">{t('common.select')}</option>
                        {(f.options ?? []).map((o) => (
                          <option key={o.value} value={o.value}>
                            {o.label}
                          </option>
                        ))}
                      </select>
                    ) : (
                      <input
                        value={form[f.key] ?? ''}
                        onChange={(e) => setForm((s) => ({ ...s, [f.key]: e.target.value }))}
                        placeholder={t(f.placeholderKey)}
                        type={f.type === 'number' ? 'number' : 'text'}
                        step={f.type === 'number' ? '0.01' : undefined}
                        disabled={!canCreate || saving || loading}
                      />
                    )}
                  </ZHField>
                ))}
              </ZHGrid>
            </ZHFormSection>

            {/* Acción principal ya está en el header del PageShell */}
          </ZHFormBody>
        </div>

        {error && <ErrorState message={error} />}
        {loading ? (
          <LoadingState />
        ) : items.length === 0 ? (
          <EmptyState message={t('common.noData')} />
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>{t('common.code')}</th>
                <th>{t('common.name')}</th>
                <th>{t('common.status')}</th>
              </tr>
            </thead>
            <tbody>
              {items.map((x) => (
                <tr key={x.id}>
                  <td>{x.code}</td>
                  <td>{x.name}</td>
                  <td>
                    <Badge label={x.isActive ? t('common.active') : t('common.inactive')} variant={x.isActive ? 'green' : 'gray'} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </TableCard>
    </PageShell>
  );
}

