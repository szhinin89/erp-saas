import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { profileService, type Profile } from '../services/profileService';
import { PageShell, TableCard, EmptyState, LoadingState, NoAccessPage } from '../components/PageShell';
import { ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHFormAlert, ZHBtn } from '../components/zh/ZHForm';
import { ZHCardSection, ZHInlineRowRight, ZHActionsRow } from '../components/zh/ZHLayout';
import ZHSearchBar from '../components/shared/ZHSearchBar';
import { ZHFormCard } from '../components/zh/ZHFormCard';
import './ProfilesPage.css';

type ProfileTab = 'data' | 'perms' | 'list';

export function ProfilesPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');

  const [items, setItems] = useState<Profile[]>([]);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [tab, setTab] = useState<ProfileTab>('data');
  const [listQuery, setListQuery] = useState('');

  const [selected, setSelected] = useState<Profile | null>(null);
  const [permLoading, setPermLoading] = useState(false);
  const [permError, setPermError] = useState('');
  const [permSaving, setPermSaving] = useState(false);
  const [permState, setPermState] = useState<Record<string, boolean>>({});
  const createFormRef = useRef<HTMLFormElement>(null);

  const permissionCatalog = useMemo(
    () => [
      { key: 'catalog.products.view', label: t('profiles.perms.catalog.products.view') },
      { key: 'catalog.products.create', label: t('profiles.perms.catalog.products.create') },
      { key: 'catalog.products.update', label: t('profiles.perms.catalog.products.update') },
      { key: 'catalog.products.delete', label: t('profiles.perms.catalog.products.delete') },
      { key: 'catalog.brands.view', label: t('profiles.perms.catalog.brands.view') },
      { key: 'catalog.brands.create', label: t('profiles.perms.catalog.brands.create') },
      { key: 'catalog.brands.update', label: t('profiles.perms.catalog.brands.update') },
      { key: 'catalog.brands.delete', label: t('profiles.perms.catalog.brands.delete') },
      { key: 'catalog.productTypes.view', label: t('profiles.perms.catalog.productTypes.view') },
      { key: 'catalog.productTypes.create', label: t('profiles.perms.catalog.productTypes.create') },
      { key: 'catalog.productTypes.update', label: t('profiles.perms.catalog.productTypes.update') },
      { key: 'catalog.productTypes.delete', label: t('profiles.perms.catalog.productTypes.delete') },
      { key: 'catalog.units.view', label: t('profiles.perms.catalog.units.view') },
      { key: 'catalog.units.create', label: t('profiles.perms.catalog.units.create') },
      { key: 'catalog.units.update', label: t('profiles.perms.catalog.units.update') },
      { key: 'catalog.units.delete', label: t('profiles.perms.catalog.units.delete') },
      { key: 'catalog.taxRates.view', label: t('profiles.perms.catalog.taxRates.view') },
      { key: 'catalog.taxRates.create', label: t('profiles.perms.catalog.taxRates.create') },
      { key: 'catalog.taxRates.update', label: t('profiles.perms.catalog.taxRates.update') },
      { key: 'catalog.taxRates.delete', label: t('profiles.perms.catalog.taxRates.delete') },
      { key: 'catalog.tariffs.view', label: t('profiles.perms.catalog.tariffs.view') },
      { key: 'catalog.tariffs.create', label: t('profiles.perms.catalog.tariffs.create') },
      { key: 'catalog.tariffs.update', label: t('profiles.perms.catalog.tariffs.update') },
      { key: 'catalog.tariffs.delete', label: t('profiles.perms.catalog.tariffs.delete') },
      { key: 'catalog.productLines.view', label: t('profiles.perms.catalog.productLines.view') },
      { key: 'catalog.productLines.create', label: t('profiles.perms.catalog.productLines.create') },
      { key: 'catalog.productLines.update', label: t('profiles.perms.catalog.productLines.update') },
      { key: 'catalog.productLines.delete', label: t('profiles.perms.catalog.productLines.delete') },
      { key: 'catalog.categories.view', label: t('profiles.perms.catalog.categories.view') },
      { key: 'catalog.categories.create', label: t('profiles.perms.catalog.categories.create') },
      { key: 'catalog.categories.update', label: t('profiles.perms.catalog.categories.update') },
      { key: 'catalog.categories.delete', label: t('profiles.perms.catalog.categories.delete') },
      { key: 'catalog.subcategories.view', label: t('profiles.perms.catalog.subcategories.view') },
      { key: 'catalog.subcategories.create', label: t('profiles.perms.catalog.subcategories.create') },
      { key: 'catalog.subcategories.update', label: t('profiles.perms.catalog.subcategories.update') },
      { key: 'catalog.subcategories.delete', label: t('profiles.perms.catalog.subcategories.delete') },
      { key: 'saas.branches.view', label: t('profiles.perms.saas.branches.view') },
      { key: 'saas.branches.create', label: t('profiles.perms.saas.branches.create') },
      { key: 'saas.branches.update', label: t('profiles.perms.saas.branches.update') },
      { key: 'saas.branches.delete', label: t('profiles.perms.saas.branches.delete') },
    ],
    [t]
  );

  const refresh = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await profileService.list(false));
    } catch {
      setError(t('profiles.error.load'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    let cancelled = false;
    void Promise.resolve().then(async () => {
      if (!cancelled) await refresh();
    });
    return () => {
      cancelled = true;
    };
  }, [refresh]);

  const listFiltered = useMemo(() => {
    const q = listQuery.trim().toLowerCase();
    if (!q) return items;
    return items.filter((p) =>
      `${p.name} ${p.description ?? ''} ${p.id}`.toLowerCase().includes(q)
    );
  }, [items, listQuery]);

  const create = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await profileService.create(name, description);
      setName('');
      setDescription('');
      await refresh();
      setTab('list');
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('profiles.error.create');
      setError(msg);
    }
  };

  const toggle = async (p: Profile) => {
    setError('');
    try {
      await profileService.update({ ...p, isActive: !p.isActive });
      await refresh();
    } catch {
      setError(t('profiles.error.update'));
    }
  };

  const loadPermissions = useCallback(
    async (profileId: string) => {
      setPermError('');
      setPermLoading(true);
      try {
        const res = await profileService.getPermissions(profileId);
        const next: Record<string, boolean> = {};
        for (const p of res?.items ?? []) next[p.permissionKey] = !!p.isAllowed;
        for (const def of permissionCatalog) {
          if (next[def.key] === undefined) next[def.key] = false;
        }
        setPermState(next);
      } catch (err: unknown) {
        const msg =
          (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          t('profiles.perms.error.load');
        setPermError(msg);
      } finally {
        setPermLoading(false);
      }
    },
    [permissionCatalog, t]
  );

  const selectProfile = async (p: Profile) => {
    setSelected(p);
    setTab('perms');
    await loadPermissions(p.id);
  };

  const savePermissions = async () => {
    if (!selected) return;
    setPermError('');
    setPermSaving(true);
    try {
      const permItems = Object.entries(permState).map(([permissionKey, isAllowed]) => ({ permissionKey, isAllowed }));
      await profileService.upsertPermissions(selected.id, permItems);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('profiles.perms.error.save');
      setPermError(msg);
    } finally {
      setPermSaving(false);
    }
  };

  if (!user || (user.role !== 'Admin' && user.role !== 'SuperAdmin')) {
    return <NoAccessPage title={t('profiles.title')} />;
  }

  return (
    <PageShell
      kicker={t('app.nav.group.access')}
      title={t('profiles.title')}
      subtitle={t('profiles.subtitle')}
      action={
        tab === 'data' ? (
          <ZHBtn variant="primary" size="md" type="button" disabled={loading} onClick={() => createFormRef.current?.requestSubmit()}>
            {t('profiles.form.create')}
          </ZHBtn>
        ) : tab === 'perms' && selected ? (
          <ZHBtn variant="primary" size="md" type="button" onClick={() => void savePermissions()} disabled={permLoading || permSaving}>
            {permSaving ? t('common.saving') : t('common.saveChanges')}
          </ZHBtn>
        ) : undefined
      }
    >
      <TableCard>
        {error ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={error} /> : null}
        <div className="zh-form-tabs" role="tablist">
          <button type="button" className={tab === 'data' ? 'is-active' : ''} onClick={() => setTab('data')}>
            {t('common.formTab.data')}
          </button>
          <button type="button" className={tab === 'perms' ? 'is-active' : ''} onClick={() => setTab('perms')}>
            {t('profiles.tab.permissions')}
          </button>
          <button type="button" className={tab === 'list' ? 'is-active' : ''} onClick={() => setTab('list')}>
            {t('profiles.tabList')}
          </button>
        </div>

        {tab === 'data' && (
          <ZHFormCard ref={createFormRef} hideHeader title={t('profiles.title')} subtitle={t('profiles.subtitle')} onSubmit={create}>
            <input type="hidden" name="tenantId" value={tenantId} />

            <ZHFormSection title={t('profiles.form.create')}>
              <ZHGrid cols={2}>
                <ZHField label={t('profiles.form.name')} required>
                  <input value={name} onChange={(e) => setName(e.target.value)} required disabled={loading} />
                </ZHField>
                <ZHField label={t('profiles.form.description')}>
                  <input value={description} onChange={(e) => setDescription(e.target.value)} disabled={loading} />
                </ZHField>
              </ZHGrid>
            </ZHFormSection>
          </ZHFormCard>
        )}

        {tab === 'list' && (
          <>
            <div className="zh-mb-12">
              <ZHSearchBar
                searchQuery={listQuery}
                onSearch={setListQuery}
                onClearAll={() => setListQuery('')}
                filterValues={{}}
                placeholder={t('common.zhList.searchPlaceholder')}
                resultCount={listFiltered.length}
                entityLabel={t('common.zhList.entityLabel')}
                loading={loading}
                actionLabel={t('profiles.list.newAction')}
                onAction={() => setTab('data')}
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
                    <th>{t('profiles.table.name')}</th>
                    <th>{t('profiles.table.active')}</th>
                    <th>{t('profiles.table.id')}</th>
                  </tr>
                </thead>
                <tbody>
                  {listFiltered.map((p) => (
                    <tr key={p.id}>
                      <td>
                        <div className="zh-card-section-title">{p.name}</div>
                        {p.description ? <div className="zh-text-muted zh-text-xs">{p.description}</div> : null}
                      </td>
                      <td>
                        <ZHActionsRow>
                          <ZHBtn variant="secondary" onClick={() => toggle(p)} type="button">
                            {p.isActive ? t('profiles.actions.disable') : t('profiles.actions.enable')}
                          </ZHBtn>
                          <ZHBtn variant="ghost" onClick={() => void selectProfile(p)} type="button">
                            {t('profiles.actions.permissions')}
                          </ZHBtn>
                        </ZHActionsRow>
                      </td>
                      <td className="mono">{p.id}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}

        {tab === 'perms' && (
          <>
            {!selected ? (
              <EmptyState message={t('profiles.perms.pickFromList')} />
            ) : (
              <ZHFormBody standalone>
                <ZHCardSection
                  title={`${t('profiles.perms.title')} — ${selected.name}`}
                  right={
                    <ZHInlineRowRight>
                      <ZHBtn variant="ghost" size="md" type="button" onClick={() => setSelected(null)} disabled={permSaving}>
                        {t('common.cancel')}
                      </ZHBtn>
                    </ZHInlineRowRight>
                  }
                >
                  <div className="subtle">{t('profiles.perms.subtitle')}</div>
                </ZHCardSection>

                {permError ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={permError} /> : null}
                {permLoading ? (
                  <LoadingState />
                ) : (
                  <div className="profiles-permsGrid">
                    {permissionCatalog.map((def) => (
                      <label key={def.key} className="profiles-permItem">
                        <input
                          type="checkbox"
                          checked={!!permState[def.key]}
                          onChange={(e) => setPermState((s) => ({ ...s, [def.key]: e.target.checked }))}
                        />
                        <div className="profiles-permText">
                          <span className="profiles-permLabel">{def.label}</span>
                          <span className="mono profiles-permKey">{def.key}</span>
                        </div>
                      </label>
                    ))}
                  </div>
                )}
              </ZHFormBody>
            )}
          </>
        )}
      </TableCard>
    </PageShell>
  );
}
