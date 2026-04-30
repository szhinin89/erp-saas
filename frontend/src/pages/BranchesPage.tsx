import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge } from '../components/PageShell';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { useAuthStore } from '../store/authStore';
import {
  branchService,
  type BranchDetailDto,
  type BranchDto,
  type CatalogActiveStatus,
  type GeographyItemDto,
} from '../services/branchService';
import '../components/Modal.css';
import './BranchesPage.css';

type FormState = {
  name: string;
  address: string;
  reference: string;
  phones: string;
  countryId: string;
  provinceId: string;
  cantonId: string;
  parishId: string;
  latitude: string;
  longitude: string;
  rechargeOption: string;
  isActive: boolean;
  isMainBranch: boolean;
};

const emptyForm = (): FormState => ({
  name: '',
  address: '',
  reference: '',
  phones: '',
  countryId: '',
  provinceId: '',
  cantonId: '',
  parishId: '',
  latitude: '',
  longitude: '',
  rechargeOption: '',
  isActive: true,
  isMainBranch: false,
});

function fromDto(d: BranchDto | BranchDetailDto): FormState {
  return {
    name: d.name,
    address: d.address,
    reference: d.reference ?? '',
    phones: d.phones ?? '',
    countryId: d.countryId ?? '',
    provinceId: d.provinceId ?? '',
    cantonId: d.cantonId ?? '',
    parishId: d.parishId ?? '',
    latitude: d.latitude ?? '',
    longitude: d.longitude ?? '',
    rechargeOption: d.rechargeOption ?? '',
    isActive: d.isActive,
    isMainBranch: d.isMainBranch,
  };
}

export function BranchesPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';

  const canView = isAdmin || hasPerm('saas.branches.view');
  const canCreate = isAdmin || hasPerm('saas.branches.create');
  const canUpdate = isAdmin || hasPerm('saas.branches.update');
  const canDelete = isAdmin || hasPerm('saas.branches.delete');

  const dialogRef = useRef<HTMLDialogElement>(null);
  const [items, setItems] = useState<BranchDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [activeStatus, setActiveStatus] = useState<CatalogActiveStatus>('all');
  const [searchDraft, setSearchDraft] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');

  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [audit, setAudit] = useState<Pick<
    BranchDetailDto,
    'createdAt' | 'updatedAt' | 'createdBy' | 'updatedBy'
  > | null>(null);

  const [countries, setCountries] = useState<GeographyItemDto[]>([]);
  const [provinces, setProvinces] = useState<GeographyItemDto[]>([]);
  const [cantons, setCantons] = useState<GeographyItemDto[]>([]);
  const [parishes, setParishes] = useState<GeographyItemDto[]>([]);
  const [loadingProvinces, setLoadingProvinces] = useState(false);
  const [loadingCantons, setLoadingCantons] = useState(false);
  const [loadingParishes, setLoadingParishes] = useState(false);
  /** 'new' | 'edit' cuando el modal está abierto; sincroniza país por defecto si los países cargan tarde. */
  const [branchDialogMode, setBranchDialogMode] = useState<'closed' | 'new' | 'edit'>('closed');
  const [geoBootstrapError, setGeoBootstrapError] = useState('');

  const fetchList = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await branchService.list(activeStatus, appliedSearch.trim() || undefined));
    } catch {
      setError(t('branches.error.load'));
    } finally {
      setLoading(false);
    }
  }, [activeStatus, appliedSearch, t]);

  useEffect(() => {
    let cancelled = false;
    void Promise.resolve().then(async () => {
      if (!cancelled) await fetchList();
    });
    return () => {
      cancelled = true;
    };
  }, [fetchList]);

  useEffect(() => {
    let cancelled = false;
    void branchService
      .countries()
      .then((c) => {
        if (!cancelled) {
          setCountries(c ?? []);
          setGeoBootstrapError('');
        }
      })
      .catch(() => {
        if (!cancelled) {
          setCountries([]);
          setGeoBootstrapError(t('branches.error.geography'));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [t]);

  const loadProvinces = useCallback(async (countryId: string) => {
    if (!countryId) {
      setProvinces([]);
      return;
    }
    setProvinces([]);
    setLoadingProvinces(true);
    try {
      setProvinces(await branchService.provinces(countryId));
    } catch {
      setProvinces([]);
    } finally {
      setLoadingProvinces(false);
    }
  }, []);

  const loadCantons = useCallback(async (provinceId: string) => {
    if (!provinceId) {
      setCantons([]);
      return;
    }
    setCantons([]);
    setLoadingCantons(true);
    try {
      setCantons(await branchService.cantons(provinceId));
    } catch {
      setCantons([]);
    } finally {
      setLoadingCantons(false);
    }
  }, []);

  const loadParishes = useCallback(async (cantonId: string) => {
    if (!cantonId) {
      setParishes([]);
      return;
    }
    setParishes([]);
    setLoadingParishes(true);
    try {
      setParishes(await branchService.parishes(cantonId));
    } catch {
      setParishes([]);
    } finally {
      setLoadingParishes(false);
    }
  }, []);

  useEffect(() => {
    if (branchDialogMode !== 'new') return;
    if (form.countryId) return;
    const ec = countries.find((c) => c.id === 'EC');
    if (!ec) return;
    let cancelled = false;
    void Promise.resolve().then(() => {
      if (cancelled) return;
      setForm((s) => ({ ...s, countryId: ec.id }));
      void loadProvinces(ec.id);
    });
    return () => {
      cancelled = true;
    };
  }, [branchDialogMode, countries, form.countryId, loadProvinces]);

  const openNew = () => {
    setEditingId(null);
    const defaultCountry = countries.some((c) => c.id === 'EC') ? 'EC' : '';
    setForm({ ...emptyForm(), countryId: defaultCountry });
    setAudit(null);
    setCantons([]);
    setParishes([]);
    setProvinces([]);
    setBranchDialogMode('new');
    if (defaultCountry) {
      void loadProvinces(defaultCountry);
    }
    dialogRef.current?.showModal();
  };

  const openEdit = async (id: string) => {
    setError('');
    try {
      const d = await branchService.getById(id);
      setEditingId(id);
      setForm(fromDto(d));
      setAudit({
        createdAt: d.createdAt,
        updatedAt: d.updatedAt,
        createdBy: d.createdBy,
        updatedBy: d.updatedBy,
      });
      await loadProvinces(d.countryId ?? '');
      await loadCantons(d.provinceId ?? '');
      await loadParishes(d.cantonId ?? '');
      setBranchDialogMode('edit');
      dialogRef.current?.showModal();
    } catch {
      setError(t('branches.error.loadOne'));
    }
  };

  const onCountryChange = async (countryId: string) => {
    setForm((s) => ({
      ...s,
      countryId,
      provinceId: '',
      cantonId: '',
      parishId: '',
    }));
    setCantons([]);
    setParishes([]);
    setProvinces([]);
    await loadProvinces(countryId);
  };

  const onProvinceChange = async (provinceId: string) => {
    setForm((s) => ({ ...s, provinceId, cantonId: '', parishId: '' }));
    setParishes([]);
    await loadCantons(provinceId);
  };

  const onCantonChange = async (cantonId: string) => {
    setForm((s) => ({ ...s, cantonId, parishId: '' }));
    await loadParishes(cantonId);
  };

  const closeDialog = () => {
    setBranchDialogMode('closed');
    dialogRef.current?.close();
  };

  const payloadBody = useMemo(
    () => ({
      name: form.name.trim(),
      address: form.address.trim(),
      reference: form.reference.trim() || null,
      phones: form.phones.trim() || null,
      countryId: form.countryId.trim() || null,
      provinceId: form.provinceId.trim() || null,
      cantonId: form.cantonId.trim() || null,
      parishId: form.parishId.trim() || null,
      latitude: form.latitude.trim() || null,
      longitude: form.longitude.trim() || null,
      rechargeOption: form.rechargeOption.trim() || null,
      isActive: form.isActive,
      isMainBranch: form.isMainBranch,
    }),
    [form]
  );

  const formDisabled = editingId ? !canUpdate : !canCreate;

  const save = async () => {
    setError('');
    setSaving(true);
    try {
      if (editingId) {
        await branchService.update(editingId, { id: editingId, ...payloadBody });
      } else {
        await branchService.create(payloadBody);
      }
      closeDialog();
      await fetchList();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('branches.error.save');
      setError(msg);
    } finally {
      setSaving(false);
    }
  };

  const toggleDisable = async (row: BranchDto) => {
    setError('');
    try {
      if (row.isActive) {
        if (!canDelete) return;
        await branchService.disable(row.id);
      } else {
        if (!canUpdate) return;
        await branchService.enable(row.id);
      }
      await fetchList();
    } catch {
      setError(t('branches.error.toggle'));
    }
  };

  if (!canView) {
    return (
      <div className="page-shell">
        <h1 className="page-title">{t('branches.title')}</h1>
        <p className="page-subtitle">{t('common.noAccess')}</p>
      </div>
    );
  }

  return (
    <PageShell
      title={t('branches.title')}
      action={
        canCreate ? (
          <button type="button" className="btn btn--primary" onClick={openNew}>
            {t('branches.new')}
          </button>
        ) : undefined
      }
    >
      <TableCard>
        <div className="form-grid form-grid--2col" style={{ marginBottom: 16 }}>
          <label className="field">
            <span className="label">{t('branches.filter.status')}</span>
            <select
              value={activeStatus}
              onChange={(e) => setActiveStatus(e.target.value as CatalogActiveStatus)}
            >
              <option value="all">{t('branches.filter.all')}</option>
              <option value="active">{t('branches.filter.active')}</option>
              <option value="inactive">{t('branches.filter.inactive')}</option>
            </select>
          </label>
          <label className="field">
            <span className="label">{t('branches.filter.search')}</span>
            <input
              value={searchDraft}
              onChange={(e) => setSearchDraft(e.target.value)}
              placeholder={t('branches.filter.searchPlaceholder')}
            />
          </label>
          <div className="field" style={{ alignSelf: 'end' }}>
            <button
              type="button"
              className="btn btn--primary"
              onClick={() => {
                setAppliedSearch(searchDraft.trim());
              }}
              disabled={loading}
            >
              {t('branches.filter.apply')}
            </button>
          </div>
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
                <th>{t('branches.col.name')}</th>
                <th>{t('branches.col.address')}</th>
                <th>{t('branches.col.main')}</th>
                <th>{t('common.status')}</th>
                <th>{t('branches.col.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {items.map((x) => (
                <tr key={x.id}>
                  <td>{x.name}</td>
                  <td>{x.address}</td>
                  <td>
                    <Badge
                      label={x.isMainBranch ? t('common.yes') : t('common.no')}
                      variant={x.isMainBranch ? 'blue' : 'gray'}
                    />
                  </td>
                  <td>
                    <Badge
                      label={x.isActive ? t('common.active') : t('common.inactive')}
                      variant={x.isActive ? 'green' : 'gray'}
                    />
                  </td>
                  <td>
                    {canUpdate && (
                      <button type="button" className="btn btn--ghost" onClick={() => void openEdit(x.id)}>
                        {t('common.edit')}
                      </button>
                    )}
                    {(x.isActive ? canDelete : canUpdate) && (
                      <button type="button" className="btn btn--ghost" onClick={() => void toggleDisable(x)}>
                        {x.isActive ? t('branches.disable') : t('branches.enable')}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </TableCard>

      {createPortal(
        <dialog ref={dialogRef} className="branches-dialog">
        <div className="branches-dialog-inner">
          <h2 className="branches-dialog-title">{editingId ? t('branches.editTitle') : t('branches.createTitle')}</h2>
          {geoBootstrapError ? (
            <p className="branches-dialog-geo-error" role="alert">
              {geoBootstrapError}
            </p>
          ) : null}

          <div className="form-grid form-grid--2col">
            <label className="field field--span2">
              <span className="label">{t('branches.form.name')}</span>
              <input
                value={form.name}
                onChange={(e) => setForm((s) => ({ ...s, name: e.target.value }))}
                disabled={formDisabled}
              />
            </label>
            <label className="field field--span2">
              <span className="label">{t('branches.form.address')}</span>
              <input
                value={form.address}
                onChange={(e) => setForm((s) => ({ ...s, address: e.target.value }))}
                disabled={formDisabled}
              />
            </label>
            <label className="field">
              <span className="label">{t('branches.form.reference')}</span>
              <input
                value={form.reference}
                onChange={(e) => setForm((s) => ({ ...s, reference: e.target.value }))}
                disabled={formDisabled}
              />
            </label>
            <label className="field">
              <span className="label">{t('branches.form.phones')}</span>
              <input
                value={form.phones}
                onChange={(e) => setForm((s) => ({ ...s, phones: e.target.value }))}
                disabled={formDisabled}
              />
            </label>

            <fieldset className="branches-location field--span2">
              <legend className="branches-location-legend">{t('branches.form.locationSection')}</legend>
              <div className="form-grid form-grid--2col branches-location-grid">
                <label className="field">
                  <span className="label">
                    {t('branches.form.country')}
                    {countries.length === 0 ? (
                      <span className="branches-geo-hint"> {t('branches.form.loadingCountries')}</span>
                    ) : null}
                  </span>
                  <select
                    value={form.countryId}
                    onChange={(e) => void onCountryChange(e.target.value)}
                    disabled={formDisabled || countries.length === 0}
                    aria-busy={countries.length === 0}
                  >
                    <option value="">{t('common.select')}</option>
                    {form.countryId && !countries.some((c) => c.id === form.countryId) ? (
                      <option value={form.countryId}>{form.countryId}</option>
                    ) : null}
                    {countries.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="field">
                  <span className="label">
                    {t('branches.form.province')}
                    {loadingProvinces ? (
                      <span className="branches-geo-hint"> {t('branches.form.loading')}</span>
                    ) : null}
                  </span>
                  <select
                    value={form.provinceId}
                    onChange={(e) => void onProvinceChange(e.target.value)}
                    disabled={!form.countryId || formDisabled}
                    aria-busy={loadingProvinces}
                  >
                    <option value="">{t('common.select')}</option>
                    {provinces.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="field">
                  <span className="label">
                    {t('branches.form.canton')}
                    {loadingCantons ? (
                      <span className="branches-geo-hint"> {t('branches.form.loading')}</span>
                    ) : null}
                  </span>
                  <select
                    value={form.cantonId}
                    onChange={(e) => void onCantonChange(e.target.value)}
                    disabled={!form.provinceId || formDisabled}
                    aria-busy={loadingCantons}
                  >
                    <option value="">{t('common.select')}</option>
                    {cantons.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="field">
                  <span className="label">
                    {t('branches.form.parish')}
                    {loadingParishes ? (
                      <span className="branches-geo-hint"> {t('branches.form.loading')}</span>
                    ) : null}
                  </span>
                  <select
                    value={form.parishId}
                    onChange={(e) => setForm((s) => ({ ...s, parishId: e.target.value }))}
                    disabled={!form.cantonId || formDisabled}
                    aria-busy={loadingParishes}
                  >
                    <option value="">{t('common.select')}</option>
                    {parishes.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
            </fieldset>

            <label className="field">
              <span className="label">{t('branches.form.latitude')}</span>
              <input
                value={form.latitude}
                onChange={(e) => setForm((s) => ({ ...s, latitude: e.target.value }))}
                disabled={formDisabled}
              />
            </label>
            <label className="field">
              <span className="label">{t('branches.form.longitude')}</span>
              <input
                value={form.longitude}
                onChange={(e) => setForm((s) => ({ ...s, longitude: e.target.value }))}
                disabled={formDisabled}
              />
            </label>
            <label className="field field--span2">
              <span className="label">{t('branches.form.recharge')}</span>
              <input
                value={form.rechargeOption}
                onChange={(e) => setForm((s) => ({ ...s, rechargeOption: e.target.value }))}
                disabled={formDisabled}
              />
            </label>

            <label className="field field--inline field--span2">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(e) => setForm((s) => ({ ...s, isActive: e.target.checked }))}
                disabled={formDisabled}
              />
              <span>{t('branches.form.enabled')}</span>
            </label>
            <label className="field field--inline field--span2">
              <input
                type="checkbox"
                checked={form.isMainBranch}
                onChange={(e) => setForm((s) => ({ ...s, isMainBranch: e.target.checked }))}
                disabled={formDisabled}
              />
              <span>{t('branches.form.mainBranch')}</span>
            </label>
          </div>

          {audit && (
            <div className="branches-audit">
              <p>
                <strong>{t('branches.audit.createdAt')}</strong> {new Date(audit.createdAt).toLocaleString()}
              </p>
              <p>
                <strong>{t('branches.audit.updatedAt')}</strong>{' '}
                {audit.updatedAt ? new Date(audit.updatedAt).toLocaleString() : '—'}
              </p>
              <p>
                <strong>{t('branches.audit.createdBy')}</strong> {audit.createdBy}
              </p>
              <p>
                <strong>{t('branches.audit.updatedBy')}</strong> {audit.updatedBy ?? '—'}
              </p>
            </div>
          )}

          <div className="branches-dialog-actions">
            <button type="button" className="btn btn--ghost" onClick={closeDialog}>
              {t('common.cancel')}
            </button>
            {(editingId ? canUpdate : canCreate) && (
              <button type="button" className="btn" onClick={() => void save()} disabled={saving}>
                {saving ? t('common.saving') : t('common.save')}
              </button>
            )}
          </div>
        </div>
      </dialog>,
        document.body
      )}
    </PageShell>
  );
}
