import { Controller, useFieldArray, useFormContext } from "react-hook-form";
import {
  ZHBtn,
  ZHField,
  ZHFormSection,
  ZHGrid,
} from "../../../../components/zh/ZHForm";
import { SupplierPicker } from "../../../purchases/components/SupplierPicker";
import { useMarkPrimaryField } from "../../hooks/useMarkPrimaryField";
import type { CreateItemFormValues } from "../../schemas/createItemSchema";

type Props = {
  t: (key: string, fallback?: string) => string;
  disabled: boolean;
};

export function SupplierCodesSection({ t, disabled }: Props) {
  const {
    register,
    control,
    watch,
    setValue,
    formState: { errors },
  } = useFormContext<CreateItemFormValues>();
  const { fields, append, remove } = useFieldArray({
    control,
    name: "supplierCodes",
  });
  const supplierCodes = watch("supplierCodes") ?? [];

  const listErrorMessage = errors.supplierCodes?.message;
  const listError =
    typeof listErrorMessage === "string" ? listErrorMessage : null;

  const markPrimary = useMarkPrimaryField(
    setValue,
    "supplierCodes",
    fields.length,
  );

  return (
    <ZHFormSection
      title={t(
        "items.supplierCodes.title",
        "Códigos del proveedor para compras",
      )}
      description={t(
        "items.supplierCodes.sectionDesc",
        "Código con el que el proveedor identifica este producto en sus facturas/XML.",
      )}
    >
      {listError && (
        <p className="zh-field-hint zh-field-hint--error">
          {t(listError, listError)}
        </p>
      )}

      {fields.map((field, index) => (
        <ZHGrid cols={4} key={field.id}>
          <ZHField
            label={t("items.supplierCodes.supplier", "Proveedor")}
            required
            fieldError={
              errors.supplierCodes?.[index]?.supplierId?.message
                ? t(
                    errors.supplierCodes[index]!.supplierId!.message!,
                    errors.supplierCodes[index]!.supplierId!.message!,
                  )
                : null
            }
          >
            <Controller
              control={control}
              name={`supplierCodes.${index}.supplierId`}
              render={({ field: rhfField }) => (
                <SupplierPicker
                  value={rhfField.value || null}
                  onChange={(supplier) => rhfField.onChange(supplier?.id ?? "")}
                  disabled={disabled}
                />
              )}
            />
          </ZHField>
          <ZHField
            label={t("items.supplierCodes.code", "Código proveedor")}
            required
            fieldError={
              errors.supplierCodes?.[index]?.code?.message
                ? t(
                    errors.supplierCodes[index]!.code!.message!,
                    errors.supplierCodes[index]!.code!.message!,
                  )
                : null
            }
          >
            <input
              {...register(`supplierCodes.${index}.code`)}
              placeholder={t("items.supplierCodes.codePlaceholder", "PROV-001")}
              disabled={disabled}
            />
          </ZHField>
          <ZHField
            label={t("items.supplierCodes.col.presentation", "Presentación")}
          >
            <span className="zh-field-hint">
              {t(
                "items.supplierCodes.presentationPending",
                "Presentación pendiente",
              )}
            </span>
          </ZHField>
          <ZHField label={t("items.supplierCodes.primary", "Principal")}>
            <div className="items-row-actions">
              <ZHBtn
                type="button"
                variant={supplierCodes[index]?.isPrimary ? "primary" : "ghost"}
                size="sm"
                disabled={disabled || !!supplierCodes[index]?.isPrimary}
                onClick={() => markPrimary(index)}
              >
                {supplierCodes[index]?.isPrimary
                  ? t("items.supplierCodes.isPrimary", "Principal")
                  : t(
                      "items.supplierCodes.markPrimary",
                      "Marcar como principal",
                    )}
              </ZHBtn>
              <ZHBtn
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => remove(index)}
                disabled={disabled}
              >
                {t("common.remove", "Quitar")}
              </ZHBtn>
            </div>
          </ZHField>
        </ZHGrid>
      ))}

      <ZHBtn
        type="button"
        variant="secondary"
        size="sm"
        disabled={disabled}
        onClick={() => append({ code: "", isPrimary: false, supplierId: "" })}
      >
        {t("items.supplierCodes.add", "Agregar código de proveedor")}
      </ZHBtn>
    </ZHFormSection>
  );
}
