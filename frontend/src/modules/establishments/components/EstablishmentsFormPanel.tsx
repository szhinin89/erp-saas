import { useState } from 'react';
import { Controller } from 'react-hook-form';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHField, ZHToggle, ZHGrid, ZHBtn } from '../../../components/zh/ZHForm';
import type { EstablishmentsPageContext } from '../hooks/useEstablishmentsPage';

type Props = Pick<
  EstablishmentsPageContext,
  | 'editingId'
  | 'editingCode'
  | 'editingName'
  | 'saving'
  | 'saveError'
  | 'branches'
  | 'loadingBranches'
  | 'register'
  | 'control'
  | 'errors'
  | 'closePanel'
  | 'save'
>;

export function EstablishmentsFormPanel({
  editingId,
  editingCode,
  editingName,
  saving,
  saveError,
  branches,
  loadingBranches,
  register,
  control,
  errors,
  closePanel,
  save,
}: Props) {
  const isEdit = Boolean(editingId);
  const [activeTab] = useState<'general'>('general');

  return (
    <div>
      {/* Panel header */}
      <div className="cfg-panel-hd">
        <span className="material-symbols-outlined cfg-panel-hd__icon">
          {isEdit ? 'edit' : 'add_circle'}
        </span>
        <div>
          <p className="cfg-panel-hd__title">
            {isEdit ? (editingName ?? 'Editar Establecimiento') : 'Nuevo Establecimiento SRI'}
          </p>
          {isEdit && editingCode && (
            <p className="cfg-panel-hd__sub">Código SRI: {editingCode}</p>
          )}
        </div>
      </div>

      {/* Tabs */}
      <div className="prd-tabs cfg-panel-tabs">
        <button
          type="button"
          className={`prd-tab-btn ${activeTab === 'general' ? 'prd-tab-btn--active' : ''}`}
        >
          General
        </button>
      </div>

      {/* Error */}
      {saveError && (
        <div className="cfg-panel-error">
          <ZHPageNotice variant="error" message="Error" detail={saveError} />
        </div>
      )}

      {/* Form */}
      <form onSubmit={(e) => { e.preventDefault(); void save(); }} noValidate>
        <div className="cfg-panel-body">

          {/* Datos SRI */}
          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">info</span>
                <span className="pg-section-label">Datos SRI</span>
              </div>
            </div>
            <div className="pg-section-body">
              <ZHGrid cols={2}>
                <ZHField label="Código SRI" required fieldError={errors.code?.message}>
                  {isEdit ? (
                    <input
                      className="zh-input mono ep-code-readonly"
                      readOnly
                      value={editingCode ?? '—'}
                    />
                  ) : (
                    <input
                      className="zh-input mono"
                      placeholder="001"
                      maxLength={3}
                      disabled={saving}
                      {...register('code')}
                    />
                  )}
                </ZHField>

                <ZHField label="Sucursal (opcional)" fieldError={errors.branchId?.message}>
                  <Controller
                    name="branchId"
                    control={control}
                    render={({ field }) => (
                      <select
                        className="zh-input"
                        disabled={saving || loadingBranches}
                        value={field.value ?? ''}
                        onChange={(e) => field.onChange(e.target.value || null)}
                      >
                        <option value="">
                          {loadingBranches ? 'Cargando…' : '— Sin sucursal —'}
                        </option>
                        {branches.map((b) => (
                          <option key={b.id} value={b.id}>{b.name}</option>
                        ))}
                      </select>
                    )}
                  />
                </ZHField>
              </ZHGrid>

              <ZHField label="Nombre del establecimiento" required fieldError={errors.name?.message}>
                <input
                  className="zh-input"
                  placeholder="Ej: Casa Matriz"
                  maxLength={200}
                  disabled={saving}
                  {...register('name')}
                />
              </ZHField>

              <ZHField label="Dirección fiscal" required fieldError={errors.address?.message}>
                <input
                  className="zh-input"
                  placeholder="Ej: Av. Amazonas N12-34 y Colón"
                  maxLength={500}
                  disabled={saving}
                  {...register('address')}
                />
              </ZHField>

              <ZHField label="Teléfono" fieldError={errors.phone?.message}>
                <input
                  className="zh-input"
                  placeholder="Ej: 02-2234567"
                  maxLength={40}
                  disabled={saving}
                  {...register('phone')}
                />
              </ZHField>
            </div>
          </div>

          {/* Configuración */}
          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">star</span>
                <span className="pg-section-label">Configuración</span>
              </div>
            </div>
            <div className="pg-section-body">
              <Controller
                name="isMain"
                control={control}
                render={({ field }) => (
                  <ZHToggle
                    label="Establecimiento principal"
                    description="Se usará este establecimiento como referencia principal de la empresa."
                    value={field.value}
                    onChange={field.onChange}
                    disabled={saving}
                  />
                )}
              />
            </div>
          </div>

        </div>

        {/* Actions bar */}
        <div className="pg-actions-bar">
          <div className="pg-actions-buttons">
            <ZHBtn variant="ghost" type="button" onClick={closePanel} disabled={saving}>
              Cancelar
            </ZHBtn>
            <ZHBtn variant="primary" type="submit" disabled={saving}>
              {saving ? 'Guardando…' : isEdit ? 'Guardar cambios' : 'Crear establecimiento'}
            </ZHBtn>
          </div>
        </div>
      </form>
    </div>
  );
}
