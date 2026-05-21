import type { FieldErrors, UseFormRegister } from 'react-hook-form';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import type { BranchDto } from '../../../branches/api/branchService';
import { STORAGE_TYPES, type WarehouseFormValues } from '../../../../schemas/inventory/warehouseSchema';

type BodegasPageFormModalProps = {
  t: (key: string) => string;
  open: boolean;
  editingId: string | null;
  editCode: string | null;
  saving: boolean;
  saveError: string;
  branches: BranchDto[];
  register: UseFormRegister<WarehouseFormValues>;
  errors: FieldErrors<WarehouseFormValues>;
  onClose: () => void;
  onSave: () => void;
};

export function BodegasPageFormModal({
  t,
  open,
  editingId,
  editCode,
  saving,
  saveError,
  branches,
  register,
  errors,
  onClose,
  onSave,
}: BodegasPageFormModalProps) {
  if (!open) return null;

  return (
    <div
      className="zh-modal-overlay"
      role="dialog"
      aria-modal="true"
      aria-label={editingId ? 'Editar bodega' : 'Nueva bodega'}
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className="zh-modal" style={{ maxWidth: 'min(900px, 95vw)', width: '100%' }}>
        <div className="zh-modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
            <span className="material-symbols-outlined" style={{ color: 'var(--color-primary)', fontSize: 22 }}>
              warehouse
            </span>
            <h2 className="zh-modal-title">{editingId ? 'Editar Bodega' : 'Registro de Nueva Bodega'}</h2>
          </div>
          <button type="button" className="zh-modal-close" onClick={onClose} aria-label="Cerrar">
            ✕
          </button>
        </div>

        <div className="zh-modal-body">
          {saveError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />}

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-6)', alignItems: 'start' }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
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
                      <input className="zh-input" placeholder="Ej: Almacén Central Norte" disabled={saving} {...register('name')} />
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
                        {STORAGE_TYPES.map((type) => (
                          <option key={type} value={type}>
                            {type}
                          </option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label="Sede / Sucursal" required error={errors.branchId?.message} style={{ gridColumn: '1 / -1' }}>
                      <select className="zh-input" disabled={saving} {...register('branchId')}>
                        <option value="">— seleccionar sucursal —</option>
                        {branches.map((b) => (
                          <option key={b.id} value={b.id}>
                            {b.name}
                          </option>
                        ))}
                      </select>
                    </ZHField>
                  </div>
                </div>
              </div>

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
                      <input className="zh-input" placeholder="Calle, número, colonia..." disabled={saving} {...register('address')} />
                    </ZHField>
                    <ZHField label="Latitud" error={errors.latitude?.message}>
                      <input className="zh-input mono" placeholder="0.000000" disabled={saving} {...register('latitude')} />
                    </ZHField>
                    <ZHField label="Longitud" error={errors.longitude?.message}>
                      <input className="zh-input mono" placeholder="0.000000" disabled={saving} {...register('longitude')} />
                    </ZHField>
                  </div>
                </div>
              </div>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
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
                      <input className="zh-input" type="tel" placeholder="+593 99 999 9999" disabled={saving} {...register('phone')} />
                    </ZHField>
                    <ZHField label="Correo Electrónico" error={errors.email?.message}>
                      <input className="zh-input" type="email" placeholder="bodega@empresa.com" disabled={saving} {...register('email')} />
                    </ZHField>
                  </div>
                </div>
              </div>

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
                      <input className="zh-input" placeholder="Nombre del responsable" disabled={saving} {...register('manager')} />
                    </ZHField>
                    <div className="pg-form-grid pg-form-grid--2">
                      <ZHField label="Capacidad Total (m³)" error={errors.capacity?.message}>
                        <input className="zh-input" type="number" min={0} step={0.01} placeholder="0" disabled={saving} {...register('capacity')} />
                      </ZHField>
                      <ZHField label="Meta Despacho Diario" error={errors.dailyDispatchGoal?.message}>
                        <input className="zh-input" type="number" min={0} step={1} placeholder="0" disabled={saving} {...register('dailyDispatchGoal')} />
                      </ZHField>
                    </div>
                  </div>
                </div>
              </div>

              <div
                style={{
                  background: 'var(--color-surface-container)',
                  borderLeft: '4px solid var(--color-primary)',
                  borderRadius: '0 var(--radius-md) var(--radius-md) 0',
                  padding: 'var(--space-4)',
                }}
              >
                <div style={{ display: 'flex', gap: 'var(--space-2)', alignItems: 'center', marginBottom: 'var(--space-1)' }}>
                  <span className="material-symbols-outlined" style={{ color: 'var(--color-primary)', fontSize: 18 }}>
                    lightbulb
                  </span>
                  <span style={{ fontWeight: 600, color: 'var(--color-primary)', fontSize: 'var(--text-label-md-size)' }}>
                    Consejo ZH
                  </span>
                </div>
                <p style={{ fontSize: 'var(--text-body-sm-size)', color: 'var(--color-text-secondary)', margin: 0, lineHeight: 1.5 }}>
                  Optimiza el espacio usando estanterías de doble profundidad para productos de baja rotación y zonas de{' '}
                  <em>cross-docking</em> cerca de los muelles para envíos rápidos.
                </p>
              </div>

              <div className="pg-section">
                <div className="pg-section-body">
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span
                        style={{
                          fontSize: 'var(--text-label-sm-size)',
                          color: 'var(--color-text-secondary)',
                          textTransform: 'uppercase',
                          letterSpacing: '0.05em',
                        }}
                      >
                        Estado de Registro
                      </span>
                      <span className="badge badge--orange badge--md">{editingId ? 'Editando' : 'Borrador'}</span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span
                        style={{
                          fontSize: 'var(--text-label-sm-size)',
                          color: 'var(--color-text-secondary)',
                          textTransform: 'uppercase',
                          letterSpacing: '0.05em',
                        }}
                      >
                        Fecha
                      </span>
                      <span style={{ fontSize: 'var(--text-body-sm-size)' }}>{new Date().toLocaleDateString('es')}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="pg-actions-bar">
          <div className="pg-actions-info" />
          <div className="pg-actions-buttons">
            <ZHBtn variant="ghost" size="md" type="button" disabled={saving} onClick={onClose}>
              Cancelar
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="button" disabled={saving} onClick={onSave}>
              <span className="material-symbols-outlined">save</span>
              {saving ? t('common.saving') : 'Guardar Bodega'}
            </ZHBtn>
          </div>
        </div>
      </div>
    </div>
  );
}
