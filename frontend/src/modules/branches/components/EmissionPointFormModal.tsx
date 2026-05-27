import { Controller } from 'react-hook-form';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField, ZHToggle } from '../../../components/zh/ZHForm';
import type { EmissionPointsSectionContext } from '../hooks/useEmissionPointsSection';

type Props = Pick<
  EmissionPointsSectionContext,
  'modalOpen' | 'editingId' | 'saving' | 'saveError' | 'register' | 'control' | 'errors' | 'closeModal' | 'save'
>;

export function EmissionPointFormModal({
  modalOpen,
  editingId,
  saving,
  saveError,
  register,
  control,
  errors,
  closeModal,
  save,
}: Props) {
  if (!modalOpen) return null;

  return (
    <div
      className="zh-modal-overlay"
      role="dialog"
      aria-modal="true"
      aria-label={editingId ? 'Editar punto de emisión' : 'Nuevo punto de emisión'}
      onClick={(e) => {
        if (e.target === e.currentTarget) closeModal();
      }}
    >
      <div className="zh-modal">
        <div className="zh-modal-header">
          <h2 className="zh-modal-title">
            {editingId ? 'Editar Punto de Emisión' : 'Nuevo Punto de Emisión'}
          </h2>
          <button type="button" className="zh-modal-close" onClick={closeModal} aria-label="Cerrar">
            ✕
          </button>
        </div>

        <div className="zh-modal-body">
          {saveError && <ZHPageNotice variant="error" message="Error" detail={saveError} />}

          <div className="br-modal-grid">
            {!editingId && (
              <ZHField label="Código" error={errors.code?.message} required>
                <input
                  className="zh-input"
                  placeholder="001"
                  maxLength={3}
                  {...register('code')}
                />
              </ZHField>
            )}

            <ZHField label="Nombre" error={errors.name?.message}>
              <input className="zh-input" placeholder="Caja 1" {...register('name')} />
            </ZHField>

            <Controller
              name="isDefault"
              control={control}
              render={({ field }) => (
                <ZHToggle
                  label="Punto de emisión por defecto"
                  description="Se usará este punto de emisión al crear documentos fiscales sin selección explícita."
                  value={field.value}
                  onChange={field.onChange}
                />
              )}
            />
          </div>
        </div>

        <div className="zh-modal-footer">
          <ZHBtn type="button" variant="ghost" onClick={closeModal} disabled={saving}>
            Cancelar
          </ZHBtn>
          <ZHBtn type="button" variant="primary" onClick={() => void save()} disabled={saving}>
            {saving ? 'Guardando…' : editingId ? 'Guardar cambios' : 'Crear punto de emisión'}
          </ZHBtn>
        </div>
      </div>
    </div>
  );
}
