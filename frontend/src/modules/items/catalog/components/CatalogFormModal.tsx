import type { FieldValues } from 'react-hook-form';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHFormActions } from '../../../../components/zh/ZHForm';
import { ZHModal } from '../../../../components/zh/ZHModal';
import type { CatalogCrudContext } from '../hooks/useCatalogCrud';

interface Props<TDto extends { isActive: boolean; name: string }, TForm extends FieldValues> {
  ctx: CatalogCrudContext<TDto, TForm>;
  entityLabel: string;
  children: React.ReactNode;
}

export function CatalogFormModal<TDto extends { isActive: boolean; name: string }, TForm extends FieldValues>(
  { ctx, entityLabel, children }: Props<TDto, TForm>,
) {
  const { modalOpen, editingId, saving, saveError, closeModal, save } = ctx;

  const isEdit = Boolean(editingId);

  return (
    <ZHModal
      open={modalOpen}
      onClose={closeModal}
      title={isEdit ? `Editar ${entityLabel}` : `Nuevo ${entityLabel}`}
      subtitle={`Configure los datos del ${entityLabel.toLowerCase()}.`}
      footer={
        <ZHFormActions
          onCancel={closeModal}
          onSave={() => void save()}
          hideDraft
          disableSave={saving}
          labels={{
            cancel: 'Cancelar',
            save: saving ? 'Guardando…' : isEdit ? 'Guardar cambios' : `Crear ${entityLabel.toLowerCase()}`,
          }}
        />
      }
    >
          {saveError && <ZHPageNotice variant="error" message="Error" detail={saveError} />}
          <div className="pg-section">
            <div className="pg-section-body">
              {children}
            </div>
          </div>
    </ZHModal>
  );
}
