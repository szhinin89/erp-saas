import { useCallback, useEffect, useMemo, useState } from 'react';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { profileService, type Profile } from '../services/profileService';

export function ProfilesPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);

  const [items, setItems] = useState<Profile[]>([]);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const [selected, setSelected] = useState<Profile | null>(null);
  const [permLoading, setPermLoading] = useState(false);
  const [permError, setPermError] = useState('');
  const [permSaving, setPermSaving] = useState(false);
  const [permState, setPermState] = useState<Record<string, boolean>>({});

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
    // Defer para evitar regla eslint react-hooks/set-state-in-effect
    void Promise.resolve().then(async () => {
      if (!cancelled) await refresh();
    });
    return () => { cancelled = true; };
  }, [refresh]);

  const create = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await profileService.create(name, description);
      setName('');
      setDescription('');
      await refresh();
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

  const loadPermissions = useCallback(async (profileId: string) => {
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
  }, [permissionCatalog, t]);

  const selectProfile = async (p: Profile) => {
    setSelected(p);
    await loadPermissions(p.id);
  };

  const savePermissions = async () => {
    if (!selected) return;
    setPermError('');
    setPermSaving(true);
    try {
      const items = Object.entries(permState).map(([permissionKey, isAllowed]) => ({ permissionKey, isAllowed }));
      await profileService.upsertPermissions(selected.id, items);
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
    return <div className="page-shell"><h1>{t('profiles.title')}</h1><p>{t('profiles.noAccess')}</p></div>;
  }

  return (
    <div className="page-shell">
      <h1 className="page-title">{t('profiles.title')}</h1>
      <p className="page-subtitle">{t('profiles.subtitle')}</p>

      <form className="companies-form" onSubmit={create}>
        <div className="grid">
          <input placeholder={t('profiles.form.name')} value={name} onChange={(e) => setName(e.target.value)} required />
          <input placeholder={t('profiles.form.description')} value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
        {error && <p className="form-error">{error}</p>}
        <button className="primary-btn" disabled={loading}>{t('profiles.form.create')}</button>
      </form>

      <div className="companies-list">
        <div className="table">
          <div className="row head">
            <div>{t('profiles.table.name')}</div>
            <div>{t('profiles.table.active')}</div>
            <div>{t('profiles.table.id')}</div>
          </div>
          {items.map((p) => (
            <div key={p.id} className="row">
              <div>
                <div style={{ fontWeight: 800 }}>{p.name}</div>
                {p.description && <div style={{ color: 'rgba(30,41,59,.75)', fontSize: 12 }}>{p.description}</div>}
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <button className="secondary-btn" onClick={() => toggle(p)} type="button">
                  {p.isActive ? t('profiles.actions.disable') : t('profiles.actions.enable')}
                </button>
                <button className="secondary-btn" onClick={() => void selectProfile(p)} type="button">
                  {t('profiles.actions.permissions')}
                </button>
              </div>
              <div className="mono">{p.id}</div>
            </div>
          ))}
        </div>
      </div>

      {selected && (
        <div className="companies-list" style={{ marginTop: 16 }}>
          <div className="table-card">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12 }}>
              <div>
                <div style={{ fontWeight: 900, fontSize: 16 }}>
                  {t('profiles.perms.title')} — {selected.name}
                </div>
                <div style={{ color: 'rgba(30,41,59,.75)', fontSize: 12 }}>
                  {t('profiles.perms.subtitle')}
                </div>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <button className="secondary-btn" type="button" onClick={() => setSelected(null)} disabled={permSaving}>
                  {t('common.cancel')}
                </button>
                <button className="primary-btn" type="button" onClick={() => void savePermissions()} disabled={permLoading || permSaving}>
                  {permSaving ? t('common.saving') : t('profiles.perms.save')}
                </button>
              </div>
            </div>

            {permError && <p className="form-error" style={{ marginTop: 8 }}>{permError}</p>}
            {permLoading ? (
              <div style={{ marginTop: 12 }}>{t('common.loading')}</div>
            ) : (
              <div style={{ marginTop: 12, display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 8 }}>
                {permissionCatalog.map((def) => (
                  <label
                    key={def.key}
                    style={{ display: 'flex', alignItems: 'center', gap: 10, padding: 10, border: '1px solid rgba(148,163,184,.35)', borderRadius: 12 }}
                  >
                    <input
                      type="checkbox"
                      checked={!!permState[def.key]}
                      onChange={(e) => setPermState((s) => ({ ...s, [def.key]: e.target.checked }))}
                    />
                    <div style={{ display: 'flex', flexDirection: 'column' }}>
                      <span style={{ fontWeight: 700 }}>{def.label}</span>
                      <span className="mono" style={{ fontSize: 12, opacity: 0.7 }}>{def.key}</span>
                    </div>
                  </label>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

