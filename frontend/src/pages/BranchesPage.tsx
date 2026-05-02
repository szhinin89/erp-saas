import { useCallback, useEffect, useMemo, useState } from 'react';
import { PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge, NoAccessPage } from '../components/PageShell';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { useAuthStore } from '../store/authStore';
import {
  branchService,
  type BranchDetailDto,
  type BranchDto,
  type GeographyItemDto,
} from '../services/branchService';
import './BranchesPage.css';
import { ZHBtn, ZHFormSection, ZHGrid, ZHField, ZHFormAlert, ZHToggle } from '../components/zh/ZHForm';
import { ZHColSpan } from '../components/zh/ZHLayout';
import ZHSearchBar from '../components/shared/ZHSearchBar';
import { EntityAuditPanel } from '../components/EntityAuditPanel';

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
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');

  const canView = isAdmin || hasPerm('saas.branches.view');
  const canCreate = isAdmin || hasPerm('saas.branches.create');
  const canUpdate = isAdmin || hasPerm('saas.branches.update');
  const canDelete = isAdmin || hasPerm('saas.branches.delete');

  const [items, setItems] = useState<BranchDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [branchListQuery, setBranchListQuery] = useState('');
  const [branchListApplied, setBranchListApplied] = useState('');

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
  /** 'new' | 'edit' cuando el formulario en pestaña Datos está activo; sincroniza país por defecto si los países cargan tarde. */
  const [branchDialogMode, setBranchDialogMode] = useState<'closed' | 'new' | 'edit'>('closed');
  const [uiTab, setUiTab] = useState<'data' | 'list' | 'audit'>('data');
  const [auditRefreshKey, setAuditRefreshKey] = useState(0);
  const [geoBootstrapError, setGeoBootstrapError] = useState('');

  const fetchList = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await branchService.list('all', branchListApplied.trim() || undefined));
    } catch {
      setError(t('branches.error.load'));
    } finally {
      setLoading(false);
    }
  }, [branchListApplied, t]);

  useEffect(() => {
    const id = window.setTimeout(() => setBranchListApplied(branchListQuery.trim()), 320);
    return () => window.clearTimeout(id);
  }, [branchListQuery]);

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

  const beginNewBranchForm = useCallback(() => {
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
  }, [countries, loadProvinces]);

  /** Listado → Datos: formulario de alta visible al entrar en Datos (crear/guardar solo en barra de Datos). */
  useEffect(() => {
    if (uiTab !== 'data' || branchDialogMode !== 'closed' || !canCreate) return;
    queueMicrotask(() => {
      beginNewBranchForm();
    });
  }, [uiTab, branchDialogMode, canCreate, beginNewBranchForm]);

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
      setUiTab('data');
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
    setError('');
    setBranchDialogMode('closed');
    setEditingId(null);
    setAudit(null);
    setUiTab('list');
  };

  /** Cancelar: en alta reinicia el borrador y permanece en Datos; en edición vuelve al listado. */
  const cancelDataTab = () => {
    setError('');
    if (editingId) {
      closeDialog();
    } else if (canCreate) {
      beginNewBranchForm();
    } else {
      closeDialog();
    }
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

  const canSubmit = useMemo(
    () =>
      Boolean(form.name.trim() && form.address.trim()) && (editingId ? canUpdate : canCreate),
    [form.name, form.address, editingId, canUpdate, canCreate]
  );

  const save = async () => {
    setError('');
    setSaving(true);
    try {
      if (editingId) {
        await branchService.update(editingId, { id: editingId, ...payloadBody });
        await fetchList();
        const d = await branchService.getById(editingId);
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
        setAuditRefreshKey((k) => k + 1);
      } else {
        await branchService.create(payloadBody);
        await fetchList();
        beginNewBranchForm();
      }
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
    return <NoAccessPage title={t('branches.title')} />;
  }

  return (
    <PageShell
      kicker={t('app.nav.group.saas')}
      title={t('branches.title')}
      action={
        uiTab === 'data' && branchDialogMode !== 'closed' ? (
          <>
            <ZHBtn variant="ghost" size="md" type="button" disabled={saving} onClick={cancelDataTab}>
              {t('common.cancel')}
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="button"
              disabled={saving || !canSubmit}
              onClick={() => void save()}
            >
              {saving ? t('common.saving') : editingId ? t('common.saveChanges') : t('branches.primaryCreate')}
            </ZHBtn>
          </>
        ) : undefined
      }
    >
      <TableCard>
        {error ? <ErrorState message={error} /> : null}
        <div className="zh-form-tabs" role="tablist">
          <button
            type="button"
            role="tab"
            aria-selected={uiTab === 'data'}
            className={uiTab === 'data' ? 'is-active' : ''}
            onClick={() => setUiTab('data')}
          >
            {t('common.formTab.data')}
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={uiTab === 'list'}
            className={uiTab === 'list' ? 'is-active' : ''}
            onClick={() => setUiTab('list')}
          >
            {t('branches.tabList')}
          </button>
          {editingId && branchDialogMode !== 'closed' ? (
            <button
              type="button"
              role="tab"
              aria-selected={uiTab === 'audit'}
              className={uiTab === 'audit' ? 'is-active' : ''}
              onClick={() => setUiTab('audit')}
            >
              {t('common.formTab.audit')}
            </button>
          ) : null}
        </div>

        {uiTab === 'data' && (
          <>
            {branchDialogMode === 'closed' ? (
              <EmptyState message={t('branches.dataTabHintUpdateOnly')} />
            ) : (
              <div className="branches-data-panel">
                <input type="hidden" name="tenantId" value={tenantId} />
                {geoBootstrapError ? (
                  <ZHFormAlert type="warning" message={t('branches.error.geography')} detail={geoBootstrapError || undefined} />
                ) : null}

                <ZHFormSection title={t('branches.section.identity')}>
                      <ZHGrid cols={2}>
                        <ZHColSpan span={2}>
                          <ZHField label={t('branches.form.name')} required>
                            <input
                              value={form.name}
                              onChange={(e) => setForm((s) => ({ ...s, name: e.target.value }))}
                              disabled={formDisabled}
                              autoComplete="organization"
                            />
                          </ZHField>
                        </ZHColSpan>
                        <ZHColSpan span={2}>
                          <ZHField label={t('branches.form.address')} required>
                            <input
                              value={form.address}
                              onChange={(e) => setForm((s) => ({ ...s, address: e.target.value }))}
                              disabled={formDisabled}
                              autoComplete="street-address"
                            />
                          </ZHField>
                        </ZHColSpan>
                        <ZHField label={t('branches.form.reference')}>
                          <input value={form.reference} onChange={(e) => setForm((s) => ({ ...s, reference: e.target.value }))} disabled={formDisabled} />
                        </ZHField>
                        <ZHField label={t('branches.form.phones')}>
                          <input value={form.phones} onChange={(e) => setForm((s) => ({ ...s, phones: e.target.value }))} disabled={formDisabled} autoComplete="tel" />
                        </ZHField>
                      </ZHGrid>
                    </ZHFormSection>

                    <ZHFormSection title={t('branches.form.locationSection')}>
                      <ZHGrid cols={2}>
                  <ZHField
                    label={t('branches.form.country')}
                    hint={countries.length === 0 ? t('branches.form.loadingCountries') : undefined}
                    hintType={countries.length === 0 ? 'info' : undefined}
                  >
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
                  </ZHField>

                  <ZHField
                    label={t('branches.form.province')}
                    hint={loadingProvinces ? t('branches.form.loading') : undefined}
                    hintType={loadingProvinces ? 'info' : undefined}
                  >
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
                  </ZHField>

                  <ZHField
                    label={t('branches.form.canton')}
                    hint={loadingCantons ? t('branches.form.loading') : undefined}
                    hintType={loadingCantons ? 'info' : undefined}
                  >
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
                  </ZHField>

                  <ZHField
                    label={t('branches.form.parish')}
                    hint={loadingParishes ? t('branches.form.loading') : undefined}
                    hintType={loadingParishes ? 'info' : undefined}
                  >
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
                  </ZHField>
                      </ZHGrid>
                    </ZHFormSection>

                    <ZHFormSection title={t('branches.form.recharge')}>
                      <ZHGrid cols={2}>
                        <ZHField label={t('branches.form.latitude')}>
                          <input value={form.latitude} onChange={(e) => setForm((s) => ({ ...s, latitude: e.target.value }))} disabled={formDisabled} />
                        </ZHField>
                        <ZHField label={t('branches.form.longitude')}>
                          <input value={form.longitude} onChange={(e) => setForm((s) => ({ ...s, longitude: e.target.value }))} disabled={formDisabled} />
                        </ZHField>
                        <ZHColSpan span={2}>
                          <ZHField label={t('branches.form.recharge')}>
                            <input
                              value={form.rechargeOption}
                              onChange={(e) => setForm((s) => ({ ...s, rechargeOption: e.target.value }))}
                              disabled={formDisabled}
                            />
                          </ZHField>
                        </ZHColSpan>
                      </ZHGrid>
                    </ZHFormSection>

                    <ZHFormSection title={t('common.status')}>
                      <ZHGrid cols={1}>
                        <ZHToggle
                          label={t('branches.form.enabled')}
                          description={t('branches.form.enabled')}
                          value={form.isActive}
                          onChange={(v) => setForm((s) => ({ ...s, isActive: v }))}
                          disabled={formDisabled}
                        />
                        <ZHToggle
                          label={t('branches.form.mainBranch')}
                          description={t('branches.form.mainBranch')}
                          value={form.isMainBranch}
                          onChange={(v) => setForm((s) => ({ ...s, isMainBranch: v }))}
                          disabled={formDisabled}
                        />
                      </ZHGrid>
                    </ZHFormSection>

                    {audit ? (
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
                    ) : null}
              </div>
            )}
          </>
        )}

        {uiTab === 'audit' && editingId && branchDialogMode !== 'closed' ? (
          <EntityAuditPanel entityType="Branch" entityId={editingId} take={10} refreshKey={auditRefreshKey} />
        ) : null}

        {uiTab === 'list' && (
          <>
            <div className="zh-mb-12">
              <ZHSearchBar
                searchQuery={branchListQuery}
                onSearch={setBranchListQuery}
                onClearAll={() => {
                  setBranchListQuery('');
                  setBranchListApplied('');
                }}
                filterValues={{}}
                placeholder={t('branches.list.searchPlaceholder')}
                resultCount={items.length}
                entityLabel={t('branches.list.entityLabel')}
                loading={loading}
                actionLabel={canCreate ? t('branches.list.newAction') : undefined}
                onAction={
                  canCreate
                    ? () => {
                        setUiTab('data');
                        beginNewBranchForm();
                      }
                    : undefined
                }
              />
            </div>

            {loading ? (
              <LoadingState />
            ) : items.length === 0 ? (
              <EmptyState
                message={branchListApplied.trim() ? t('common.listTab.noMatch') : t('common.noData')}
              />
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
                          <ZHBtn variant="secondary" size="xs" type="button" onClick={() => void openEdit(x.id)}>
                            {t('common.edit')}
                          </ZHBtn>
                        )}
                        {(x.isActive ? canDelete : canUpdate) && (
                          <ZHBtn variant="ghost" size="xs" type="button" onClick={() => void toggleDisable(x)}>
                            {x.isActive ? t('branches.disable') : t('branches.enable')}
                          </ZHBtn>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}
      </TableCard>
    </PageShell>
  );
}
