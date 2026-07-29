import { useFormContext } from "react-hook-form";
import {
  ZHField,
  ZHFormSection,
  ZHGrid,
} from "../../../../components/zh/ZHForm";
import type { CreateItemFormValues } from "../../schemas/createItemSchema";

type Props = {
  t: (key: string, fallback?: string) => string;
  disabled: boolean;
  vatRateOptions: { code: string; name: string; percentage: number }[];
  iceRateOptions: { code: string; name: string; percentage: number }[];
};

export function TaxConfigTab({
  t,
  disabled,
  vatRateOptions,
  iceRateOptions,
}: Props) {
  const {
    register,
    formState: { errors },
  } = useFormContext<CreateItemFormValues>();
  const fe = (msg?: string) => (msg ? t(msg, msg) : null);

  return (
    <>
      <ZHFormSection
        title={t("items.tax.saleVat", "IVA en Venta")}
        description={t(
          "items.tax.saleVatDesc",
          'Tarifa que se aplica cuando este ítem se vende. "No aplica" es una elección válida, no un campo vacío por completar después.',
        )}
      >
        <ZHGrid cols={2}>
          <ZHField
            label={t("items.tax.saleVatCode", "Tarifa IVA venta")}
            fieldError={fe(errors.taxConfig?.saleVatCode?.message)}
          >
            <select {...register("taxConfig.saleVatCode")} disabled={disabled}>
              <option value="">
                {t("items.tax.notApplicable", "— No aplica —")}
              </option>
              {vatRateOptions.map((v) => (
                <option key={v.code} value={v.code}>
                  {v.code} — {v.name} ({v.percentage}%)
                </option>
              ))}
            </select>
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      <ZHFormSection
        title={t("items.tax.purchaseVat", "IVA en Compra")}
        description={t(
          "items.tax.purchaseVatDesc",
          "Tarifa que se aplica cuando este ítem se compra a un proveedor. Independiente de la tarifa de venta.",
        )}
      >
        <ZHGrid cols={2}>
          <ZHField
            label={t("items.tax.purchaseVatCode", "Tarifa IVA compra")}
            fieldError={fe(errors.taxConfig?.purchaseVatCode?.message)}
          >
            <select
              {...register("taxConfig.purchaseVatCode")}
              disabled={disabled}
            >
              <option value="">
                {t("items.tax.notApplicable", "— No aplica —")}
              </option>
              {vatRateOptions.map((v) => (
                <option key={v.code} value={v.code}>
                  {v.code} — {v.name} ({v.percentage}%)
                </option>
              ))}
            </select>
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      <ZHFormSection
        title={t("items.tax.excise", "Impuesto ICE")}
        description={t(
          "items.tax.exciseDesc",
          "Impuesto a los Consumos Especiales, solo aplica a ciertos bienes/servicios definidos por el SRI. La mayoría de ítems no lo requiere.",
        )}
      >
        <ZHGrid cols={2}>
          <ZHField
            label={t("items.tax.exciseTaxCode", "Código ICE")}
            fieldError={fe(errors.taxConfig?.exciseTaxCode?.message)}
          >
            <select
              {...register("taxConfig.exciseTaxCode")}
              disabled={disabled}
            >
              <option value="">
                {t("items.tax.notApplicable", "— No aplica —")}
              </option>
              {iceRateOptions.map((v) => (
                <option key={v.code} value={v.code}>
                  {v.code} — {v.name} ({v.percentage}%)
                </option>
              ))}
            </select>
          </ZHField>
        </ZHGrid>
      </ZHFormSection>
    </>
  );
}
