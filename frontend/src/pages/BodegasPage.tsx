import { useCallback, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { EmptyState, LoadingState, NoAccessPage } from '../components/PageShell';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../components/zh/ZHForm';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import { branchService, type BranchDto } from '../services/branchService';
import { bodegaService, type WarehouseDto } from '../services/bodegaService';
import {
  warehouseSchema,
  defaultWarehouseValues,
  type WarehouseFormValues,
  STORAGE_TYPES,
} from '../schemas/inventory/warehouseSchema';
import './BodegasPage.css';

function fromDto(d: WarehouseDto): WarehouseFormValues {
  return {
    branchId:          d.branchId,
    name:              d.name,
    storageType:       d.storageType       ?? '',
    address:           d.address           ?? '',
    phone:             d.phone             ?? '',
    email:             d.email             ?? '',
    manager:           d.manager           ?? '',
    latitude:          d.latitude          ?? '',
    longitude:         d.longitude         ?? '',
    capacity:          d.capacity          ?? null,
    dailyDispatchGoal: d.dailyDispatchGoal ?? null,
  };
}

export function BodegasPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role    = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';

  const canView   = isAdmin || hasPerm('inventario.bodegas.view');
  const canCreate = isAdmin || hasPerm('inventario.bodegas.create');
  const canUpdate = isAdmin || hasPerm('inventario.bodegas.update');
  const canDelete = isAdmin || hasPerm('inventario.bodegas.delete');

  /* ── List state ── */
  const [items,    setItems]    = useState<WarehouseDto[]>([]);
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [loading,  setLoading]  = useState(true);
  const [error,    setError]    = useState('');
  const [search,   setSearch]   = useState('');

  /* ── Modal state ── */
  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editCode,  setEditCode]  = useState<string | null>(null);
  const [saving,    setSaving]    = useState(false);
  const [saveError, setSaveError] = useState('');

  /* ── Form ── */
  const { register, handleSubmit, reset, formState: { errors } } =
    useForm<WarehouseFormValues>({
      resolver: zodResolver(warehouseSchema),
      defaultValues: defaultWarehouseValues,
    });

  /* ── Fetch data ── */
  const fetchList = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await bodegaService.list('all') ?? []);
    } catch {
      setError(t('common.errorPrefix'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => { void fetchList(); }, [fetchList]);

  useEffect(() => {
    branchService.list('active').then(setBranches).catch(() => setBranches([]));
  }, []);

  /* ── KPI totals ── */
  const totals = useMemo(() => ({
    total:    items.length,
    active:   items.filter((x) => x.isActive).length,
    inactive: items.filter((x) => !x.isActive).length,
    branches: [...new Set(items.map((x) => x.branchId))].length,
  }), [items]);

  /* ── Filtered list ── */
  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return items;
    return items.filter(
      (x) =>
        x.name.toLowerCase().includes(q) ||
        (x.code ?? '').toLowerCase().includes(q) ||
        (x.manager ?? '').toLowerCase().includes(q) ||
        (x.address ?? '').toLowerCase().includes(q),
    );
  }, [items, search]);

  /* ── Modal helpers ── */
  const openCreateModal = () => {
    setEditingId(null);
    setEditCode(null);
    setSaveError('');
    reset(defaultWarehouseValues);
    setModalOpen(true);
  };

  const openEditModal = async (id: string) => {
    setSaveError('');
    try {
      const d = await bodegaService.getById(id);
      if (!d) return;
      setEditingId(id);
      setEditCode(d.code);
      reset(fromDto(d));
      setModalOpen(true);
    } catch {
      setError(t('common.errorPrefix'));
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
        branchId:          form.branchId,
        name:              form.name.trim(),
        storageType:       form.storageType       || null,
        address:           form.address           || null,
        phone:             form.phone             || null,
        email:             form.email             || null,
        manager:           form.manager           || null,
        latitude:          form.latitude          || null,
        longitude:         form.longitude         || null,
        capacity:          form.capacity          ?? null,
        dailyDispatchGoal: form.dailyDispatchGoal ?? null,
      };
      if (editingId) {
        await bodegaService.update(editingId, { id: editingId, ...payload });
      } else {
        await bodegaService.create(payload);
      }
      await fetchList();
      closeModal();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
        ?? t('common.errorPrefix');
      setSaveError(msg);
    } finally {
      setSaving(false);
    }
  });

  /* ── Toggle status ── */
  const toggleStatus = async (row: WarehouseDto) => {
    setError('');
    try {
      if (row.isActive) { if (!canDelete) return; await bodegaService.disable(row.id); }
      else              { if (!canUpdate) return; await bodegaService.enable(row.id);  }
      await fetchList();
    } catch {
      setError(t('common.errorPrefix'));
    }
  };

  if (!canView) return <NoAccessPage title="Bodegas" />;

  const branchName = (id: string) => branches.find((b) => b.id === id)?.name ?? id;

  return (
    <div className="pg-page">

      {/* ── Header ── */}
      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">Inventario</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Bodegas</span>
          </nav>
          <h1 className="pg-title">Gestión de Bodegas</h1>
          <p className="pg-subtitle">Administre sus almacenes, encargados y parámetros logísticos.</p>
        </div>
        <div className="pg-header-right">
          <button
            className="zh-btn zh-btn--secondary"
            type="button"
            disabled={loading}
            onClick={() => void fetchList()}
          >
            <span className="material-symbols-outlined">refresh</span>
            Actualizar
          </button>
          {canCreate && (
            <button className="zh-btn zh-btn--primary" type="button" onClick={openCreateModal}>
              <span className="material-symbols-outlined">add</span>
              Nueva Bodega
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
              <p className="pg-kpi-label">Total Bodegas</p>
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
            <div className="pg-kpi-icon pg-kpi-icon--error">
              <span className="material-symbols-outlined">block</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Inactivas</p>
              <p className="pg-kpi-value">{totals.inactive}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--secondary">
              <span className="material-symbols-outlined">business</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Sucursales con Bodega</p>
              <p className="pg-kpi-value">{totals.branches}</p>
            </div>
          </div>
        </div>
      )}

      {/* ── Table Section ── */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">warehouse</span>
            <span className="pg-section-label">Bodegas Registradas</span>
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
                  <th>Bodega</th>
                  <th>Sucursal</th>
                  <th>Encargado</th>
                  <th>Capacidad</th>
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
                      {row.storageType && (
                        <div style={{ fontSize: 'var(--text-label-sm-size)', color: 'var(--color-text-secondary)' }}>
                          {row.storageType}
                        </div>
                      )}
                    </td>
                    <td style={{ color: 'var(--color-text-secondary)', fontSize: 'var(--text-body-sm-size)' }}>
                      {branchName(row.branchId)}
                    </td>
                    <td>{row.manager ?? <span className="subtle">—</span>}</td>
                    <td>
                      {row.capacity
                        ? <span className="mono subtle">{row.capacity.toLocaleString('es')} m³</span>
                        : <span className="subtle">—</span>}
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
                              onClick={() => void toggleStatus(row)}
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
          <p className="subtle" style={{ fontSize: 12, margin: 0 }}>{filtered.length} bodegas</p>
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
          aria-label={editingId ? 'Editar bodega' : 'Nueva bodega'}
          onClick={(e) => { if (e.target === e.currentTarget) closeModal(); }}
        >
          <div className="zh-modal" style={{ maxWidth: 'min(900px, 95vw)', width: '100%' }}>

            {/* Modal header */}
            <div className="zh-modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
                <span className="material-symbols-outlined" style={{ color: 'var(--color-primary)', fontSize: 22 }}>
                  warehouse
                </span>
                <h2 className="zh-modal-title">
                  {editingId ? 'Editar Bodega' : 'Registro de Nueva Bodega'}
                </h2>
              </div>
              <button type="button" className="zh-modal-close" onClick={closeModal} aria-label="Cerrar">✕</button>
            </div>

            {/* Modal body */}
            <div className="zh-modal-body">
              {saveError && (
                <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />
              )}

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-6)', alignItems: 'start' }}>

                {/* ── Left column: General Info + Location ── */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>

                  {/* General Info */}
                  <div className="pg-section">
                    <div className="pg-section-header">
                      <div className="pg-section-header-left">
                        <span className="material-symbols-outlined pg-section-icon">info</span>
                        <span className="pg-section-label">Información General</span>
                      </div>
                    </div>
                    <div className="pg-section-body">
                      <div className="pg-form-grid pg-form-grid--2">
                        <ZHField label="Nombre de la Bodega" required error={errors.name?.message} style={{ gridColumn: '1 / -1' }}>
                          <input
                            className="zh-input"
                            placeholder="Ej: Almacén Central Norte"
                            disabled={saving}
                            {...register('name')}
                          />
                        </ZHField>
                        <ZHField label="Código">
                          <input
                            className="zh-input mono"
                            readOnly
                            value={editingId ? (editCode ?? '—') : 'Auto-generado'}
                            style={{ background: 'var(--color-surface-container)', color: 'var(--color-primary)' }}
                          />
                        </ZHField>
                        <ZHField label="Tipo de Almacenamiento">
                          <select className="zh-input" disabled={saving} {...register('storageType')}>
                            <option value="">— seleccionar —</option>
                            {STORAGE_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                          </select>
                        </ZHField>
                        <ZHField label="Sede / Sucursal" required error={errors.branchId?.message} style={{ gridColumn: '1 / -1' }}>
                          <select className="zh-input" disabled={saving} {...register('branchId')}>
                            <option value="">— seleccionar sucursal —</option>
                            {branches.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
                          </select>
                        </ZHField>
                      </div>
                    </div>
                  </div>

                  {/* Location */}
                  <div className="pg-section">
                    <div className="pg-section-header">
                      <div className="pg-section-header-left">
                        <span className="material-symbols-outlined pg-section-icon">location_on</span>
                        <span className="pg-section-label">Detalles de Ubicación</span>
                      </div>
                    </div>
                    <div className="pg-section-body">
                      <div className="pg-form-grid pg-form-grid--2">
                        <ZHField label="Dirección Completa" error={errors.address?.message} style={{ gridColumn: '1 / -1' }}>
                          <input
                            className="zh-input"
                            placeholder="Calle, número, colonia..."
                            disabled={saving}
                            {...register('address')}
                          />
                        </ZHField>
                        <ZHField label="Latitud" error={errors.latitude?.message}>
                          <input
                            className="zh-input mono"
                            placeholder="0.000000"
                            disabled={saving}
                            {...register('latitude')}
                          />
                        </ZHField>
                        <ZHField label="Longitud" error={errors.longitude?.message}>
                          <input
                            className="zh-input mono"
                            placeholder="0.000000"
                            disabled={saving}
                            {...register('longitude')}
                          />
                        </ZHField>
                      </div>
                    </div>
                  </div>
                </div>

                {/* ── Right column: Contact + Operations + Tip ── */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>

                  {/* Contact */}
                  <div className="pg-section">
                    <div className="pg-section-header">
                      <div className="pg-section-header-left">
                        <span className="material-symbols-outlined pg-section-icon">contact_phone</span>
                        <span className="pg-section-label">Contacto</span>
                      </div>
                    </div>
                    <div className="pg-section-body">
                      <div className="pg-form-grid pg-form-grid--2">
                        <ZHField label="Teléfono Directo" error={errors.phone?.message}>
                          <input
                            className="zh-input"
                            type="tel"
                            placeholder="+593 99 999 9999"
                            disabled={saving}
                            {...register('phone')}
                          />
                        </ZHField>
                        <ZHField label="Correo Electrónico" error={errors.email?.message}>
                          <input
                            className="zh-input"
                            type="email"
                            placeholder="bodega@empresa.com"
                            disabled={saving}
                            {...register('email')}
                          />
                        </ZHField>
                      </div>
                    </div>
                  </div>

                  {/* Operations */}
                  <div className="pg-section">
                    <div className="pg-section-header">
                      <div className="pg-section-header-left">
                        <span className="material-symbols-outlined pg-section-icon">monitoring</span>
                        <span className="pg-section-label">Operaciones y Metas</span>
                      </div>
                    </div>
                    <div className="pg-section-body">
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
                        <ZHField label="Jefe de Logística">
                          <input
                            className="zh-input"
                            placeholder="Nombre del responsable"
                            disabled={saving}
                            {...register('manager')}
                          />
                        </ZHField>
                        <div className="pg-form-grid pg-form-grid--2">
                          <ZHField label="Capacidad Total (m³)" error={errors.capacity?.message}>
                            <input
                              className="zh-input"
                              type="number"
                              min={0}
                              step={0.01}
                              placeholder="0"
                              disabled={saving}
                              {...register('capacity')}
                            />
                          </ZHField>
                          <ZHField label="Meta Despacho Diario" error={errors.dailyDispatchGoal?.message}>
                            <input
                              className="zh-input"
                              type="number"
                              min={0}
                              step={1}
                              placeholder="0"
                              disabled={saving}
                              {...register('dailyDispatchGoal')}
                            />
                          </ZHField>
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* ZH Tip */}
                  <div style={{
                    background: 'var(--color-surface-container)',
                    borderLeft: '4px solid var(--color-primary)',
                    borderRadius: '0 var(--radius-md) var(--radius-md) 0',
                    padding: 'var(--space-4)',
                  }}>
                    <div style={{ display: 'flex', gap: 'var(--space-2)', alignItems: 'center', marginBottom: 'var(--space-1)' }}>
                      <span className="material-symbols-outlined" style={{ color: 'var(--color-primary)', fontSize: 18 }}>lightbulb</span>
                      <span style={{ fontWeight: 600, color: 'var(--color-primary)', fontSize: 'var(--text-label-md-size)' }}>Consejo ZH</span>
                    </div>
                    <p style={{ fontSize: 'var(--text-body-sm-size)', color: 'var(--color-text-secondary)', margin: 0, lineHeight: 1.5 }}>
                      Optimiza el espacio usando estanterías de doble profundidad para productos de baja rotación y zonas de <em>cross-docking</em> cerca de los muelles para envíos rápidos.
                    </p>
                  </div>

                  {/* Registration summary */}
                  <div className="pg-section">
                    <div className="pg-section-body">
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                          <span style={{ fontSize: 'var(--text-label-sm-size)', color: 'var(--color-text-secondary)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Estado de Registro</span>
                          <span className="badge badge--orange badge--md">
                            {editingId ? 'Editando' : 'Borrador'}
                          </span>
                        </div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                          <span style={{ fontSize: 'var(--text-label-sm-size)', color: 'var(--color-text-secondary)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Fecha</span>
                          <span style={{ fontSize: 'var(--text-body-sm-size)' }}>{new Date().toLocaleDateString('es')}</span>
                        </div>
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
                  {saving ? t('common.saving') : 'Guardar Bodega'}
                </ZHBtn>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
