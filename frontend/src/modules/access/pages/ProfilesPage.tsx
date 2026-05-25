import { useCallback, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../../../i18n/i18n';
import { profileService, type Profile } from '../api/profileService';
import { ZHField, ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { profileCreateSchema, type ProfileCreateFormValues } from '../../../schemas/access/profileSchema';
import { formatApiRequestError } from '../../lib/apiError';
import './ProfilesPage.css';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { useAuthStore } from '../../../store/authStore';

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
    module: 'Clientes / Proveedores',
    view:   ['masterdata.businesspartners.view'],
    create: ['masterdata.businesspartners.create'],
    edit:   ['masterdata.businesspartners.update'],
    delete: ['masterdata.businesspartners.disable'],
  },
  {
    module: 'Ventas',
    view:   ['sales.invoices.view', 'sales.quotes.view', 'sales.orders.view', 'sales.credit-notes.view'],
    create: ['sales.invoices.create', 'sales.quotes.create', 'sales.orders.create', 'sales.credit-notes.create'],
    edit:   ['sales.invoices.update', 'sales.quotes.update', 'sales.orders.update', 'sales.credit-notes.send'],
    delete: ['sales.invoices.void'],
  },
  {
    module: 'Compras',
    view:   ['purchases.invoices.view', 'purchases.orders.view', 'purchases.credit-notes.view', 'purchases.withholding-issued.view'],
    create: ['purchases.invoices.create', 'purchases.orders.create'],
    edit:   ['purchases.orders.approve', 'purchases.orders.cancel'],
    delete: [],
  },
  {
    module: 'Inventario',
    view:   ['inventory.products.view', 'inventory.warehouses.view', 'inventory.stock.view', 'inventory.kardex.view', 'inventory.transfers.view', 'inventory.adjustments.view'],
    create: ['inventory.products.create', 'inventory.warehouses.create', 'inventory.transfers.create', 'inventory.adjustments.create'],
    edit:   ['inventory.products.update', 'inventory.warehouses.update', 'inventory.transfers.approve', 'inventory.adjustments.approve'],
    delete: ['inventory.products.delete'],
  },
  {
    module: 'Gastos',
    view:   ['expenses.invoices.view'],
    create: ['expenses.invoices.create'],
    edit:   [],
    delete: [],
  },
  {
    module: 'Contabilidad',
    view:   ['finance.accounts.view', 'finance.journal.view', 'finance.config.view'],
    create: ['finance.accounts.create'],
    edit:   ['finance.accounts.edit', 'finance.config.edit'],
    delete: [],
  },
  {
    module: 'Configuración',
    view:   ['settings.branches.view', 'settings.company.view', 'settings.sri.view', 'settings.ride.view', 'settings.geography.view'],
    create: ['settings.branches.create'],
    edit:   ['settings.branches.update'],
    delete: ['settings.branches.delete'],
  },
  {
    module: 'Administración',
    view:   ['admin.roles.view', 'admin.users.view', 'admin.activity.view'],
    create: [],
    edit:   [],
    delete: [],
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
  const { canShow, isTenantAdmin } = usePermissionsUi();
  const canManage = canShow('admin.roles.view');

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

  if (!user || (!isTenantAdmin && !canManage)) {
    return <NoAccessPage title={t('profiles.title')} />;
  }

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.configuracion')}
      title={t('profiles.title')}
      subtitle={t('profiles.subtitle')}
      action={
        <ZHBtn variant="primary" size="md" type="button" onClick={openCreate}>
          + {t('profiles.list.newAction')}
        </ZHBtn>
      }
    >
      {listError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={listError} /> : null}

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
          <p className="subtle pg-state-pad">{t('common.loading')}</p>
        ) : filteredProfiles.length === 0 ? (
          <p className="subtle pg-state-pad">{t('common.noData')}</p>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>{t('profiles.table.name')}</th>
                <th>{t('profiles.table.active')}</th>
                <th className="pg-th-actions">{t('common.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {filteredProfiles.map((p) => (
                <tr key={p.id}>
                  <td>
                    <strong>{p.name}</strong>
                    {p.description ? <p className="subtle pg-desc-subtle">{p.description}</p> : null}
                  </td>
                  <td>
                    <span className={`zh-status zh-status--${p.isActive ? 'active' : 'inactive'}`}>
                      {p.isActive ? t('common.active') : t('common.inactive')}
                    </span>
                  </td>
                  <td>
                    <div className="prf-row-actions">
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
          <div className="zh-modal prf-modal--wide">

            {/* Modal header */}
            <div className="zh-modal-header">
              <div className="prf-modal-header-main">
                <div className="pg-kpi-icon--primary prf-modal-header-icon">
                  <span className="material-symbols-outlined">shield_person</span>
                </div>
                <div>
                  <h2 className="prf-modal-header-title">
                    {editTarget ? t('profiles.form.edit') : t('profiles.form.create')}
                  </h2>
                  <p className="subtle prf-modal-header-subtitle">{t('profiles.subtitle')}</p>
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
            <div className="zh-modal-body prf-modal-body-grid">

              {/* ── Left column ─────────────────────────────────── */}
              <div className="prf-modal-column-main">

                {/* General info */}
                <form id="profile-form" onSubmit={onSubmit}>
                  <div className="pg-section prf-modal-section-flush">
                    <div className="pg-section-header">
                      <span className="material-symbols-outlined prf-modal-section-icon">info</span>
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
                    <div className="prf-modal-field-offset">
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
                <div className="pg-section prf-modal-section-flush">
                  <div className="pg-section-header">
                    <span className="material-symbols-outlined prf-modal-section-icon">rule</span>
                    {t('profiles.perms.title')}
                  </div>

                  {permLoading ? (
                    <p className="subtle prf-modal-loading">{t('common.loading')}</p>
                  ) : (
                    <div className="prf-matrix-wrap">
                      <table className="table prf-matrix-table">
                        <thead>
                          <tr>
                            <th>{t('profiles.perms.module')}</th>
                            <th className="prf-matrix-th-action">{t('profiles.perms.view')}</th>
                            <th className="prf-matrix-th-action">{t('profiles.perms.create')}</th>
                            <th className="prf-matrix-th-action">{t('profiles.perms.edit')}</th>
                            <th className="prf-matrix-th-action">{t('profiles.perms.delete')}</th>
                          </tr>
                        </thead>
                        <tbody>
                          {MODULE_PERM_GROUPS.map((group) => (
                            <tr key={group.module}>
                              <td><strong>{group.module}</strong></td>
                              {(['view', 'create', 'edit', 'delete'] as const).map((col) => (
                                <td key={col} className="prf-matrix-cell-action">
                                  {group[col].length > 0 ? (
                                    <input
                                      type="checkbox"
                                      className="zh-inline-check prf-matrix-check"
                                      checked={allOn(group[col], permState)}
                                      onChange={(e) =>
                                        setPermState((s) => handleColumnToggle(group, col, e.target.checked, s))
                                      }
                                    />
                                  ) : (
                                    <span className="subtle prf-matrix-empty">—</span>
                                  )}
                                </td>
                              ))}
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              </div>

              {/* ── Right column ────────────────────────────────── */}
              <div className="prf-modal-column-aside">

                {/* Tip box */}
                <div className="pg-section prf-modal-tip">
                  <div className="prf-modal-tip-row">
                    <span className="material-symbols-outlined prf-modal-tip-icon">lightbulb</span>
                    <div>
                      <p className="prf-modal-tip-title">
                        {t('profiles.tip.title')}
                      </p>
                      <p className="subtle prf-modal-tip-body">
                        {t('profiles.tip.body')}
                      </p>
                    </div>
                  </div>
                </div>

                {/* Configuration status */}
                <div className="pg-section prf-modal-status">
                  <div className="prf-modal-status-head">
                    <p className="prf-modal-status-label">{t('profiles.form.configStatus')}</p>
                    <span
                      className={
                        progressPct > 0
                          ? 'prf-modal-progress-value prf-modal-progress-value--active'
                          : 'prf-modal-progress-value'
                      }
                    >
                      {progressPct}%
                    </span>
                  </div>
                  <progress
                    className="prf-modal-progress"
                    value={modulesWithPerms}
                    max={MODULE_PERM_GROUPS.length}
                    aria-label={t('profiles.form.configStatus')}
                  />
                  {modulesWithoutPerms > 0 ? (
                    <div className="prf-modal-status-row">
                      <span className="material-symbols-outlined prf-modal-status-row-icon">pending_actions</span>
                      <p className="subtle prf-modal-status-row-text">
                        {t('profiles.form.missingModules', { count: modulesWithoutPerms })}
                      </p>
                    </div>
                  ) : (
                    <div className="prf-modal-status-row">
                      <span className="material-symbols-outlined prf-modal-status-row-icon prf-modal-status-row-icon--success">
                        check_circle
                      </span>
                      <p className="subtle prf-modal-status-row-text">{t('profiles.form.allModulesSet')}</p>
                    </div>
                  )}
                </div>

                {/* Security badge */}
                <div className="pg-section prf-modal-security">
                  <div className="prf-modal-security-icon-wrap">
                    <span className="material-symbols-outlined prf-modal-security-icon">security</span>
                  </div>
                  <p className="prf-modal-security-title">
                    Sistema Protegido
                  </p>
                  <p className="subtle prf-modal-security-desc">
                    Cifrado ZH Core v2.4 activo y verificado.
                  </p>
                </div>
              </div>
            </div>

            {/* Save error */}
            {saveError && (
              <div className="prf-modal-error-wrap">
                <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />
              </div>
            )}

            {/* Modal footer */}
            <div className="pg-actions-bar prf-modal-actions">
              <ZHBtn variant="ghost" size="md" type="button" onClick={closeModal} disabled={saving}>
                {t('common.cancel')}
              </ZHBtn>
              <ZHBtn variant="primary" size="md" type="submit" form="profile-form" disabled={saving || permLoading}>
                <span className="material-symbols-outlined prf-modal-save-icon">save</span>
                {saving ? t('common.saving') : t('profiles.form.saveProfile')}
              </ZHBtn>
            </div>
          </div>
        </div>
      )}
    </ErpPageTemplate>
  );
}
