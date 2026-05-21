import { Controller } from 'react-hook-form';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField, ZHToggle } from '../../../components/zh/ZHForm';
import { BRANCH_TYPES, type BranchesPageContext } from '../hooks/useBranchesPage';

type Props = Pick<
  BranchesPageContext,
  | 't'
  | 'modalOpen'
  | 'editingId'
  | 'editCode'
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
  | 'closeModal'
  | 'save'
>;

export function BranchFormModal({
  t,
  modalOpen,
  editingId,
  editCode,
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
  closeModal,
  save,
}: Props) {
  if (!modalOpen) return null;

  return (
    <div
      className="zh-modal-overlay"
      role="dialog"
      aria-modal="true"
      aria-label={editingId ? 'Editar sucursal' : 'Nueva sucursal'}
      onClick={(e) => {
        if (e.target === e.currentTarget) closeModal();
      }}
    >
      <div className="zh-modal" style={{ maxWidth: 'min(900px, 95vw)', width: '100%' }}>
        <div className="zh-modal-header">
          <h2 className="zh-modal-title">{editingId ? 'Editar Sucursal' : 'Nueva Sucursal'}</h2>
          <button type="button" className="zh-modal-close" onClick={closeModal} aria-label="Cerrar">
            ✕
          </button>
        </div>

        <div className="zh-modal-body">
          {saveError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />}

          <div
            style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 'var(--space-6)', alignItems: 'start' }}
          >
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
                        {BRANCH_TYPES.map((type) => (
                          <option key={type} value={type}>
                            {type}
                          </option>
                        ))}
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
                          onChange: async (e) => {
                            await onCountryChange(e.target.value);
                          },
                        })}
                      >
                        <option value="">— seleccionar —</option>
                        {countries.map((c) => (
                          <option key={c.id} value={c.id}>
                            {c.name}
                          </option>
                        ))}
                      </select>
                    </ZHField>

                    <ZHField label={loadingProvinces ? 'Provincia (cargando…)' : 'Provincia'}>
                      <select
                        className="zh-input"
                        disabled={saving || !formWatch.countryId || loadingProvinces}
                        {...register('provinceId', {
                          onChange: async (e) => {
                            await onProvinceChange(e.target.value);
                          },
                        })}
                      >
                        <option value="">— seleccionar —</option>
                        {provinces.map((c) => (
                          <option key={c.id} value={c.id}>
                            {c.name}
                          </option>
                        ))}
                      </select>
                    </ZHField>

                    <ZHField label={loadingCantons ? 'Cantón (cargando…)' : 'Cantón'}>
                      <select
                        className="zh-input"
                        disabled={saving || !formWatch.provinceId || loadingCantons}
                        {...register('cantonId', {
                          onChange: async (e) => {
                            await onCantonChange(e.target.value);
                          },
                        })}
                      >
                        <option value="">— seleccionar —</option>
                        {cantons.map((c) => (
                          <option key={c.id} value={c.id}>
                            {c.name}
                          </option>
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
                          <option key={c.id} value={c.id}>
                            {c.name}
                          </option>
                        ))}
                      </select>
                    </ZHField>

                    <ZHField label="Latitud" error={errors.latitude?.message}>
                      <input className="zh-input mono" placeholder="-0.2295" disabled={saving} {...register('latitude')} />
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

            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
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

              <div
                className="pg-accent-card"
                style={{
                  background: 'var(--color-primary-fixed)',
                  borderRadius: 'var(--radius-lg)',
                  padding: 'var(--space-4)',
                }}
              >
                <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'flex-start' }}>
                  <span
                    className="material-symbols-outlined"
                    style={{ color: 'var(--color-primary)', fontSize: 24, flexShrink: 0, marginTop: 2 }}
                  >
                    lightbulb
                  </span>
                  <div>
                    <p
                      style={{
                        fontWeight: 600,
                        color: 'var(--color-primary)',
                        margin: '0 0 var(--space-1)',
                        fontSize: 'var(--text-label-md-size)',
                      }}
                    >
                      Consejo ZH
                    </p>
                    <p style={{ fontSize: 'var(--text-body-sm-size)', color: 'var(--color-text-secondary)', margin: 0 }}>
                      Asegúrate de asignar un <strong>Gerente</strong> con experiencia en el tipo de sucursal
                      seleccionado para optimizar la meta diaria.
                    </p>
                  </div>
                </div>
              </div>

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

        <div className="pg-actions-bar">
          <div className="pg-actions-info" />
          <div className="pg-actions-buttons">
            <ZHBtn variant="ghost" size="md" type="button" disabled={saving} onClick={closeModal}>
              Cancelar
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="button" disabled={saving} onClick={() => void save()}>
              <span className="material-symbols-outlined">save</span>
              {saving ? t('common.saving') : 'Guardar Sucursal'}
            </ZHBtn>
          </div>
        </div>
      </div>
    </div>
  );
}
