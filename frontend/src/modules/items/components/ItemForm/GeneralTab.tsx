import { useFormContext } from "react-hook-form";
import {
  ZHField,
  ZHFormSection,
  ZHGrid,
} from "../../../../components/zh/ZHForm";
import { ZhSelect } from "../../../../components/zh/inputs";
import type { CreateItemFormValues } from "../../schemas/createItemSchema";

type Props = {
  t: (key: string, fallback?: string) => string;
  disabled: boolean;
  isEditMode: boolean;
  categoryOptions: { id: string; name: string; depth: number }[];
  brandOptions: { id: string; name: string }[];
  sriUomOptions: { code: string; name: string }[];
  itemTypeOptions: { id: string; name: string }[];
};

export function GeneralTab({
  t,
  disabled,
  isEditMode,
  categoryOptions,
  brandOptions,
  sriUomOptions,
  itemTypeOptions,
}: Props) {
  const {
    register,
    formState: { errors },
  } = useFormContext<CreateItemFormValues>();
  const fe = (msg?: string) => (msg ? t(msg, msg) : null);

  return (
    <ZHFormSection
      title={t("items.section.identity", "Información")}
      description={t(
        "items.section.identityDesc",
        "Datos que identifican y clasifican el ítem: cómo se llama, de qué tipo es, a qué marca/categoría pertenece, y su unidad de medida base.",
      )}
    >
      <ZHGrid cols={3}>
        <ZHField
          label={t("items.form.sku", "SKU")}
          required
          fieldError={fe(errors.sku?.message)}
        >
          <input
            {...register("sku")}
            placeholder={t("items.form.skuPlaceholder", "CAMISA-ROJA")}
            disabled={disabled}
            className="zh-input--upper"
          />
        </ZHField>
        <ZHField
          label={t("items.form.shortName", "Nombre corto")}
          required
          fieldError={fe(errors.shortName?.message)}
        >
          <input
            {...register("shortName")}
            placeholder={t("items.form.shortNamePlaceholder", "Camisa Roja L")}
            disabled={disabled}
          />
        </ZHField>
        <ZHField
          label={t("items.form.itemType", "Tipo")}
          required
          fieldError={fe(errors.itemTypeId?.message)}
        >
          <ZhSelect {...register("itemTypeId")} disabled={disabled || isEditMode}>
            <option value="">
              {t("common.selectOption", "— Seleccionar —")}
            </option>
            {itemTypeOptions.map((it) => (
              <option key={it.id} value={it.id}>
                {it.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </ZHGrid>
      <ZHField
        label={t("items.form.description", "Descripción")}
        required
        fieldError={fe(errors.description?.message)}
      >
        <input
          {...register("description")}
          placeholder={t(
            "items.form.descriptionPlaceholder",
            "Descripción completa del ítem",
          )}
          disabled={disabled}
        />
      </ZHField>
      <ZHGrid cols={3}>
        <ZHField
          label={t("items.form.brand", "Marca")}
          required={!isEditMode}
          fieldError={fe((errors as any).brandId?.message)}
        >
          <ZhSelect {...register("brandId")} disabled={disabled}>
            <option value="">
              {t("common.selectOption", "— Seleccionar —")}
            </option>
            {brandOptions.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHField
          label={t("items.form.category", "Categoría")}
          required={!isEditMode}
          fieldError={fe((errors as any).categoryNodeId?.message)}
        >
          <ZhSelect {...register("categoryNodeId")} disabled={disabled}>
            <option value="">
              {t("common.selectOption", "— Seleccionar —")}
            </option>
            {categoryOptions.map((c) => (
              <option key={c.id} value={c.id}>
                {"  ".repeat(c.depth)}
                {c.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHField
          label={t("items.form.uom", "UOM base")}
          required
          fieldError={fe(errors.defaultUomCode?.message)}
        >
          <ZhSelect {...register("defaultUomCode")} disabled={disabled}>
            <option value="">
              {t("common.selectOption", "— Seleccionar —")}
            </option>
            {sriUomOptions.map((u) => (
              <option key={u.code} value={u.code}>
                {u.code} — {u.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </ZHGrid>
    </ZHFormSection>
  );
}
