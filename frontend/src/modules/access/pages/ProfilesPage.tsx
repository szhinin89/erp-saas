import { useCallback, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../../../i18n/i18n';
import { profileService, type Profile } from '../api/profileService';
import { ZHField, ZHBtn, ZHGrid, ZHFormSection, ZHToggle, ZHFormActions } from '../../../components/zh/ZHForm';
import { ZHModal } from '../../../components/zh/ZHModal';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { profileCreateSchema, type ProfileCreateFormValues } from '../../../schemas/access/profileSchema';
import { formatApiRequestError } from '../../lib/apiError';
import { applyServerErrors } from '../../lib/validationErrors';
import './ProfilesPage.css';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { useAuthStore } from '../../../store/authStore';

/* ── Permission groups mapped to CRUD columns ───────────────── */
type PermGroup = {
  module: string;
  planModule: string;
  view: string[];
  create: string[];
  edit: string[];
  delete: string[];
};

const MODULE_PERM_GROUPS: PermGroup[] = [
  {
    module: 'Clientes / Proveedores', planModule: 'sales',
    view:   ['masterdata.businesspartners.view'],
    create: ['masterdata.businesspartners.create'],
    edit:   ['masterdata.businesspartners.update'],
    delete: ['masterdata.businesspartners.disable'],
  },
  {
    module: 'Inventario', planModule: 'inventory',
    view:   ['inventory.Items.view', 'inventory.warehouses.view'],
    create: ['inventory.Items.create', 'inventory.warehouses.create'],
    edit:   ['inventory.Items.update', 'inventory.warehouses.update'],
    delete: ['inventory.Items.delete'],
  },
  {
    module: 'Configuración', planModule: 'access',
    view:   ['settings.branches.view', 'settings.company.view', 'settings.geography.view'],
    create: ['settings.branches.create'],
    edit:   ['settings.branches.update'],
    delete: ['settings.branches.delete'],
  },
  {
    module: 'Facturación Electrónica', planModule: 'access',
    view:   ['electronic-invoicing.view'],
    create: [],
    edit:   ['electronic-invoicing.configure'],
    delete: [],
  },
  {
    module: 'Administración', planModule: 'access',
    view:   ['admin.roles.view', 'admin.users.view', 'admin.activity.view'],
    create: [],
    edit:   [],
    delete: [],
  },
  {
    module: 'RIDE (Ventas)', planModule: 'sales',
    view:   ['ride.view'],
    create: [],
    edit:   ['ride.regenerate'],
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
  const { canShow, isAdminRole } = usePermissionsUi();
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
  const [rejectedPerms, setRejectedPerms] = useState<{ permissionKey: string; reason: string }[]>([]);

  /* permission state inside modal */
  const [permState, setPermState] = useState<Record<string, boolean>>(buildEmptyPermState);
  const [permLoading, setPermLoading] = useState(false);

  /* form */
  const { register, handleSubmit, reset, setError: setFieldError, formState: { errors } } = useForm<ProfileCreateFormValues>({
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
    setRejectedPerms([]);
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
      const upsertResult = await profileService.upsertPermissions(profileId, permItems);
      // Show any permissions rejected because they are not in the plan
      const planRejections = (upsertResult?.rejected ?? []).filter(
        (r) => r.rejectionCode === 'blocked_by_plan'
      );
      if (planRejections.length > 0) {
        setRejectedPerms(planRejections);
      }
      await loadProfiles();
      if (planRejections.length === 0) closeModal();
    } catch (err) {
      const applied = applyServerErrors(err, setFieldError, (msg) => setSaveError(msg));
      if (!applied) setSaveError(formatApiRequestError(err, { generic: t('profiles.error.create') }));
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

  if (!user || (!isAdminRole && !canManage)) {
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

      <ZHModal
        open={modalOpen}
        onClose={closeModal}
        size="2xl"
        title={editTarget ? t('profiles.form.edit') : t('profiles.form.create')}
        subtitle={t('profiles.subtitle')}
      >
            <form onSubmit={onSubmit}>
            <div className="prf-modal-body-grid">

              {/* ── Left column ─────────────────────────────────── */}
              <div className="prf-modal-column-main">

                {/* General info */}
                <div className="pg-section prf-modal-section-flush">
                  <div className="pg-section-header">
                    <span className="material-symbols-outlined prf-modal-section-icon">info</span>
                    {t('profiles.form.generalInfo')}
                  </div>
                  <ZHGrid cols={2}>
                    <ZHField label={t('profiles.form.name')} required error={errors.name?.message}>
                      <input className="zh-input" {...register('name')} placeholder="Ej. Analista de Finanzas Jr." />
                    </ZHField>
                    <ZHField label={t('profiles.form.status')}>
                      <select {...register('isActive', { setValueAs: (v) => v === 'true' || v === true })}>
                        <option value="true">{t('common.active')}</option>
                        <option value="false">{t('common.inactive')}</option>
                      </select>
                    </ZHField>
                  </ZHGrid>
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

                {/* Permissions */}
                <div className="pg-section prf-modal-section-flush">
                  <div className="pg-section-header">
                    <span className="material-symbols-outlined prf-modal-section-icon">rule</span>
                    {t('profiles.perms.title')}
                  </div>

                  {permLoading ? (
                    <p className="subtle prf-modal-loading">{t('common.loading')}</p>
                  ) : (
                    MODULE_PERM_GROUPS.map((group) => (
                      <ZHFormSection key={group.module} title={group.module}>
                        <div className="zh-stack zh-gap-8">
                          {(['view', 'create', 'edit', 'delete'] as const).map((col) => (
                            group[col].length > 0 && (
                              <ZHToggle
                                key={col}
                                label={t(`profiles.perms.${col}`)}
                                description={t(`profiles.perms.${col}.desc`)}
                                value={allOn(group[col], permState)}
                                onChange={(checked) => setPermState((s) => handleColumnToggle(group, col, checked, s))}
                              />
                            )
                          ))}
                        </div>
                      </ZHFormSection>
                    ))
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

            {/* Rejected permissions notice */}
            {rejectedPerms.length > 0 && (
              <div className="prf-modal-error-wrap">
                <ZHPageNotice
                  variant="warning"
                  message={`${rejectedPerms.length} permiso(s) no guardados -- fuera del plan`}
                  detail={rejectedPerms.map((r) => `${r.permissionKey}: ${r.reason}`).join('\n')}
                />
              </div>
            )}

            {/* Save error */}
            {saveError && (
              <div className="prf-modal-error-wrap">
                <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />
              </div>
            )}

            <ZHFormActions
              onCancel={closeModal}
              hideDraft
              saveButtonType="submit"
              disableSave={saving || permLoading}
              labels={{
                cancel: t('common.cancel'),
                save: saving ? t('common.saving') : t('profiles.form.saveProfile'),
              }}
            />
            </form>
      </ZHModal>
    </ErpPageTemplate>
  );
}
