import type { FieldValues } from "react-hook-form";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHFormActions } from "../../../../components/zh/ZHForm";
import { ZHModal } from "../../../../components/zh/ZHModal";
import { useI18n } from "../../../../i18n/i18n";
import type { CatalogCrudContext } from "../hooks/useCatalogCrud";

interface Props<
  TDto extends { isActive: boolean; name: string },
  TForm extends FieldValues,
> {
  ctx: CatalogCrudContext<TDto, TForm>;
  entityLabel: string;
  children: React.ReactNode;
}

export function CatalogFormModal<
  TDto extends { isActive: boolean; name: string },
  TForm extends FieldValues,
>({ ctx, entityLabel, children }: Props<TDto, TForm>) {
  const { modalOpen, editingId, saving, saveError, closeModal, save } = ctx;
  const { t } = useI18n();

  const isEdit = Boolean(editingId);
  const entity = entityLabel.toLocaleLowerCase();

  return (
    <ZHModal
      open={modalOpen}
      onClose={closeModal}
      title={
        isEdit
          ? t("catalog.modal.editTitle", { entity: entityLabel })
          : t("catalog.modal.newTitle", { entity: entityLabel })
      }
      subtitle={t("catalog.modal.subtitle", { entity })}
      footer={
        <ZHFormActions
          onCancel={closeModal}
          onSave={() => void save()}
          hideDraft
          disableSave={saving}
          labels={{
            cancel: t("common.cancel", "Cancelar"),
            save: saving
              ? t("common.saving", "Guardando...")
              : isEdit
                ? t("common.saveChanges", "Guardar cambios")
                : t("catalog.modal.createAction", { entity }),
          }}
        />
      }
    >
      {saveError && (
        <ZHPageNotice
          variant="error"
          message={t("common.error", "Error")}
          detail={saveError}
        />
      )}
      <div className="pg-section">
        <div className="pg-section-body">{children}</div>
      </div>
    </ZHModal>
  );
}
