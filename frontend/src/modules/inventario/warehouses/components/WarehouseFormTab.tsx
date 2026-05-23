import type { FieldErrors, UseFormRegister } from 'react-hook-form';
import { useI18n } from '../../../../i18n/i18n';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import type { BranchDto } from '../../../branches/api/branchService';
import { STORAGE_TYPES, type WarehouseFormValues } from '../../../../schemas/inventory/warehouseSchema';

interface Props {
  editingId: string | null;
  editCode: string | null;
  saving: boolean;
  saveError: string;
  branches: BranchDto[];
  register: UseFormRegister<WarehouseFormValues>;
  errors: FieldErrors<WarehouseFormValues>;
  onSave: () => void;
  onCancel: () => void;
}

export function WarehouseFormTab({
  editingId, editCode, saving, saveError,
  branches, register, errors,
  onSave, onCancel,
}: Props) {
  const { t } = useI18n();

  return (
    <div className="bod-form-tab prd-fadein">

      {/* Panel header */}
      <div className="bod-form-head">
        <span className="bod-form-head__icon material-symbols-outlined">
          {editingId ? 'edit' : 'add_circle'}
        </span>
        <div>
          <h3 className="bod-form-head__title">
            {editingId
              ? t('warehouses.form.title.edit', 'Editar Bodega')
              : t('warehouses.form.title.new', 'Registro de Nueva Bodega')}
          </h3>
        </div>
      </div>

      {saveError && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix', 'Error:')} detail={saveError} />
      )}

      <div className="bod-form-grid">

        {/* ── Columna izquierda ─────────────────────────────── */}
        <div className="bod-form-col">

          {/* Información General */}
          <div className="bod-form-section">
            <div className="bod-form-section__head">
              <span className="material-symbols-outlined bod-form-section__icon">info</span>
              <span className="bod-form-section__label">{t('warehouses.form.section.general', 'Información General')}</span>
            </div>
            <div className="bod-form-section__body">
              <div className="pg-form-grid pg-form-grid--2">
                <div className="pg-form-span-full">
                  <ZHField
                    label={t('warehouses.form.field.name', 'Nombre de la Bodega')}
                    required
                    error={errors.name?.message}
                  >
                    <input
                      className="zh-input"
                      placeholder={t('warehouses.form.field.name.placeholder', 'Ej: Almacén Central Norte')}
                      disabled={saving}
                      aria-required="true"
                      {...register('name')}
                    />
                  </ZHField>
                </div>

                <ZHField label={t('warehouses.form.field.code', 'Código')}>
                  <input
                    className="zh-input mono bod-code-readonly"
                    readOnly
                    value={editingId ? (editCode ?? '—') : t('warehouses.form.field.code.auto', 'Auto-generado')}
                    aria-readonly="true"
                  />
                </ZHField>

                <ZHField label={t('warehouses.form.field.storageType', 'Tipo de Almacenamiento')}>
                  <select className="zh-input" disabled={saving} {...register('storageType')}>
                    <option value="">{t('warehouses.form.field.storageType.placeholder', '— seleccionar —')}</option>
                    {STORAGE_TYPES.map((type) => (
                      <option key={type} value={type}>{type}</option>
                    ))}
                  </select>
                </ZHField>

                <div className="pg-form-span-full">
                  <ZHField
                    label={t('warehouses.form.field.branch', 'Sede / Sucursal')}
                    required
                    error={errors.branchId?.message}
                  >
                    <select
                      className="zh-input"
                      disabled={saving}
                      aria-required="true"
                      {...register('branchId')}
                    >
                      <option value="">{t('warehouses.form.field.branch.placeholder', '— seleccionar sucursal —')}</option>
                      {branches.map((b) => (
                        <option key={b.id} value={b.id}>{b.name}</option>
                      ))}
                    </select>
                  </ZHField>
                </div>
              </div>
            </div>
          </div>

          {/* Ubicación */}
          <div className="bod-form-section">
            <div className="bod-form-section__head">
              <span className="material-symbols-outlined bod-form-section__icon">location_on</span>
              <span className="bod-form-section__label">{t('warehouses.form.section.location', 'Detalles de Ubicación')}</span>
            </div>
            <div className="bod-form-section__body">
              <div className="pg-form-grid pg-form-grid--2">
                <div className="pg-form-span-full">
                  <ZHField label={t('warehouses.form.field.address', 'Dirección Completa')} error={errors.address?.message}>
                    <input
                      className="zh-input"
                      placeholder={t('warehouses.form.field.address.placeholder', 'Calle, número, colonia...')}
                      disabled={saving}
                      {...register('address')}
                    />
                  </ZHField>
                </div>
                <ZHField label={t('warehouses.form.field.lat', 'Latitud')} error={errors.latitude?.message}>
                  <input className="zh-input mono" placeholder="0.000000" disabled={saving} {...register('latitude')} />
                </ZHField>
                <ZHField label={t('warehouses.form.field.lng', 'Longitud')} error={errors.longitude?.message}>
                  <input className="zh-input mono" placeholder="0.000000" disabled={saving} {...register('longitude')} />
                </ZHField>
              </div>
            </div>
          </div>
        </div>

        {/* ── Columna derecha ───────────────────────────────── */}
        <div className="bod-form-col">

          {/* Contacto */}
          <div className="bod-form-section">
            <div className="bod-form-section__head">
              <span className="material-symbols-outlined bod-form-section__icon">contact_phone</span>
              <span className="bod-form-section__label">{t('warehouses.form.section.contact', 'Contacto')}</span>
            </div>
            <div className="bod-form-section__body">
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label={t('warehouses.form.field.phone', 'Teléfono Directo')} error={errors.phone?.message}>
                  <input className="zh-input" type="tel" placeholder="+593 99 999 9999" disabled={saving} {...register('phone')} />
                </ZHField>
                <ZHField label={t('warehouses.form.field.email', 'Correo Electrónico')} error={errors.email?.message}>
                  <input className="zh-input" type="email" placeholder="bodega@empresa.com" disabled={saving} {...register('email')} />
                </ZHField>
              </div>
            </div>
          </div>

          {/* Operaciones */}
          <div className="bod-form-section">
            <div className="bod-form-section__head">
              <span className="material-symbols-outlined bod-form-section__icon">monitoring</span>
              <span className="bod-form-section__label">{t('warehouses.form.section.operations', 'Operaciones y Metas')}</span>
            </div>
            <div className="bod-form-section__body">
              <div className="pg-form-grid pg-form-grid--1">
                <ZHField label={t('warehouses.form.field.manager', 'Jefe de Logística')}>
                  <input
                    className="zh-input"
                    placeholder={t('warehouses.form.field.manager.placeholder', 'Nombre del responsable')}
                    disabled={saving}
                    {...register('manager')}
                  />
                </ZHField>
              </div>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label={t('warehouses.form.field.capacity', 'Capacidad Total (m³)')} error={errors.capacity?.message}>
                  <input className="zh-input" type="number" min={0} step={0.01} placeholder="0" disabled={saving} {...register('capacity')} />
                </ZHField>
                <ZHField label={t('warehouses.form.field.dailyGoal', 'Meta Despacho Diario')} error={errors.dailyDispatchGoal?.message}>
                  <input className="zh-input" type="number" min={0} step={1} placeholder="0" disabled={saving} {...register('dailyDispatchGoal')} />
                </ZHField>
              </div>
            </div>
          </div>

          {/* Tip */}
          <div className="bod-tip-panel">
            <div className="bod-tip-head">
              <span className="material-symbols-outlined pg-icon-18 pg-icon-primary">lightbulb</span>
              <span className="bod-tip-title">{t('warehouses.form.tip.title', 'Consejo ZH')}</span>
            </div>
            <p className="bod-tip-text">{t('warehouses.form.tip.text', 'Optimiza el espacio usando estanterías de doble profundidad.')}</p>
          </div>

          {/* Meta info */}
          <div className="bod-form-meta">
            <div className="bod-meta-row">
              <span className="bod-meta-label">{t('warehouses.form.status.label', 'Estado de Registro')}</span>
              <span className={`badge badge--md ${editingId ? 'badge--blue' : 'badge--gray'}`}>
                {editingId
                  ? t('warehouses.form.status.editing', 'Editando')
                  : t('warehouses.form.status.draft', 'Borrador')}
              </span>
            </div>
            <div className="bod-meta-row">
              <span className="bod-meta-label">{t('warehouses.form.date.label', 'Fecha')}</span>
              <span className="bod-meta-value">{new Date().toLocaleDateString('es')}</span>
            </div>
          </div>
        </div>
      </div>

      {/* ── Footer ─────────────────────────────────────────── */}
      <div className="bod-form-footer">
        <div />
        <div className="bod-form-footer__actions">
          <ZHBtn variant="ghost" size="md" type="button" disabled={saving} onClick={onCancel}>
            {t('warehouses.form.btn.cancel', 'Cancelar')}
          </ZHBtn>
          <ZHBtn variant="primary" size="md" type="button" disabled={saving} onClick={onSave}>
            <span className="material-symbols-outlined">save</span>
            {saving
              ? t('common.saving', 'Guardando…')
              : editingId
                ? t('warehouses.form.btn.update', 'Actualizar Bodega')
                : t('warehouses.form.btn.save', 'Guardar Bodega')}
          </ZHBtn>
        </div>
      </div>
    </div>
  );
}
