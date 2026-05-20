import { useCallback, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import { profileService, type Profile } from '../services/profileService';
import { ZHField, ZHBtn } from '../components/zh/ZHForm';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { NoAccessPage } from '../components/PageShell';
import { profileCreateSchema, type ProfileCreateFormValues } from '../schemas/access/profileSchema';
import { formatApiRequestError } from '../modules/lib/apiError';

/* ── Permission groups mapped to CRUD columns ───────────────── */
type PermGroup = {
  module: string;
  view: string[];
  create: string[];
  edit: string[];
  delete: string[];
};

const MODULE_PERM_GROUPS: PermGroup[] = [
  {
    module: 'Ventas',
    view:   ['sales.customers.view', 'sales.invoices.view'],
    create: ['sales.customers.create', 'sales.invoices.create'],
    edit:   ['sales.customers.update'],
    delete: ['sales.customers.delete'],
  },
  {
    module: 'Inventario',
    view:   ['inventory.products.view', 'inventory.warehouses.view', 'inventory.brands.view', 'inventory.categories.view'],
    create: ['inventory.products.create', 'inventory.warehouses.create', 'inventory.brands.create', 'inventory.categories.create'],
    edit:   ['inventory.products.update', 'inventory.warehouses.update', 'inventory.brands.update', 'inventory.categories.update'],
    delete: ['inventory.products.delete', 'inventory.warehouses.delete', 'inventory.brands.delete', 'inventory.categories.delete'],
  },
  {
    module: 'Compras',
    view:   ['purchases.invoices.view', 'purchases.suppliers.view', 'purchases.credit-notes.view'],
    create: ['purchases.invoices.create', 'purchases.suppliers.create', 'compras.notas-proveedor.create'],
    edit:   ['compras.facturas.validate', 'compras.facturas.approve', 'purchases.suppliers.update', 'compras.notas-proveedor.approve'],
    delete: ['compras.facturas.reject', 'purchases.suppliers.delete'],
  },
  {
    module: 'Gastos',
    view:   ['expenses.invoices.view'],
    create: ['expenses.invoices.create'],
    edit:   ['gastos.facturas.validate', 'gastos.facturas.approve'],
    delete: ['gastos.facturas.reject'],
  },
  {
    module: 'Contabilidad',
    view:   ['finance.accounts.view', 'finance.journal.view', 'finance.config.view'],
    create: ['finance.accounts.create', 'finance.journal.create'],
    edit:   ['finance.journal.edit', 'finance.config.edit'],
    delete: [],
  },
  {
    module: 'Configuración',
    view:   ['settings.branches.view', 'admin.roles.view', 'admin.users.view'],
    create: ['settings.branches.create'],
    edit:   ['settings.branches.update'],
    delete: ['settings.branches.delete'],
  },
];

/* ── Helpers ────────────────────────────────────────────────── */
function allOn(keys: string[], state: Record<string, boolean>): boolean {
  return keys.length > 0 && keys.every((k) => !!state[k]);
}

function setKeys(keys: string[], value: boolean, state: Record<string, boolean>): Record<string, boolean> {
  const next = { ...state };
  for (const k of keys) next[k] = value;
  return next;
}

function countModulesWithAnyPerm(state: Record<string, boolean>): number {
  return MODULE_PERM_GROUPS.filter((g) =>
    [...g.view, ...g.create, ...g.edit, ...g.delete].some((k) => !!state[k])
  ).length;
}

function buildEmptyPermState(): Record<string, boolean> {
  const s: Record<string, boolean> = {};
  for (const g of MODULE_PERM_GROUPS) {
    for (const k of [...g.view, ...g.create, ...g.edit, ...g.delete]) s[k] = false;
  }
  return s;
}

/* ── Column toggle with cascade ─────────────────────────────── */
function handleColumnToggle(
  group: PermGroup,
  col: 'view' | 'create' | 'edit' | 'delete',
  checked: boolean,
  state: Record<string, boolean>,
): Record<string, boolean> {
  let next = setKeys(group[col], checked, state);
  if (checked) {
    // cascading: any action requires view
    if (col !== 'view') next = setKeys(group.view, true, next);
    // delete also requires edit
    if (col === 'delete') next = setKeys(group.edit, true, next);
  }
  return next;
}

/* ── Main component ─────────────────────────────────────────── */
export function ProfilesPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const canManage = usePermissionsStore((s) => s.has('admin.roles.view'));
  const isAdminOrSuper = user?.role === 'Admin' || user?.role === 'SuperAdmin';

  /* list state */
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState('');
  const [search, setSearch] = useState('');

  /* modal state */
  const [modalOpen, setModalOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<Profile | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');

  /* permission state inside modal */
  const [permState, setPermState] = useState<Record<string, boolean>>(buildEmptyPermState);
  const [permLoading, setPermLoading] = useState(false);

  /* form */
  const { register, handleSubmit, reset, formState: { errors } } = useForm<ProfileCreateFormValues>({
    resolver: zodResolver(profileCreateSchema),
    defaultValues: { name: '', description: '', isActive: true },
  });

  /* ── Load list ─────────────────────────────────────────────── */
  const loadProfiles = useCallback(async () => {
    setListLoading(true);
    setListError('');
    try {
      setProfiles(await profileService.list(false) ?? []);
    } catch (err) {
      setListError(formatApiRequestError(err, { generic: t('profiles.error.load') }));
    } finally {
      setListLoading(false);
    }
  }, [t]);

  useEffect(() => { void loadProfiles(); }, [loadProfiles]);

  /* ── Filtered list ─────────────────────────────────────────── */
  const filteredProfiles = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return profiles;
    return profiles.filter((p) =>
      `${p.name} ${p.description ?? ''}`.toLowerCase().includes(q)
    );
  }, [profiles, search]);

  /* ── Open modal (create or edit) ───────────────────────────── */
  const openCreate = () => {
    setEditTarget(null);
    reset({ name: '', description: '', isActive: true });
    setPermState(buildEmptyPermState());
    setSaveError('');
    setModalOpen(true);
  };

  const openEdit = async (p: Profile) => {
    setEditTarget(p);
    reset({ name: p.name, description: p.description ?? '', isActive: p.isActive });
    setSaveError('');
    setModalOpen(true);
    setPermLoading(true);
    try {
      const res = await profileService.getPermissions(p.id);
      const next = buildEmptyPermState();
      for (const item of res?.items ?? []) {
        if (item.permissionKey in next) next[item.permissionKey] = !!item.isAllowed;
      }
      setPermState(next);
    } catch {
      setPermState(buildEmptyPermState());
    } finally {
      setPermLoading(false);
    }
  };

  const closeModal = () => {
    setModalOpen(false);
    setEditTarget(null);
    setSaveError('');
  };

  /* ── Save (create or update) ───────────────────────────────── */
  const onSubmit = handleSubmit(async (values) => {
    setSaving(true);
    setSaveError('');
    try {
      let profileId: string;
      if (editTarget) {
        await profileService.update({ ...editTarget, name: values.name, description: values.description ?? null, isActive: values.isActive ?? true });
        profileId = editTarget.id;
      } else {
        const created = await profileService.create(values.name, values.description || null);
        profileId = created!.id;
      }
      const permItems = Object.entries(permState).map(([permissionKey, isAllowed]) => ({ permissionKey, isAllowed }));
      await profileService.upsertPermissions(profileId, permItems);
      await loadProfiles();
      closeModal();
    } catch (err) {
      setSaveError(formatApiRequestError(err, { generic: t('profiles.error.create') }));
    } finally {
      setSaving(false);
    }
  });

  /* ── Quick toggle active/inactive ──────────────────────────── */
  const toggleActive = async (p: Profile) => {
    setListError('');
    try {
      await profileService.update({ ...p, isActive: !p.isActive });
      await loadProfiles();
    } catch {
      setListError(t('profiles.error.update'));
    }
  };

  /* ── Permission progress (for right column) ─────────────────── */
  const modulesWithPerms = countModulesWithAnyPerm(permState);
  const progressPct = Math.round((modulesWithPerms / MODULE_PERM_GROUPS.length) * 100);
  const modulesWithoutPerms = MODULE_PERM_GROUPS.length - modulesWithPerms;

  if (!user || (!isAdminOrSuper && !canManage)) {
    return <NoAccessPage title={t('profiles.title')} />;
  }

  return (
    <div className="pg-page">
      {/* ── Header ─────────────────────────────────────────────── */}
      <div className="pg-header-row">
        <div>
          <p className="pg-kicker">{t('app.nav.group.configuracion')}</p>
          <h1 className="pg-title">{t('profiles.title')}</h1>
          <p className="pg-subtitle">{t('profiles.subtitle')}</p>
        </div>
        <ZHBtn variant="primary" size="md" type="button" onClick={openCreate}>
          + {t('profiles.list.newAction')}
        </ZHBtn>
      </div>

      {/* ── List error ─────────────────────────────────────────── */}
      {listError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={listError} />}

      {/* ── Table section ──────────────────────────────────────── */}
      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-search">
            <span className="material-symbols-outlined">search</span>
            <input
              className="zh-input"
              type="search"
              placeholder={t('common.zhList.searchPlaceholder')}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <span className="pg-result-count">
            {filteredProfiles.length} {t('common.zhList.entityLabel')}
          </span>
        </div>

        {listLoading ? (
          <p className="subtle" style={{ padding: 24 }}>{t('common.loading')}</p>
        ) : filteredProfiles.length === 0 ? (
          <p className="subtle" style={{ padding: 24 }}>{t('common.noData')}</p>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>{t('profiles.table.name')}</th>
                <th>{t('profiles.table.active')}</th>
                <th style={{ width: 180 }}>{t('common.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {filteredProfiles.map((p) => (
                <tr key={p.id}>
                  <td>
                    <strong>{p.name}</strong>
                    {p.description && <p className="subtle" style={{ margin: 0, fontSize: 12 }}>{p.description}</p>}
                  </td>
                  <td>
                    <span className={`zh-status zh-status--${p.isActive ? 'active' : 'inactive'}`}>
                      {p.isActive ? t('common.active') : t('common.inactive')}
                    </span>
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: 8 }}>
                      <ZHBtn variant="ghost" size="md" type="button" onClick={() => void openEdit(p)}>
                        {t('profiles.actions.permissions')}
                      </ZHBtn>
                      <ZHBtn variant="ghost" size="md" type="button" onClick={() => void toggleActive(p)}>
                        {p.isActive ? t('profiles.actions.disable') : t('profiles.actions.enable')}
                      </ZHBtn>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* ── Create / Edit modal ─────────────────────────────────── */}
      {modalOpen && (
        <div
          className="zh-modal-overlay"
          role="dialog"
          aria-modal="true"
          aria-label={editTarget ? t('profiles.form.edit') : t('profiles.form.create')}
          onClick={(e) => { if (e.target === e.currentTarget) closeModal(); }}
        >
          <div className="zh-modal" style={{ maxWidth: 960, width: '100%' }}>

            {/* Modal header */}
            <div className="zh-modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <div className="pg-kpi-icon--primary" style={{ width: 40, height: 40, borderRadius: 8, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <span className="material-symbols-outlined" style={{ fontSize: 22 }}>shield_person</span>
                </div>
                <div>
                  <h2 style={{ margin: 0, fontSize: 16, fontWeight: 600 }}>
                    {editTarget ? t('profiles.form.edit') : t('profiles.form.create')}
                  </h2>
                  <p className="subtle" style={{ margin: 0, fontSize: 12 }}>{t('profiles.subtitle')}</p>
                </div>
              </div>
              <button
                type="button"
                className="zh-btn zh-btn--ghost zh-btn--sm"
                onClick={closeModal}
                aria-label={t('common.close')}
              >
                <span className="material-symbols-outlined">close</span>
              </button>
            </div>

            {/* Modal body — two columns */}
            <div className="zh-modal-body" style={{ display: 'grid', gridTemplateColumns: '1fr 280px', gap: 24, alignItems: 'start' }}>

              {/* ── Left column ─────────────────────────────────── */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>

                {/* General info */}
                <form id="profile-form" onSubmit={onSubmit}>
                  <div className="pg-section" style={{ padding: 0, boxShadow: 'none', border: 'none' }}>
                    <div className="pg-section-header">
                      <span className="material-symbols-outlined" style={{ fontSize: 16 }}>info</span>
                      {t('profiles.form.generalInfo')}
                    </div>
                    <div className="pg-form-grid--2">
                      <ZHField label={t('profiles.form.name')} required error={errors.name?.message}>
                        <input className="zh-input" {...register('name')} placeholder="Ej. Analista de Finanzas Jr." />
                      </ZHField>
                      <ZHField label={t('profiles.form.status')}>
                        <select className="zh-input" {...register('isActive', { setValueAs: (v) => v === 'true' || v === true })}>
                          <option value="true">{t('common.active')}</option>
                          <option value="false">{t('common.inactive')}</option>
                        </select>
                      </ZHField>
                    </div>
                    <div style={{ marginTop: 12 }}>
                      <ZHField label={t('profiles.form.description')} error={errors.description?.message}>
                        <textarea
                          className="zh-input"
                          rows={3}
                          placeholder={t('profiles.form.descriptionPlaceholder')}
                          {...register('description')}
                        />
                      </ZHField>
                    </div>
                  </div>
                </form>

                {/* Permissions table */}
                <div className="pg-section" style={{ padding: 0, boxShadow: 'none', border: 'none' }}>
                  <div className="pg-section-header">
                    <span className="material-symbols-outlined" style={{ fontSize: 16 }}>rule</span>
                    {t('profiles.perms.title')}
                  </div>

                  {permLoading ? (
                    <p className="subtle" style={{ padding: 16 }}>{t('common.loading')}</p>
                  ) : (
                    <table className="table">
                      <thead>
                        <tr>
                          <th>{t('profiles.perms.module')}</th>
                          <th style={{ textAlign: 'center', width: 70 }}>{t('profiles.perms.view')}</th>
                          <th style={{ textAlign: 'center', width: 70 }}>{t('profiles.perms.create')}</th>
                          <th style={{ textAlign: 'center', width: 70 }}>{t('profiles.perms.edit')}</th>
                          <th style={{ textAlign: 'center', width: 70 }}>{t('profiles.perms.delete')}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {MODULE_PERM_GROUPS.map((group) => (
                          <tr key={group.module}>
                            <td><strong>{group.module}</strong></td>
                            {(['view', 'create', 'edit', 'delete'] as const).map((col) => (
                              <td key={col} style={{ textAlign: 'center' }}>
                                {group[col].length > 0 ? (
                                  <input
                                    type="checkbox"
                                    className="zh-inline-check"
                                    checked={allOn(group[col], permState)}
                                    onChange={(e) =>
                                      setPermState((s) => handleColumnToggle(group, col, e.target.checked, s))
                                    }
                                    style={{ width: 18, height: 18, cursor: 'pointer', accentColor: 'var(--color-primary)' }}
                                  />
                                ) : (
                                  <span className="subtle" style={{ fontSize: 12 }}>—</span>
                                )}
                              </td>
                            ))}
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                </div>
              </div>

              {/* ── Right column ────────────────────────────────── */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>

                {/* Tip box */}
                <div className="pg-section" style={{ background: 'var(--color-primary-lt, #e8f0f8)', border: '1px solid var(--color-primary, #3a5f84)22' }}>
                  <div style={{ display: 'flex', gap: 10, alignItems: 'flex-start' }}>
                    <span className="material-symbols-outlined" style={{ fontSize: 20, color: 'var(--color-primary)', flexShrink: 0 }}>lightbulb</span>
                    <div>
                      <p style={{ margin: '0 0 4px', fontWeight: 600, fontSize: 12, color: 'var(--color-primary)' }}>
                        {t('profiles.tip.title')}
                      </p>
                      <p className="subtle" style={{ margin: 0, fontSize: 12, lineHeight: 1.5 }}>
                        {t('profiles.tip.body')}
                      </p>
                    </div>
                  </div>
                </div>

                {/* Configuration status */}
                <div className="pg-section">
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
                    <p style={{ margin: 0, fontSize: 12, fontWeight: 600 }}>{t('profiles.form.configStatus')}</p>
                    <span style={{ fontSize: 12, fontWeight: 700, color: progressPct > 0 ? 'var(--success, #1a7a4a)' : 'var(--color-text-secondary)' }}>
                      {progressPct}%
                    </span>
                  </div>
                  <div style={{ height: 8, background: 'var(--color-border)', borderRadius: 99, overflow: 'hidden', marginBottom: 8 }}>
                    <div style={{ height: '100%', width: `${progressPct}%`, background: 'var(--success, #1a7a4a)', borderRadius: 99, transition: 'width 0.3s' }} />
                  </div>
                  {modulesWithoutPerms > 0 ? (
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <span className="material-symbols-outlined" style={{ fontSize: 14, color: 'var(--color-text-secondary)' }}>pending_actions</span>
                      <p className="subtle" style={{ margin: 0, fontSize: 12 }}>
                        {t('profiles.form.missingModules', { count: modulesWithoutPerms })}
                      </p>
                    </div>
                  ) : (
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <span className="material-symbols-outlined" style={{ fontSize: 14, color: 'var(--success, #1a7a4a)' }}>check_circle</span>
                      <p className="subtle" style={{ margin: 0, fontSize: 12 }}>{t('profiles.form.allModulesSet')}</p>
                    </div>
                  )}
                </div>

                {/* Security badge */}
                <div className="pg-section" style={{ textAlign: 'center', padding: '24px 16px' }}>
                  <div style={{ width: 56, height: 56, borderRadius: 14, background: 'var(--color-primary)', margin: '0 auto 12px', display: 'flex', alignItems: 'center', justifyContent: 'center', boxShadow: '0 4px 12px rgba(58,95,132,0.25)' }}>
                    <span className="material-symbols-outlined" style={{ fontSize: 28, color: '#fff' }}>security</span>
                  </div>
                  <p style={{ margin: '0 0 4px', fontSize: 11, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--color-primary)' }}>
                    Sistema Protegido
                  </p>
                  <p className="subtle" style={{ margin: 0, fontSize: 11, lineHeight: 1.4 }}>
                    Cifrado ZH Core v2.4 activo y verificado.
                  </p>
                </div>
              </div>
            </div>

            {/* Save error */}
            {saveError && (
              <div style={{ padding: '0 24px' }}>
                <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />
              </div>
            )}

            {/* Modal footer */}
            <div className="pg-actions-bar">
              <ZHBtn variant="ghost" size="md" type="button" onClick={closeModal} disabled={saving}>
                {t('common.cancel')}
              </ZHBtn>
              <ZHBtn variant="primary" size="md" type="submit" form="profile-form" disabled={saving || permLoading}>
                <span className="material-symbols-outlined" style={{ fontSize: 16 }}>save</span>
                {saving ? t('common.saving') : t('profiles.form.saveProfile')}
              </ZHBtn>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
