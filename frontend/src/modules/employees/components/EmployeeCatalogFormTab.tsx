import type { FormEventHandler } from "react";
import type { UseFormReturn } from "react-hook-form";
import { useI18n } from "../../../i18n/i18n";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { CATALOG_NAME_MAX_LENGTH, type CatalogEntryValues } from "../schemas/employeeSchemas";

type Props = {
  form: UseFormReturn<CatalogEntryValues>;
  editing: boolean;
  saving: boolean;
  saveError: string;
  onSubmit: FormEventHandler<HTMLFormElement>;
  onCancel: () => void;
};

export function EmployeeCatalogFormTab({ form, editing, saving, saveError, onSubmit, onCancel }: Props) {
  const { t } = useI18n();
  const { errors } = form.formState;
  const name = t("employees.fields.name", "Nombre");

  return (
    <form className="card-body prd-fadein" onSubmit={onSubmit} noValidate>
      {saveError && <ZHPageNotice variant="error" message={saveError} />}
      <ZHField label={name} required fieldError={errors.name?.message}>
        <ZhTextInput
          disabled={saving}
          maxLength={CATALOG_NAME_MAX_LENGTH}
          aria-required="true"
          aria-label={name}
          {...form.register("name")}
        />
      </ZHField>
      <div className="zh-form-actions-row zh-form-actions-row--end">
        <ZHBtn variant="ghost" size="md" type="button" disabled={saving} onClick={onCancel}>
          {t("common.cancel", "Cancelar")}
        </ZHBtn>
        <ZHBtn variant="primary" size="md" type="submit" icon="save" disabled={saving}>
          {saving
            ? t("common.saving", "Guardando...")
            : editing
              ? t("common.save", "Guardar")
              : t("employees.catalogs.create", "Crear")}
        </ZHBtn>
      </div>
    </form>
  );
}
