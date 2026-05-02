import { useCallback, useEffect, useMemo, useState } from 'react';
import { PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge, NoAccessPage } from '../components/PageShell';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { useAuthStore } from '../store/authStore';
import { ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHBtn } from '../components/zh/ZHForm';
import ZHSearchBar from '../components/shared/ZHSearchBar';
import { EntityAuditPanel } from '../components/EntityAuditPanel';

export type CatalogRow = { id: string; code: string; name: string; isActive: boolean };

type LoadFn = () => Promise<CatalogRow[]>;
type CreateFn = (payload: Record<string, unknown>) => Promise<unknown>;

type Field =
  | { key: 'code' | 'name'; labelKey: string; placeholderKey: string; type?: 'text' }
  | { key: string; labelKey: string; placeholderKey: string; type: 'number' | 'text' | 'select'; options?: { value: string; label: string }[] };

interface Props {
  titleKey: string;
  /** Clave i18n `*.tabList` — copy «Ver [entidad]» / «View …» solo en la pestaña de listado. */
  listTabLabelKey: string;
  /** Texto del botón principal (verbo + objeto), p. ej. catalog.brands.primaryCreate */
  primaryCreateKey: string;
  /** Clave i18n para el botón «nuevo» en la pestaña listado (por defecto = primaryCreateKey). */
  listNewActionKey?: string;
  /** Placeholder y leyenda de conteo en la barra de búsqueda del listado. */
  listSearchPlaceholderKey?: string;
  listEntityLabelKey?: string;
  load: LoadFn;
  create: CreateFn;
  fields?: Field[]; // default: code+name
  viewPermissionKey: string;
  createPermissionKey: string;
  /** P. ej. "Brand" — añade la pestaña «Auditoría» a la derecha; el listado permite elegir fila. */
  auditEntityType?: string;
}

type SimpleTab = 'data' | 'list' | 'audit';

export function CatalogSimplePage({
  titleKey,
  listTabLabelKey,
  primaryCreateKey,
  listNewActionKey,
  listSearchPlaceholderKey = 'common.zhList.searchPlaceholder',
  listEntityLabelKey = 'common.zhList.entityLabel',
  load,
  create,
  fields,
  viewPermissionKey,
  createPermissionKey,
  auditEntityType,
}: Props) {
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
  const [tab, setTab] = useState<SimpleTab>('data');
  const [listQuery, setListQuery] = useState('');
  const [auditEntityId, setAuditEntityId] = useState<string | null>(null);
  const [auditRefreshKey, setAuditRefreshKey] = useState(0);

  const listFiltered = useMemo(() => {
    const q = listQuery.trim().toLowerCase();
    if (!q) return items;
    return items.filter((x) => `${x.code} ${x.name}`.toLowerCase().includes(q));
  }, [items, listQuery]);

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
      const created = (await create(payload)) as CatalogRow | undefined;
      setForm(() => {
        const next: Record<string, string> = {};
        for (const f of actualFields) next[f.key] = '';
        return next;
      });
      await refresh();
      if (auditEntityType && created?.id) {
        setAuditEntityId(created.id);
        setAuditRefreshKey((k) => k + 1);
        setTab('audit');
      } else {
        setTab('list');
      }
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
        canCreate && tab === 'data' ? (
          <ZHBtn variant="primary" size="md" type="button" onClick={onCreate} disabled={saving || loading}>
            {saving ? t('common.saving') : t(primaryCreateKey)}
          </ZHBtn>
        ) : undefined
      }
    >
      <TableCard>
        {error ? <ErrorState message={error} /> : null}
        <div className="zh-form-tabs" role="tablist">
          <button type="button" className={tab === 'data' ? 'is-active' : ''} onClick={() => setTab('data')}>
            {t('common.formTab.data')}
          </button>
          <button type="button" className={tab === 'list' ? 'is-active' : ''} onClick={() => setTab('list')}>
            {t(listTabLabelKey)}
          </button>
          {auditEntityType ? (
            <button type="button" className={tab === 'audit' ? 'is-active' : ''} onClick={() => setTab('audit')}>
              {t('common.formTab.audit')}
            </button>
          ) : null}
        </div>

        {tab === 'data' && (
          <>
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
              </ZHFormBody>
            </div>
          </>
        )}

        {tab === 'list' && (
          <>
            <div className="zh-mb-12">
              <ZHSearchBar
                searchQuery={listQuery}
                onSearch={setListQuery}
                onClearAll={() => setListQuery('')}
                filterValues={{}}
                placeholder={t(listSearchPlaceholderKey)}
                resultCount={listFiltered.length}
                entityLabel={t(listEntityLabelKey)}
                loading={loading}
                actionLabel={canCreate ? t(listNewActionKey ?? primaryCreateKey) : undefined}
                onAction={canCreate ? () => setTab('data') : undefined}
              />
            </div>
            {loading ? (
              <LoadingState />
            ) : items.length === 0 ? (
              <EmptyState message={t('common.noData')} />
            ) : listFiltered.length === 0 ? (
              <EmptyState message={t('common.listTab.noMatch')} />
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
                  {listFiltered.map((x) => (
                    <tr
                      key={x.id}
                      className={auditEntityType ? 'zh-tr-clickable' : undefined}
                      onClick={
                        auditEntityType
                          ? () => {
                              setAuditEntityId(x.id);
                              setAuditRefreshKey((k) => k + 1);
                              setTab('audit');
                            }
                          : undefined
                      }
                    >
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
          </>
        )}

        {tab === 'audit' && auditEntityType ? (
          auditEntityId ? (
            <EntityAuditPanel
              entityType={auditEntityType}
              entityId={auditEntityId}
              take={10}
              refreshKey={auditRefreshKey}
            />
          ) : (
            <EmptyState message={t('audit.pickRow')} />
          )
        ) : null}
      </TableCard>
    </PageShell>
  );
}

