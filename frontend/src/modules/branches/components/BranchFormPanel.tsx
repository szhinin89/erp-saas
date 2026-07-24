import { useState } from 'react';
import { Controller } from 'react-hook-form';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHField, ZHToggle, ZHGrid, ZHBtn } from '../../../components/zh/ZHForm';
import { ZhPhoneInput, ZhDateInput } from '../../../components/zh/inputs';
import { type BranchesPageContext } from '../hooks/useBranchesPage';

type Props = Pick<
  BranchesPageContext,
  | 't'
  | 'editingId'
  | 'editCode'
  | 'editName'
  | 'saving'
  | 'saveError'
  | 'countries'
  | 'provinces'
  | 'cantons'
  | 'parishes'
  | 'loadingProvinces'
  | 'loadingCantons'
  | 'loadingParishes'
  | 'register'
  | 'control'
  | 'errors'
  | 'formWatch'
  | 'onCountryChange'
  | 'onProvinceChange'
  | 'onCantonChange'
  | 'closePanel'
  | 'save'
>;

export function BranchFormPanel({
  t,
  editingId,
  editCode,
  editName,
  saving,
  saveError,
  countries,
  provinces,
  cantons,
  parishes,
  loadingProvinces,
  loadingCantons,
  loadingParishes,
  register,
  control,
  errors,
  formWatch,
  onCountryChange,
  onProvinceChange,
  onCantonChange,
  closePanel,
  save,
}: Props) {
  const [activeTab] = useState<'general'>('general');

  return (
    <div>
      {/* Panel header */}
      <div className="cfg-panel-hd">
        <span className="material-symbols-outlined cfg-panel-hd__icon">
          {editingId ? 'edit' : 'add_circle'}
        </span>
        <div>
          <p className="cfg-panel-hd__title">
            {editingId ? (editName ?? 'Editar Sucursal') : 'Nueva Sucursal'}
          </p>
          {editingId && editCode && (
            <p className="cfg-panel-hd__sub">Código: {editCode}</p>
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
          <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />
        </div>
      )}

      {/* Form */}
      <form
        onSubmit={(e) => { e.preventDefault(); void save(); }}
        noValidate
      >
        <div className="cfg-panel-body">

          {/* Identificación */}
          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">info</span>
                <span className="pg-section-label">Identificación</span>
              </div>
            </div>
            <div className="pg-section-body">
              <ZHGrid cols={2}>
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
                    className="zh-input mono br-code-readonly"
                    readOnly
                    value={editingId ? (editCode ?? '—') : 'Auto-generado'}
                  />
                </ZHField>
                <ZHField label="Descripción" error={errors.description?.message}>
                  <input
                    className="zh-input"
                    placeholder="Descripción breve"
                    disabled={saving}
                    {...register('description')}
                  />
                </ZHField>
              </ZHGrid>
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

          {/* Dirección */}
          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">location_on</span>
                <span className="pg-section-label">Dirección</span>
              </div>
            </div>
            <div className="pg-section-body">
              <ZHGrid cols={2}>
                <ZHField label="País">
                  <select
                    className="zh-input"
                    disabled={saving || countries.length === 0}
                    {...register('countryId', {
                      onChange: async (e) => { await onCountryChange(e.target.value); },
                    })}
                  >
                    <option value="">— seleccionar —</option>
                    {countries.map((c) => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
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
                    {provinces.map((c) => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
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
                    {cantons.map((c) => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </ZHField>

                <ZHField label={loadingParishes ? 'Parroquia (cargando…)' : 'Parroquia'}>
                  <select
                    className="zh-input"
                    disabled={saving || !formWatch.cantonId || loadingParishes}
                    {...register('parishId')}
                  >
                    <option value="">— seleccionar —</option>
                    {parishes.map((c) => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </ZHField>
              </ZHGrid>

              <ZHField label="Dirección" required error={errors.address?.message}>
                <input
                  className="zh-input"
                  placeholder="Calle, número, colonia..."
                  disabled={saving}
                  {...register('address')}
                />
              </ZHField>

              <ZHGrid cols={2}>
                <ZHField label="Referencia">
                  <input
                    className="zh-input"
                    placeholder="Punto de referencia"
                    disabled={saving}
                    {...register('reference')}
                  />
                </ZHField>
                <ZHField label="Código postal" error={errors.postalCode?.message}>
                  <input className="zh-input" placeholder="170150" disabled={saving} {...register('postalCode')} />
                </ZHField>
                <ZHField label="Latitud" error={errors.latitude?.message}>
                  <input className="zh-input mono" placeholder="-0.2295" disabled={saving} {...register('latitude')} />
                </ZHField>
                <ZHField label="Longitud" error={errors.longitude?.message}>
                  <input className="zh-input mono" placeholder="-78.5243" disabled={saving} {...register('longitude')} />
                </ZHField>
              </ZHGrid>
            </div>
          </div>

          {/* Contacto */}
          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">contact_phone</span>
                <span className="pg-section-label">Contacto</span>
              </div>
            </div>
            <div className="pg-section-body">
              <ZHGrid cols={2}>
                <ZHField label="Teléfono principal" error={errors.phone?.message}>
                  <Controller
                    name="phone"
                    control={control}
                    render={({ field }) => <ZhPhoneInput {...field} disabled={saving} />}
                  />
                </ZHField>
                <ZHField label="Teléfono secundario" error={errors.secondaryPhone?.message}>
                  <Controller
                    name="secondaryPhone"
                    control={control}
                    render={({ field }) => <ZhPhoneInput {...field} disabled={saving} />}
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
                <ZHField label="Sitio web" error={errors.website?.message}>
                  <input
                    className="zh-input"
                    placeholder="https://www.empresa.com"
                    disabled={saving}
                    {...register('website')}
                  />
                </ZHField>
              </ZHGrid>
            </div>
          </div>

          {/* Responsable */}
          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">badge</span>
                <span className="pg-section-label">Responsable</span>
              </div>
            </div>
            <div className="pg-section-body">
              <ZHGrid cols={2}>
                <ZHField label="Nombre completo" error={errors.managerName?.message}>
                  <input
                    className="zh-input"
                    placeholder="Nombre del responsable"
                    disabled={saving}
                    {...register('managerName')}
                  />
                </ZHField>
                <ZHField label="Cargo" error={errors.managerPosition?.message}>
                  <input
                    className="zh-input"
                    placeholder="Ej: Administrador"
                    disabled={saving}
                    {...register('managerPosition')}
                  />
                </ZHField>
                <ZHField label="Correo" error={errors.managerEmail?.message}>
                  <input
                    className="zh-input"
                    type="email"
                    placeholder="responsable@empresa.com"
                    disabled={saving}
                    {...register('managerEmail')}
                  />
                </ZHField>
                <ZHField label="Teléfono" error={errors.managerPhone?.message}>
                  <Controller
                    name="managerPhone"
                    control={control}
                    render={({ field }) => <ZhPhoneInput {...field} disabled={saving} />}
                  />
                </ZHField>
              </ZHGrid>
            </div>
          </div>

          {/* Operación */}
          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">settings_input_component</span>
                <span className="pg-section-label">Operación</span>
              </div>
            </div>
            <div className="pg-section-body">
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
              <ZHGrid cols={2}>
                <ZHField label="Fecha de apertura" error={errors.openingDate?.message}>
                  <ZhDateInput disabled={saving} {...register('openingDate')} />
                </ZHField>
              </ZHGrid>
              <ZHField label="Notas internas" error={errors.internalNotes?.message}>
                <textarea
                  className="zh-input"
                  rows={3}
                  maxLength={1000}
                  placeholder="Notas visibles solo para el equipo administrativo"
                  disabled={saving}
                  {...register('internalNotes')}
                />
              </ZHField>
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
              {saving
                ? t('common.saving')
                : editingId
                  ? 'Guardar cambios'
                  : 'Crear sucursal'}
            </ZHBtn>
          </div>
        </div>
      </form>
    </div>
  );
}
