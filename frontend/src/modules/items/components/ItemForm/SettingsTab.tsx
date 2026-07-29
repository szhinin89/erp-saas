import { Controller, useFormContext } from "react-hook-form";
import {
  ZHField,
  ZHFormSection,
  ZHGrid,
  ZHToggle,
} from "../../../../components/zh/ZHForm";
import type { CreateItemFormValues } from "../../schemas/createItemSchema";

type Props = {
  t: (key: string, fallback?: string) => string;
  disabled: boolean;
};

export function SettingsTab({ t, disabled }: Props) {
  const { register, control } = useFormContext<CreateItemFormValues>();

  return (
    <>
      <ZHFormSection
        title={t("items.sale.title", "Configuración de venta")}
        description={t(
          "items.sale.sectionDesc",
          "En qué canales está disponible este ítem para la venta.",
        )}
      >
        <ZHGrid cols={1}>
          <Controller
            name="saleConfig.isForSale"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label={t("items.sale.isForSale", "Disponible para venta")}
                description={t(
                  "items.sale.isForSaleDesc",
                  "Habilita este ítem para ser incluido en documentos de venta.",
                )}
                value={!!field.value}
                onChange={field.onChange}
                disabled={disabled}
              />
            )}
          />
          <Controller
            name="saleConfig.isAvailableOnPOS"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label={t("items.sale.isAvailableOnPOS", "Disponible en POS")}
                description={t(
                  "items.sale.isAvailableOnPOSDesc",
                  "Aparece en el punto de venta físico.",
                )}
                value={!!field.value}
                onChange={field.onChange}
                disabled={disabled}
              />
            )}
          />
          <Controller
            name="saleConfig.isAvailableOnWeb"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label={t("items.sale.isAvailableOnWeb", "Disponible en web")}
                description={t(
                  "items.sale.isAvailableOnWebDesc",
                  "Aparece en la tienda web, si el módulo eCommerce está activo.",
                )}
                value={!!field.value}
                onChange={field.onChange}
                disabled={disabled}
              />
            )}
          />
          <Controller
            name="saleConfig.isAvailableOnMobile"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label={t(
                  "items.sale.isAvailableOnMobile",
                  "Disponible en móvil",
                )}
                description={t(
                  "items.sale.isAvailableOnMobileDesc",
                  "Aparece en la app móvil de ventas.",
                )}
                value={!!field.value}
                onChange={field.onChange}
                disabled={disabled}
              />
            )}
          />
          <Controller
            name="saleConfig.isEcommerceActive"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label={t("items.sale.isEcommerceActive", "eCommerce activo")}
                description={t(
                  "items.sale.isEcommerceActiveDesc",
                  "Activa la publicación de este ítem en canales eCommerce.",
                )}
                value={!!field.value}
                onChange={field.onChange}
                disabled={disabled}
              />
            )}
          />
        </ZHGrid>
      </ZHFormSection>

      <ZHFormSection
        title={t("items.section.observations", "Observaciones")}
        description={t(
          "items.section.observationsDesc",
          "Notas internas del ítem, no visibles para el cliente.",
        )}
      >
        <ZHField label={t("items.form.observations", "Notas adicionales")}>
          <textarea
            {...register("observations")}
            rows={3}
            disabled={disabled}
          />
        </ZHField>
      </ZHFormSection>
    </>
  );
}
