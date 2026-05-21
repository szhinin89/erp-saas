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
      <div className="zh-modal pg-modal--lg">
        <div className="zh-modal-header">
          <div className="pg-modal-title-row">
            <span className="material-symbols-outlined pg-icon-22 pg-icon-primary">warehouse</span>
            <h2 className="zh-modal-title">{editingId ? 'Editar Bodega' : 'Registro de Nueva Bodega'}</h2>
          </div>
          <button type="button" className="zh-modal-close" onClick={onClose} aria-label="Cerrar">
            ✕
          </button>
        </div>

        <div className="zh-modal-body">
          {saveError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />}

          <div className="bod-modal-grid">
            <div className="pg-flex-col-4">
              <div className="pg-section">
                <div className="pg-section-header">
                  <div className="pg-section-header-left">
                    <span className="material-symbols-outlined pg-section-icon">info</span>
                    <span className="pg-section-label">Información General</span>
                  </div>
                </div>
                <div className="pg-section-body">
                  <div className="pg-form-grid pg-form-grid--2">
                    <div className="pg-form-span-full">
                      <ZHField label="Nombre de la Bodega" required error={errors.name?.message}>
                        <input className="zh-input" placeholder="Ej: Almacén Central Norte" disabled={saving} {...register('name')} />
                      </ZHField>
                    </div>
                    <ZHField label="Código">
                      <input
                        className="zh-input mono bod-code-readonly"
                        readOnly
                        value={editingId ? (editCode ?? '—') : 'Auto-generado'}
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
                    <div className="pg-form-span-full">
                      <ZHField label="Sede / Sucursal" required error={errors.branchId?.message}>
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
                    <div className="pg-form-span-full">
                      <ZHField label="Dirección Completa" error={errors.address?.message}>
                        <input className="zh-input" placeholder="Calle, número, colonia..." disabled={saving} {...register('address')} />
                      </ZHField>
                    </div>
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

            <div className="pg-flex-col-4">
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
                  <div className="pg-flex-col-3">
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

              <div className="bod-tip-panel">
                <div className="bod-tip-head">
                  <span className="material-symbols-outlined pg-icon-18 pg-icon-primary">lightbulb</span>
                  <span className="bod-tip-title">Consejo ZH</span>
                </div>
                <p className="bod-tip-text">
                  Optimiza el espacio usando estanterías de doble profundidad para productos de baja rotación y zonas de{' '}
                  <em>cross-docking</em> cerca de los muelles para envíos rápidos.
                </p>
              </div>

              <div className="pg-section">
                <div className="pg-section-body">
                  <div className="bod-meta-stack">
                    <div className="bod-meta-row">
                      <span className="bod-meta-label">Estado de Registro</span>
                      <span className="badge badge--orange badge--md">{editingId ? 'Editando' : 'Borrador'}</span>
                    </div>
                    <div className="bod-meta-row">
                      <span className="bod-meta-label">Fecha</span>
                      <span className="bod-meta-value">{new Date().toLocaleDateString('es')}</span>
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
