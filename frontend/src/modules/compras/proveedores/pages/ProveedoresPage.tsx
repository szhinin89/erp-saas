import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useAuthStore } from '../../../../store/authStore';
import { useI18n } from '../../../../i18n/i18n';
import { useProveedores } from '../hooks/useProveedores';
import {
  proveedorSchema,
  defaultProveedorValues,
  type ProveedorFormValues,
} from '../schemas/proveedorSchema';
import type { Proveedor } from '../api/proveedorService';
import './proveedores-page.css';

type TabId = 'listado' | 'formulario';

const CATEGORIES = ['Insumos', 'Tecnología', 'Servicios', 'Manufactura', 'Logística', 'Otro'];

function statusClass(status: Proveedor['status']) {
  if (status === 'activo')    return 'zh-status zh-status--active';
  if (status === 'pendiente') return 'zh-status zh-status--pending';
  return 'zh-status zh-status--inactive';
}

function statusLabel(status: Proveedor['status']) {
  if (status === 'activo')    return 'Activo';
  if (status === 'pendiente') return 'Pendiente';
  return 'Inactivo';
}

export function ProveedoresPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role    = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin   = role === 'Admin' || role === 'SuperAdmin';
  const canView   = isAdmin || hasPerm('compras.proveedores.view');
  const canCreate = isAdmin || hasPerm('compras.proveedores.create');
  const canEdit   = isAdmin || hasPerm('compras.proveedores.edit');

  const { proveedores, loading, error, saving, saveError, createProveedor, updateProveedor, setProveedorStatus } = useProveedores();

  /* ── UI state ── */
  const [activeTab, setActiveTab]     = useState<TabId>('listado');
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'activo' | 'pendiente' | 'inactivo'>('all');
  const [modalOpen, setModalOpen]     = useState(false);
  const [editingId, setEditingId]     = useState<string | null>(null);

  /* ── Form ── */
  const { register, handleSubmit, reset, setValue, formState: { errors } } = useForm<ProveedorFormValues>({
    resolver: zodResolver(proveedorSchema),
    defaultValues: defaultProveedorValues,
  });

  /* ── Derived ── */
  const filtered = useMemo(() => {
    let list = proveedores;
    if (statusFilter !== 'all') list = list.filter((p) => p.status === statusFilter);
    const term = searchQuery.trim().toLowerCase();
    if (!term) return list;
    return list.filter((p) =>
      p.ruc.toLowerCase().includes(term) ||
      p.razonSocial.toLowerCase().includes(term) ||
      (p.contactoPrincipal ?? '').toLowerCase().includes(term)
    );
  }, [proveedores, searchQuery, statusFilter]);

  const totals = useMemo(() => ({
    total:     proveedores.length,
    activos:   proveedores.filter((p) => p.status === 'activo').length,
    pendientes: proveedores.filter((p) => p.status === 'pendiente').length,
    inactivos: proveedores.filter((p) => p.status === 'inactivo').length,
  }), [proveedores]);

  /* ── Modal helpers ── */
  const openCreateModal = () => {
    setEditingId(null);
    reset(defaultProveedorValues);
    setModalOpen(true);
  };

  const openEditModal = (prov: Proveedor) => {
    setEditingId(prov.id);
    setValue('ruc', prov.ruc);
    setValue('razonSocial', prov.razonSocial);
    setValue('nombreComercial', prov.nombreComercial ?? '');
    setValue('contactoPrincipal', prov.contactoPrincipal ?? '');
    setValue('email', prov.email ?? '');
    setValue('phone', prov.phone ?? '');
    setValue('address', prov.address ?? '');
    setValue('website', prov.website ?? '');
    setValue('category', prov.category ?? '');
    setValue('status', prov.status);
    setModalOpen(true);
  };

  const closeModal = () => {
    setModalOpen(false);
    setEditingId(null);
    reset(defaultProveedorValues);
  };

  const onSubmit = handleSubmit(async (values) => {
    const payload = {
      ruc:               values.ruc,
      razonSocial:       values.razonSocial,
      nombreComercial:   values.nombreComercial || null,
      contactoPrincipal: values.contactoPrincipal || null,
      email:             values.email || null,
      phone:             values.phone || null,
      address:           values.address || null,
      website:           values.website || null,
      category:          values.category || null,
      status:            values.status,
    };
    if (editingId) {
      const updated = await updateProveedor(editingId, payload);
      if (updated) closeModal();
    } else {
      const created = await createProveedor(payload);
      if (created) closeModal();
    }
  });

  const handleToggleStatus = async (prov: Proveedor) => {
    if (!canEdit) return;
    const next = prov.status === 'activo' ? 'inactivo' : 'activo';
    await setProveedorStatus(prov.id, next);
  };

  /* ── Inline form tab ── */
  const handleNewInTab = () => {
    setEditingId(null);
    reset(defaultProveedorValues);
    setActiveTab('formulario');
  };

  if (!canView) return <NoAccessPage title="Proveedores" />;

  const anyError = error || saveError;

  return (
    <div className="pg-page">

      {/* ── Page header ── */}
      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Ruta de navegación">
            <span className="pg-breadcrumb-item">Compras</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Proveedores</span>
          </nav>
          <h1 className="pg-title">Administración de Proveedores</h1>
          <p className="pg-subtitle">Gestione el directorio de proveedores, sus estados y condiciones de pago.</p>
        </div>
        <div className="pg-header-right">
          <ZHBtn variant="ghost" size="md" type="button">
            <span className="material-symbols-outlined">download</span>
            Exportar
          </ZHBtn>
          {canCreate && (
            <ZHBtn variant="primary" size="md" type="button" onClick={openCreateModal}>
              <span className="material-symbols-outlined">add</span>
              Crear Proveedor
            </ZHBtn>
          )}
        </div>
      </div>

      {/* ── Errors ── */}
      {anyError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={anyError} /> : null}

      {/* ── KPI bento grid ── */}
      <div className="pg-kpis">
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--primary">
            <span className="material-symbols-outlined">storefront</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Total Proveedores</p>
            <p className="pg-kpi-value">{totals.total}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--success">
            <span className="material-symbols-outlined">verified</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Activos</p>
            <p className="pg-kpi-value">{totals.activos}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--warning">
            <span className="material-symbols-outlined">pending</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Pendientes</p>
            <p className="pg-kpi-value">{totals.pendientes}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--error">
            <span className="material-symbols-outlined">block</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Desactivados</p>
            <p className="pg-kpi-value">{totals.inactivos}</p>
          </div>
        </div>
      </div>

      {/* ── Main section ── */}
      <div className="pg-section">

        {/* Tabs */}
        <div className="pg-section-header">
          <div className="zh-form-tabs" role="tablist" aria-label="Secciones de proveedores">
            <button
              type="button" role="tab"
              aria-selected={activeTab === 'listado'}
              className={activeTab === 'listado' ? 'is-active' : ''}
              onClick={() => setActiveTab('listado')}
            >
              <span className="material-symbols-outlined" style={{ fontSize: 16, verticalAlign: 'middle', marginRight: 4 }}>view_list</span>
              Listado
            </button>
            {canCreate && (
              <button
                type="button" role="tab"
                aria-selected={activeTab === 'formulario'}
                className={activeTab === 'formulario' ? 'is-active' : ''}
                onClick={handleNewInTab}
              >
                <span className="material-symbols-outlined" style={{ fontSize: 16, verticalAlign: 'middle', marginRight: 4 }}>add_box</span>
                Nuevo Proveedor
              </button>
            )}
          </div>
        </div>

        {/* ── Listado tab ── */}
        {activeTab === 'listado' && (
          <>
            {/* Filter bar */}
            <div className="pg-table-controls">
              <div className="pg-table-controls-left">
                <div className="pg-search">
                  <span className="material-symbols-outlined">search</span>
                  <input
                    className="zh-input"
                    type="search"
                    placeholder="Buscar RUC, razón social…"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    aria-label="Buscar proveedor"
                  />
                </div>
                <select
                  className="zh-input"
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
                  aria-label="Filtrar por estado"
                >
                  <option value="all">Todos</option>
                  <option value="activo">Activos</option>
                  <option value="pendiente">Pendientes</option>
                  <option value="inactivo">Inactivos</option>
                </select>
              </div>
              <div className="pg-table-controls-right">
                <span>{filtered.length} de {proveedores.length} proveedores</span>
              </div>
            </div>

            {/* Table */}
            {loading ? (
              <LoadingState />
            ) : filtered.length === 0 ? (
              <EmptyState message={proveedores.length === 0 ? 'No hay proveedores registrados.' : 'No hay resultados para los filtros aplicados.'} />
            ) : (
              <>
                <div className="prv-table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th>RUC</th>
                        <th>Nombre / Razón Social</th>
                        <th>Contacto Principal</th>
                        <th>Teléfono</th>
                        <th>Estado</th>
                        {canEdit ? <th style={{ textAlign: 'right' }}>Acciones</th> : null}
                      </tr>
                    </thead>
                    <tbody>
                      {filtered.map((prov) => (
                        <tr key={prov.id} style={{ opacity: prov.status === 'inactivo' ? 0.65 : 1 }}>
                          <td className="mono subtle" style={{ whiteSpace: 'nowrap' }}>{prov.ruc}</td>
                          <td>
                            <div className="prv-supplier-name">{prov.razonSocial}</div>
                            {prov.category && (
                              <div className="prv-contact-sub">{prov.category}</div>
                            )}
                          </td>
                          <td>{prov.contactoPrincipal ?? <span className="subtle">—</span>}</td>
                          <td className="mono">{prov.phone ?? <span className="subtle">—</span>}</td>
                          <td>
                            <span className={statusClass(prov.status)}>
                              {statusLabel(prov.status)}
                            </span>
                          </td>
                          {canEdit ? (
                            <td style={{ textAlign: 'right' }}>
                              <div className="prv-actions-cell" style={{ justifyContent: 'flex-end' }}>
                                <button
                                  type="button"
                                  className="zh-btn zh-btn--ghost zh-btn--sm"
                                  title="Editar"
                                  onClick={() => openEditModal(prov)}
                                  aria-label="Editar proveedor"
                                >
                                  <span className="material-symbols-outlined">edit</span>
                                </button>
                                <button
                                  type="button"
                                  className={`zh-btn zh-btn--ghost zh-btn--sm ${prov.status === 'activo' ? 'prv-btn-block' : 'prv-btn-activate'}`}
                                  title={prov.status === 'activo' ? 'Desactivar' : 'Activar'}
                                  disabled={saving}
                                  onClick={() => void handleToggleStatus(prov)}
                                  aria-label={prov.status === 'activo' ? 'Desactivar proveedor' : 'Activar proveedor'}
                                >
                                  <span className="material-symbols-outlined">
                                    {prov.status === 'activo' ? 'block' : 'check_circle'}
                                  </span>
                                </button>
                              </div>
                            </td>
                          ) : null}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <div className="pg-table-footer">
                  <p className="subtle" style={{ fontSize: 12, margin: 0 }}>
                    {filtered.length} resultados
                  </p>
                  <p className="pg-table-timestamp">Actualizado en esta sesión</p>
                </div>
              </>
            )}

            {/* ── Secondary panels ── */}
            <div className="prv-panels-grid">

              {/* Notas de Gestión */}
              <div className="pg-kpi" style={{ flexDirection: 'column', gap: 'var(--space-3)', height: 'auto' }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <p className="pg-kpi-label" style={{ margin: 0 }}>Notas de Gestión</p>
                  <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" aria-label="Agregar nota">
                    <span className="material-symbols-outlined">add</span>
                  </button>
                </div>
                <div className="prv-notes-grid">
                  <div className="prv-note-card">
                    <p className="prv-note-title">Renovación de contrato</p>
                    <p className="prv-note-body">Contactar al proveedor principal para renovar el acuerdo marco antes de fin de trimestre.</p>
                    <p className="prv-note-meta">Hace 2 días · admin@zhtechnologies.com</p>
                  </div>
                  <div className="prv-note-card prv-note-card--warning">
                    <p className="prv-note-title">Pagos pendientes</p>
                    <p className="prv-note-body">3 facturas sin confirmar por parte del proveedor de logística.</p>
                    <p className="prv-note-meta">Hace 5 días · admin@zhtechnologies.com</p>
                  </div>
                </div>
              </div>

              {/* Resumen de Pagos */}
              <div className="prv-payment-card">
                <p className="prv-payment-title">Resumen de Pagos</p>
                <div>
                  <p className="prv-payment-amount">$0.00</p>
                  <p className="prv-payment-sub">Total acumulado esta sesión</p>
                </div>
                <div style={{ display: 'flex', gap: 'var(--space-3)', flexWrap: 'wrap' }}>
                  <ZHBtn variant="primary" size="md" type="button">
                    <span className="material-symbols-outlined">receipt_long</span>
                    Ver Pagos
                  </ZHBtn>
                  <ZHBtn variant="ghost" size="md" type="button">
                    Generar Reporte
                  </ZHBtn>
                </div>
              </div>
            </div>
          </>
        )}

        {/* ── Formulario tab ── */}
        {activeTab === 'formulario' && canCreate && (
          <div style={{ padding: 'var(--space-4) var(--space-5)' }}>
            <form onSubmit={onSubmit}>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label="RUC" required error={errors.ruc?.message}>
                  <input className="zh-input" placeholder="10 o 13 dígitos" disabled={saving} {...register('ruc')} />
                </ZHField>
                <ZHField label="Razón Social" required error={errors.razonSocial?.message}>
                  <input className="zh-input" placeholder="Nombre legal del proveedor" disabled={saving} {...register('razonSocial')} />
                </ZHField>
              </div>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label="Nombre Comercial">
                  <input className="zh-input" placeholder="Nombre de marca o comercial" disabled={saving} {...register('nombreComercial')} />
                </ZHField>
                <ZHField label="Contacto Principal">
                  <input className="zh-input" placeholder="Nombre del representante" disabled={saving} {...register('contactoPrincipal')} />
                </ZHField>
              </div>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label="Email" error={errors.email?.message}>
                  <input className="zh-input" type="email" placeholder="proveedor@empresa.com" disabled={saving} {...register('email')} />
                </ZHField>
                <ZHField label="Teléfono">
                  <input className="zh-input" placeholder="0999123456" disabled={saving} {...register('phone')} />
                </ZHField>
              </div>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label="Categoría">
                  <select className="zh-input" disabled={saving} {...register('category')}>
                    <option value="">— seleccionar —</option>
                    {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
                  </select>
                </ZHField>
                <ZHField label="Estado">
                  <select className="zh-input" disabled={saving} {...register('status')}>
                    <option value="activo">Activo</option>
                    <option value="pendiente">Pendiente</option>
                    <option value="inactivo">Inactivo</option>
                  </select>
                </ZHField>
              </div>
              <ZHField label="Dirección">
                <textarea className="zh-input" rows={2} disabled={saving} {...register('address')} />
              </ZHField>
              <ZHField label="Sitio Web">
                <input className="zh-input" type="url" placeholder="https://proveedor.com" disabled={saving} {...register('website')} />
              </ZHField>
              <div className="prv-modal-actions">
                <ZHBtn variant="ghost" size="md" type="button" onClick={() => setActiveTab('listado')}>
                  Cancelar
                </ZHBtn>
                <ZHBtn variant="primary" size="md" type="submit" disabled={saving}>
                  {saving ? t('common.saving') : 'Guardar Proveedor'}
                </ZHBtn>
              </div>
            </form>
          </div>
        )}
      </div>

      {/* ── Create / Edit modal ── */}
      {modalOpen && (
        <div
          className="zh-modal-overlay"
          role="dialog"
          aria-modal="true"
          aria-label={editingId ? 'Editar proveedor' : 'Nuevo proveedor'}
          onClick={(e) => { if (e.target === e.currentTarget) closeModal(); }}
        >
          <div className="zh-modal">
            <div className="zh-modal-header">
              <h2 className="zh-modal-title">
                {editingId ? 'Editar Proveedor' : 'Nuevo Proveedor'}
              </h2>
              <button type="button" className="zh-modal-close" onClick={closeModal} aria-label="Cerrar">✕</button>
            </div>
            <div className="zh-modal-body">
              <form onSubmit={onSubmit}>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label="RUC" required error={errors.ruc?.message}>
                    <input className="zh-input" placeholder="10 o 13 dígitos" disabled={saving} {...register('ruc')} />
                  </ZHField>
                  <ZHField label="Razón Social" required error={errors.razonSocial?.message}>
                    <input className="zh-input" placeholder="Nombre legal" disabled={saving} {...register('razonSocial')} />
                  </ZHField>
                </div>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label="Contacto Principal">
                    <input className="zh-input" disabled={saving} {...register('contactoPrincipal')} />
                  </ZHField>
                  <ZHField label="Teléfono">
                    <input className="zh-input" placeholder="0999123456" disabled={saving} {...register('phone')} />
                  </ZHField>
                </div>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label="Email" error={errors.email?.message}>
                    <input className="zh-input" type="email" disabled={saving} {...register('email')} />
                  </ZHField>
                  <ZHField label="Categoría">
                    <select className="zh-input" disabled={saving} {...register('category')}>
                      <option value="">— seleccionar —</option>
                      {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
                    </select>
                  </ZHField>
                </div>
                <ZHField label="Estado">
                  <select className="zh-input" disabled={saving} {...register('status')}>
                    <option value="activo">Activo</option>
                    <option value="pendiente">Pendiente</option>
                    <option value="inactivo">Inactivo</option>
                  </select>
                </ZHField>
                <div className="prv-modal-actions">
                  <ZHBtn variant="ghost" size="md" type="button" onClick={closeModal}>Cancelar</ZHBtn>
                  <ZHBtn variant="primary" size="md" type="submit" disabled={saving}>
                    {saving ? t('common.saving') : 'Guardar'}
                  </ZHBtn>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
