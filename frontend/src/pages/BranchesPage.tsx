import { useCallback, useEffect, useMemo, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { EmptyState, LoadingState, NoAccessPage } from '../components/PageShell';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { ZHBtn, ZHField, ZHToggle } from '../components/zh/ZHForm';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { useAuthStore } from '../store/authStore';
import {
  branchService,
  type BranchDto,
  type BranchDetailDto,
  type GeographyItemDto,
} from '../services/branchService';
import { branchFormSchema, type BranchFormValues } from '../schemas/saas/branchSchema';

const BRANCH_TYPES = ['Flagship Store', 'Boutique', 'Express', 'Almacén / Centro de Distribución'];

function emptyForm(): BranchFormValues {
  return {
    name: '', address: '', branchType: '', reference: '', phones: '',
    email: '', managerName: '', countryId: '', provinceId: '', cantonId: '',
    parishId: '', latitude: '', longitude: '', storageCapacity: null,
    dailySalesGoal: null, rechargeOption: '', isActive: true, isMainBranch: false,
  };
}

function fromDto(d: BranchDto | BranchDetailDto): BranchFormValues {
  return {
    name:            d.name,
    address:         d.address,
    branchType:      d.branchType      ?? '',
    reference:       d.reference       ?? '',
    phones:          d.phones          ?? '',
    email:           d.email           ?? '',
    managerName:     d.managerName     ?? '',
    countryId:       d.countryId       ?? '',
    provinceId:      d.provinceId      ?? '',
    cantonId:        d.cantonId        ?? '',
    parishId:        d.parishId        ?? '',
    latitude:        d.latitude        ?? '',
    longitude:       d.longitude       ?? '',
    storageCapacity: d.storageCapacity ?? null,
    dailySalesGoal:  d.dailySalesGoal  ?? null,
    rechargeOption:  d.rechargeOption  ?? '',
    isActive:        d.isActive,
    isMainBranch:    d.isMainBranch,
  };
}

export function BranchesPage() {
  const { t } = useI18n();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const role      = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin   = role === 'Admin' || role === 'SuperAdmin';
  const canView   = isAdmin || hasPerm('saas.branches.view');
  const canCreate = isAdmin || hasPerm('saas.branches.create');
  const canUpdate = isAdmin || hasPerm('saas.branches.update');
  const canDelete = isAdmin || hasPerm('saas.branches.delete');

  /* ── List state ── */
  const [items,   setItems]   = useState<BranchDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState('');
  const [search,  setSearch]  = useState('');

  /* ── Modal state ── */
  const [modalOpen,  setModalOpen]  = useState(false);
  const [editingId,  setEditingId]  = useState<string | null>(null);
  const [editCode,   setEditCode]   = useState<string | null>(null);
  const [saving,     setSaving]     = useState(false);
  const [saveError,  setSaveError]  = useState('');

  /* ── Geography ── */
  const [countries,        setCountries]        = useState<GeographyItemDto[]>([]);
  const [provinces,        setProvinces]        = useState<GeographyItemDto[]>([]);
  const [cantons,          setCantons]          = useState<GeographyItemDto[]>([]);
  const [parishes,         setParishes]         = useState<GeographyItemDto[]>([]);
  const [loadingProvinces, setLoadingProvinces] = useState(false);
  const [loadingCantons,   setLoadingCantons]   = useState(false);
  const [loadingParishes,  setLoadingParishes]  = useState(false);

  /* ── Form ── */
  const { register, control, handleSubmit, reset, watch, setValue, formState: { errors } } =
    useForm<BranchFormValues>({
      resolver: zodResolver(branchFormSchema),
      defaultValues: emptyForm(),
    });
  const formWatch = watch();

  /* ── Fetch list ── */
  const fetchList = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await branchService.list('all'));
    } catch {
      setError(t('branches.error.load'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => { void fetchList(); }, [fetchList]);

  /* ── Load countries once ── */
  useEffect(() => {
    branchService.countries().then(setCountries).catch(() => setCountries([]));
  }, []);

  /* ── Geography cascade ── */
  const loadProvinces = useCallback(async (countryId: string) => {
    if (!countryId) { setProvinces([]); return; }
    setLoadingProvinces(true);
    try { setProvinces(await branchService.provinces(countryId)); }
    catch { setProvinces([]); }
    finally { setLoadingProvinces(false); }
  }, []);

  const loadCantons = useCallback(async (provinceId: string) => {
    if (!provinceId) { setCantons([]); return; }
    setLoadingCantons(true);
    try { setCantons(await branchService.cantons(provinceId)); }
    catch { setCantons([]); }
    finally { setLoadingCantons(false); }
  }, []);

  const loadParishes = useCallback(async (cantonId: string) => {
    if (!cantonId) { setParishes([]); return; }
    setLoadingParishes(true);
    try { setParishes(await branchService.parishes(cantonId)); }
    catch { setParishes([]); }
    finally { setLoadingParishes(false); }
  }, []);

  const onCountryChange = async (countryId: string) => {
    setValue('provinceId', ''); setValue('cantonId', ''); setValue('parishId', '');
    setProvinces([]); setCantons([]); setParishes([]);
    await loadProvinces(countryId);
  };

  const onProvinceChange = async (provinceId: string) => {
    setValue('cantonId', ''); setValue('parishId', '');
    setCantons([]); setParishes([]);
    await loadCantons(provinceId);
  };

  const onCantonChange = async (cantonId: string) => {
    setValue('parishId', '');
    setParishes([]);
    await loadParishes(cantonId);
  };

  /* ── KPI totals ── */
  const totals = useMemo(() => ({
    total:    items.length,
    active:   items.filter((x) => x.isActive).length,
    main:     items.filter((x) => x.isMainBranch).length,
    inactive: items.filter((x) => !x.isActive).length,
  }), [items]);

  /* ── Filtered list ── */
  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return items;
    return items.filter(
      (x) =>
        x.name.toLowerCase().includes(q) ||
        (x.code ?? '').toLowerCase().includes(q) ||
        (x.managerName ?? '').toLowerCase().includes(q) ||
        (x.address ?? '').toLowerCase().includes(q),
    );
  }, [items, search]);

  /* ── Modal helpers ── */
  const openCreateModal = () => {
    setEditingId(null);
    setEditCode(null);
    setSaveError('');
    const defaultCountry = countries.find((c) => c.id === 'EC') ? 'EC' : '';
    reset({ ...emptyForm(), countryId: defaultCountry });
    setProvinces([]); setCantons([]); setParishes([]);
    if (defaultCountry) void loadProvinces(defaultCountry);
    setModalOpen(true);
  };

  const openEditModal = async (id: string) => {
    setSaveError('');
    try {
      const d = await branchService.getById(id);
      setEditingId(id);
      setEditCode(d.code);
      reset(fromDto(d));
      setProvinces([]); setCantons([]); setParishes([]);
      await loadProvinces(d.countryId ?? '');
      await loadCantons(d.provinceId ?? '');
      await loadParishes(d.cantonId ?? '');
      setModalOpen(true);
    } catch {
      setError(t('branches.error.loadOne'));
    }
  };

  const closeModal = () => {
    setModalOpen(false);
    setEditingId(null);
    setEditCode(null);
    setSaveError('');
  };

  /* ── Save ── */
  const save = handleSubmit(async (form) => {
    setSaveError('');
    setSaving(true);
    try {
      const payload = {
        name:            form.name.trim(),
        address:         form.address.trim(),
        branchType:      form.branchType      || null,
        reference:       form.reference       || null,
        phones:          form.phones          || null,
        email:           form.email           || null,
        managerName:     form.managerName     || null,
        countryId:       form.countryId       || null,
        provinceId:      form.provinceId      || null,
        cantonId:        form.cantonId        || null,
        parishId:        form.parishId        || null,
        latitude:        form.latitude        || null,
        longitude:       form.longitude       || null,
        storageCapacity: form.storageCapacity ?? null,
        dailySalesGoal:  form.dailySalesGoal  ?? null,
        rechargeOption:  form.rechargeOption  || null,
        isActive:        form.isActive,
        isMainBranch:    form.isMainBranch,
      };
      if (editingId) {
        await branchService.update(editingId, { id: editingId, ...payload });
      } else {
        await branchService.create(payload);
      }
      await fetchList();
      closeModal();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
        ?? t('branches.error.save');
      setSaveError(msg);
    } finally {
      setSaving(false);
    }
  });

  /* ── Toggle status ── */
  const toggleDisable = async (row: BranchDto) => {
    setError('');
    try {
      if (row.isActive) { if (!canDelete) return; await branchService.disable(row.id); }
      else              { if (!canUpdate) return; await branchService.enable(row.id);  }
      await fetchList();
    } catch {
      setError(t('branches.error.toggle'));
    }
  };

  if (!canView) return <NoAccessPage title={t('branches.title')} />;

  return (
    <div className="pg-page">

      {/* ── Header ── */}
      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">{t('app.nav.group.security')}</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">{t('branches.title')}</span>
          </nav>
          <h1 className="pg-title">{t('branches.title')}</h1>
          <p className="pg-subtitle">Gestione sus puntos de venta, encargados y parámetros operativos por sucursal.</p>
        </div>
        <div className="pg-header-right">
          <button
            className="zh-btn zh-btn--secondary"
            type="button"
            disabled={loading}
            onClick={() => void fetchList()}
          >
            <span className="material-symbols-outlined">refresh</span>
            {t('common.refresh') || 'Actualizar'}
          </button>
          {canCreate && (
            <button className="zh-btn zh-btn--primary" type="button" onClick={openCreateModal}>
              <span className="material-symbols-outlined">add</span>
              Nueva Sucursal
            </button>
          )}
        </div>
      </div>

      {/* ── Alerts ── */}
      {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}

      {/* ── KPI Cards ── */}
      {!loading && (
        <div className="pg-kpis">
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">warehouse</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Total Sucursales</p>
              <p className="pg-kpi-value">{totals.total}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--success">
              <span className="material-symbols-outlined">task_alt</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Activas</p>
              <p className="pg-kpi-value">{totals.active}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--secondary">
              <span className="material-symbols-outlined">star</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Sucursal Principal</p>
              <p className="pg-kpi-value">{totals.main}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--error">
              <span className="material-symbols-outlined">block</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Inactivas</p>
              <p className="pg-kpi-value">{totals.inactive}</p>
            </div>
          </div>
        </div>
      )}

      {/* ── Table Section ── */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">business</span>
            <span className="pg-section-label">Sucursales Registradas</span>
          </div>
        </div>

        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <input
                type="text"
                placeholder="Buscar por nombre, código o encargado..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                disabled={loading}
              />
            </div>
          </div>
          <div className="pg-table-controls-right">
            <span>Mostrando {filtered.length} de {items.length}</span>
          </div>
        </div>

        {loading ? (
          <div style={{ padding: '40px' }}><LoadingState /></div>
        ) : items.length === 0 ? (
          <div style={{ padding: '40px' }}><EmptyState message={t('common.noData')} /></div>
        ) : filtered.length === 0 ? (
          <div style={{ padding: '40px' }}><EmptyState message="No se encontraron resultados." /></div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Código</th>
                  <th>Sucursal</th>
                  <th>Encargado</th>
                  <th>Contacto</th>
                  <th>Principal</th>
                  <th>Estado</th>
                  {(canUpdate || canDelete) ? <th style={{ textAlign: 'right' }}>Acciones</th> : null}
                </tr>
              </thead>
              <tbody>
                {filtered.map((row) => (
                  <tr key={row.id} style={{ opacity: row.isActive ? 1 : 0.65 }}>
                    <td>
                      <span className="badge badge--gray badge--md mono">
                        {row.code ?? '—'}
                      </span>
                    </td>
                    <td>
                      <div style={{ fontWeight: 500, color: 'var(--color-text-primary)' }}>{row.name}</div>
                      {row.branchType && (
                        <div style={{ fontSize: 'var(--text-label-sm-size)', color: 'var(--color-text-secondary)' }}>
                          {row.branchType}
                        </div>
                      )}
                    </td>
                    <td>{row.managerName ?? <span className="subtle">—</span>}</td>
                    <td>
                      <div style={{ fontSize: 'var(--text-body-sm-size)', color: 'var(--color-text-secondary)' }}>
                        {row.phones ?? ''}
                        {row.email && row.phones ? ' · ' : ''}
                        {row.email ?? ''}
                        {!row.phones && !row.email && <span className="subtle">—</span>}
                      </div>
                    </td>
                    <td>
                      {row.isMainBranch
                        ? <span className="badge badge--blue badge--md">Principal</span>
                        : <span className="subtle" style={{ fontSize: 'var(--text-body-sm-size)' }}>—</span>}
                    </td>
                    <td>
                      <span className={row.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                        {row.isActive ? t('common.active') : t('common.inactive')}
                      </span>
                    </td>
                    {(canUpdate || canDelete) ? (
                      <td style={{ textAlign: 'right' }}>
                        <div style={{ display: 'flex', gap: 'var(--space-1)', justifyContent: 'flex-end' }}>
                          {canUpdate && (
                            <button
                              type="button"
                              className="zh-btn zh-btn--ghost zh-btn--sm"
                              title="Editar"
                              onClick={() => void openEditModal(row.id)}
                            >
                              <span className="material-symbols-outlined">edit</span>
                            </button>
                          )}
                          {(row.isActive ? canDelete : canUpdate) && (
                            <button
                              type="button"
                              className="zh-btn zh-btn--ghost zh-btn--sm"
                              title={row.isActive ? 'Desactivar' : 'Activar'}
                              onClick={() => void toggleDisable(row)}
                            >
                              <span className="material-symbols-outlined">
                                {row.isActive ? 'block' : 'check_circle'}
                              </span>
                            </button>
                          )}
                        </div>
                      </td>
                    ) : null}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="pg-table-footer">
          <p className="subtle" style={{ fontSize: 12, margin: 0 }}>{filtered.length} sucursales</p>
          {items.length > 0 && (
            <p className="pg-table-timestamp">Última carga: {new Date().toLocaleTimeString('es')}</p>
          )}
        </div>
      </div>

      {/* ── Create / Edit Modal ── */}
      {modalOpen && (
        <div
          className="zh-modal-overlay"
          role="dialog"
          aria-modal="true"
          aria-label={editingId ? 'Editar sucursal' : 'Nueva sucursal'}
          onClick={(e) => { if (e.target === e.currentTarget) closeModal(); }}
        >
          <div
            className="zh-modal"
            style={{ maxWidth: 'min(900px, 95vw)', width: '100%' }}
          >
            {/* Modal header */}
            <div className="zh-modal-header">
              <h2 className="zh-modal-title">
                {editingId ? 'Editar Sucursal' : 'Nueva Sucursal'}
              </h2>
              <button
                type="button"
                className="zh-modal-close"
                onClick={closeModal}
                aria-label="Cerrar"
              >✕</button>
            </div>

            {/* Modal body */}
            <div className="zh-modal-body">
              {saveError && (
                <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />
              )}

              <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 'var(--space-6)', alignItems: 'start' }}>

                {/* ── Left column: General Info + Location ── */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>

                  {/* Section: General Info */}
                  <div className="pg-section">
                    <div className="pg-section-header">
                      <div className="pg-section-header-left">
                        <span className="material-symbols-outlined pg-section-icon">info</span>
                        <span className="pg-section-label">Información General</span>
                      </div>
                    </div>
                    <div className="pg-section-body">
                      <div className="pg-form-grid pg-form-grid--2">
                        <ZHField label="Nombre de la Sucursal" required error={errors.name?.message}>
                          <input
                            className="zh-input"
                            placeholder="Ej: Sucursal Norte Central"
                            disabled={saving}
                            {...register('name')}
                          />
                        </ZHField>
                        <ZHField label="Código">
                          <input
                            className="zh-input mono"
                            readOnly
                            value={editingId ? (editCode ?? '—') : 'Auto-generado'}
                            style={{ color: 'var(--color-primary)', background: 'var(--color-surface-container)' }}
                          />
                        </ZHField>
                        <ZHField label="Tipo de Sucursal">
                          <select className="zh-input" disabled={saving} {...register('branchType')}>
                            <option value="">— seleccionar —</option>
                            {BRANCH_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                          </select>
                        </ZHField>
                        <ZHField label="Dirección" required error={errors.address?.message}>
                          <input
                            className="zh-input"
                            placeholder="Calle, número, colonia..."
                            disabled={saving}
                            {...register('address')}
                          />
                        </ZHField>
                      </div>
                    </div>
                  </div>

                  {/* Section: Location */}
                  <div className="pg-section">
                    <div className="pg-section-header">
                      <div className="pg-section-header-left">
                        <span className="material-symbols-outlined pg-section-icon">location_on</span>
                        <span className="pg-section-label">Ubicación Geográfica</span>
                      </div>
                    </div>
                    <div className="pg-section-body">
                      <div className="pg-form-grid pg-form-grid--2">
                        <ZHField label="País">
                          <select
                            className="zh-input"
                            disabled={saving || countries.length === 0}
                            {...register('countryId', {
                              onChange: async (e) => { await onCountryChange(e.target.value); },
                            })}
                          >
                            <option value="">— seleccionar —</option>
                            {countries.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                          </select>
                        </ZHField>

                        <ZHField label={loadingProvinces ? 'Provincia (cargando…)' : 'Provincia'}>
                          <select
                            className="zh-input"
                            disabled={saving || !formWatch.countryId || loadingProvinces}
                            {...register('provinceId', {
                              onChange: async (e) => { await onProvinceChange(e.target.value); },
                            })}
                          >
                            <option value="">— seleccionar —</option>
                            {provinces.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                          </select>
                        </ZHField>

                        <ZHField label={loadingCantons ? 'Cantón (cargando…)' : 'Cantón'}>
                          <select
                            className="zh-input"
                            disabled={saving || !formWatch.provinceId || loadingCantons}
                            {...register('cantonId', {
                              onChange: async (e) => { await onCantonChange(e.target.value); },
                            })}
                          >
                            <option value="">— seleccionar —</option>
                            {cantons.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                          </select>
                        </ZHField>

                        <ZHField label={loadingParishes ? 'Parroquia (cargando…)' : 'Parroquia'}>
                          <select
                            className="zh-input"
                            disabled={saving || !formWatch.cantonId || loadingParishes}
                            {...register('parishId')}
                          >
                            <option value="">— seleccionar —</option>
                            {parishes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                          </select>
                        </ZHField>

                        <ZHField label="Latitud" error={errors.latitude?.message}>
                          <input
                            className="zh-input mono"
                            placeholder="-0.2295"
                            disabled={saving}
                            {...register('latitude')}
                          />
                        </ZHField>

                        <ZHField label="Longitud" error={errors.longitude?.message}>
                          <input
                            className="zh-input mono"
                            placeholder="-78.5243"
                            disabled={saving}
                            {...register('longitude')}
                          />
                        </ZHField>

                        <ZHField label="Referencia">
                          <input
                            className="zh-input"
                            placeholder="Punto de referencia"
                            disabled={saving}
                            {...register('reference')}
                          />
                        </ZHField>
                      </div>
                    </div>
                  </div>
                </div>

                {/* ── Right column: Contact + Operations + Cards ── */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>

                  {/* Section: Contact */}
                  <div className="pg-section">
                    <div className="pg-section-header">
                      <div className="pg-section-header-left">
                        <span className="material-symbols-outlined pg-section-icon">contact_phone</span>
                        <span className="pg-section-label">Contacto</span>
                      </div>
                    </div>
                    <div className="pg-section-body">
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
                        <ZHField label="Teléfono" error={errors.phones?.message}>
                          <input
                            className="zh-input"
                            placeholder="+593 99 999 9999"
                            disabled={saving}
                            {...register('phones')}
                          />
                        </ZHField>
                        <ZHField label="Correo Electrónico" error={errors.email?.message}>
                          <input
                            className="zh-input"
                            type="email"
                            placeholder="sucursal@empresa.com"
                            disabled={saving}
                            {...register('email')}
                          />
                        </ZHField>
                      </div>
                    </div>
                  </div>

                  {/* Section: Operations */}
                  <div className="pg-section">
                    <div className="pg-section-header">
                      <div className="pg-section-header-left">
                        <span className="material-symbols-outlined pg-section-icon">settings_input_component</span>
                        <span className="pg-section-label">Operaciones y Metas</span>
                      </div>
                    </div>
                    <div className="pg-section-body">
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
                        <ZHField label="Gerente Asignado">
                          <input
                            className="zh-input"
                            placeholder="Nombre del responsable"
                            disabled={saving}
                            {...register('managerName')}
                          />
                        </ZHField>
                        <ZHField label="Capacidad de Almacén (m²)" error={errors.storageCapacity?.message}>
                          <input
                            className="zh-input"
                            type="number"
                            min={0}
                            step={0.01}
                            placeholder="0"
                            disabled={saving}
                            {...register('storageCapacity')}
                          />
                        </ZHField>
                        <ZHField label="Meta de Venta Diaria ($)" error={errors.dailySalesGoal?.message}>
                          <input
                            className="zh-input"
                            type="number"
                            min={0}
                            step={0.01}
                            placeholder="0.00"
                            disabled={saving}
                            {...register('dailySalesGoal')}
                          />
                        </ZHField>
                      </div>
                    </div>
                  </div>

                  {/* ZH Tip card */}
                  <div className="pg-accent-card" style={{ background: 'var(--color-primary-fixed)', borderRadius: 'var(--radius-lg)', padding: 'var(--space-4)' }}>
                    <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'flex-start' }}>
                      <span className="material-symbols-outlined" style={{ color: 'var(--color-primary)', fontSize: 24, flexShrink: 0, marginTop: 2 }}>lightbulb</span>
                      <div>
                        <p style={{ fontWeight: 600, color: 'var(--color-primary)', margin: '0 0 var(--space-1)', fontSize: 'var(--text-label-md-size)' }}>Consejo ZH</p>
                        <p style={{ fontSize: 'var(--text-body-sm-size)', color: 'var(--color-text-secondary)', margin: 0 }}>
                          Asegúrate de asignar un <strong>Gerente</strong> con experiencia en el tipo de sucursal seleccionado para optimizar la meta diaria.
                        </p>
                      </div>
                    </div>
                  </div>

                  {/* Status toggles */}
                  <div className="pg-section">
                    <div className="pg-section-body">
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
                        <Controller
                          name="isActive"
                          control={control}
                          render={({ field }) => (
                            <ZHToggle
                              label="Sucursal Activa"
                              description="La sucursal aparece en listados y puede operar."
                              value={field.value}
                              onChange={field.onChange}
                              disabled={saving}
                            />
                          )}
                        />
                        <Controller
                          name="isMainBranch"
                          control={control}
                          render={({ field }) => (
                            <ZHToggle
                              label="Sucursal Principal"
                              description="Solo puede haber una sucursal principal por empresa."
                              value={field.value}
                              onChange={field.onChange}
                              disabled={saving}
                            />
                          )}
                        />
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* Modal footer */}
            <div className="pg-actions-bar">
              <div className="pg-actions-info" />
              <div className="pg-actions-buttons">
                <ZHBtn variant="ghost" size="md" type="button" disabled={saving} onClick={closeModal}>
                  Cancelar
                </ZHBtn>
                <ZHBtn
                  variant="primary"
                  size="md"
                  type="button"
                  disabled={saving}
                  onClick={() => void save()}
                >
                  <span className="material-symbols-outlined">save</span>
                  {saving ? t('common.saving') : 'Guardar Sucursal'}
                </ZHBtn>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
