import { Controller } from 'react-hook-form';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField, ZHToggle } from '../../../components/zh/ZHForm';
import type { EstablishmentsSectionContext } from '../hooks/useEstablishmentsSection';

type Props = Pick<
  EstablishmentsSectionContext,
  'modalOpen' | 'editingId' | 'saving' | 'saveError' | 'register' | 'control' | 'errors' | 'closeModal' | 'save'
>;

export function EstablishmentFormModal({
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
      aria-label={editingId ? 'Editar establecimiento' : 'Nuevo establecimiento'}
      onClick={(e) => {
        if (e.target === e.currentTarget) closeModal();
      }}
    >
      <div className="zh-modal">
        <div className="zh-modal-header">
          <h2 className="zh-modal-title">
            {editingId ? 'Editar Establecimiento' : 'Nuevo Establecimiento'}
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

            <ZHField label="Nombre" error={errors.name?.message} required>
              <input className="zh-input" placeholder="Nombre del establecimiento" {...register('name')} />
            </ZHField>

            <ZHField label="Dirección" error={errors.address?.message} required>
              <input className="zh-input" placeholder="Dirección completa" {...register('address')} />
            </ZHField>

            <ZHField label="Teléfono" error={errors.phone?.message}>
              <input className="zh-input" placeholder="02-xxx-xxxx" {...register('phone')} />
            </ZHField>

            <Controller
              name="isMain"
              control={control}
              render={({ field }) => (
                <ZHToggle
                  label="Establecimiento principal"
                  description="Este establecimiento es el establecimiento SRI principal de la sucursal."
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
            {saving ? 'Guardando…' : editingId ? 'Guardar cambios' : 'Crear establecimiento'}
          </ZHBtn>
        </div>
      </div>
    </div>
  );
}
