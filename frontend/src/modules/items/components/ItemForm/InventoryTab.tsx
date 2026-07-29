import { Controller, useFormContext } from "react-hook-form";
import { useNavigate } from "react-router-dom";
import {
  ZHField,
  ZHFormSection,
  ZHGrid,
  ZHToggle,
} from "../../../../components/zh/ZHForm";
import { ZhDecimalInput } from "../../../../components/zh/inputs/ZhDecimalInput";
import { getDecimalConfig } from "../../../../lib/config/decimal.config";
import { UnitConversionsSection } from "../../detail/components/CollectionSection";
import type { CreateItemFormValues } from "../../schemas/createItemSchema";
import type { ItemUnitConversionDto } from "../../../../types/items";

type Props = {
  t: (key: string, fallback?: string) => string;
  disabled: boolean;
  isEditMode: boolean;
  itemId?: string;
  unitConversions?: ItemUnitConversionDto[];
};

export function InventoryTab({
  t,
  disabled,
  isEditMode,
  itemId,
  unitConversions,
}: Props) {
  const {
    register,
    control,
    formState: { errors },
  } = useFormContext<CreateItemFormValues>();
  const navigate = useNavigate();
  const fe = (msg?: string) => (msg ? t(msg, msg) : null);
  const quantityDecimals = getDecimalConfig().quantity;

  return (
    <>
      <ZHFormSection
        title={t("items.stock.title", "Configuración de inventario")}
        description={t(
          "items.stock.sectionDesc",
          "Cómo se controla el stock de este ítem: si se rastrea cantidad, por lote/serie, y en qué unidades.",
        )}
      >
        <ZHGrid cols={1}>
          <Controller
            name="stockConfig.tracksStock"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label={t("items.stock.tracksStock", "Maneja stock")}
                description={t(
                  "items.stock.tracksStockDesc",
                  "El sistema descuenta y controla existencias de este ítem.",
                )}
                value={!!field.value}
                onChange={field.onChange}
                disabled={disabled}
              />
            )}
          />
          <Controller
            name="stockConfig.tracksLot"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label={t("items.stock.tracksLot", "Rastreo por lote")}
                description={t(
                  "items.stock.tracksLotDesc",
                  "Cada movimiento de este ítem exige indicar el lote de origen.",
                )}
                value={!!field.value}
                onChange={field.onChange}
                disabled={disabled}
              />
            )}
          />
          <Controller
            name="stockConfig.tracksSeries"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label={t("items.stock.tracksSeries", "Rastreo por serie")}
                description={t(
                  "items.stock.tracksSeriesDesc",
                  "Cada unidad de este ítem se identifica con un número de serie individual.",
                )}
                value={!!field.value}
                onChange={field.onChange}
                disabled={disabled}
              />
            )}
          />
          <Controller
            name="stockConfig.allowDecimalQty"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label={t("items.stock.allowDecimalQty", "Cantidades decimales")}
                description={t(
                  "items.stock.allowDecimalQtyDesc",
                  "Permite manejar existencias con decimales (p. ej. 1.5 kg).",
                )}
                value={!!field.value}
                onChange={field.onChange}
                disabled={disabled}
              />
            )}
          />
          <Controller
            name="stockConfig.allowDecimalSale"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label={t("items.stock.allowDecimalSale", "Venta en decimales")}
                description={t(
                  "items.stock.allowDecimalSaleDesc",
                  "Permite vender cantidades con decimales de este ítem.",
                )}
                value={!!field.value}
                onChange={field.onChange}
                disabled={disabled}
              />
            )}
          />
        </ZHGrid>
        <ZHGrid cols={2}>
          <ZHField
            label={t("items.stock.minStockQty", "Stock mínimo")}
            fieldError={fe(errors.stockConfig?.minStockQty?.message)}
          >
            <ZhDecimalInput
              decimals={quantityDecimals}
              positiveOnly
              {...register("stockConfig.minStockQty", {
                valueAsNumber: true,
                setValueAs: (v) => (v === "" ? null : Number(v)),
              })}
              disabled={disabled}
            />
          </ZHField>
          <ZHField
            label={t("items.stock.maxStockQty", "Stock máximo")}
            fieldError={fe(errors.stockConfig?.maxStockQty?.message)}
          >
            <ZhDecimalInput
              decimals={quantityDecimals}
              positiveOnly
              {...register("stockConfig.maxStockQty", {
                valueAsNumber: true,
                setValueAs: (v) => (v === "" ? null : Number(v)),
              })}
              disabled={disabled}
            />
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      <ZHFormSection
        title={t("items.conversions.title", "Conversiones de unidad")}
        description={t(
          "items.conversions.sectionDesc",
          "Equivalencias entre la unidad base y otras unidades de manejo (p. ej. caja = 12 unidades).",
        )}
      >
        {isEditMode ? (
          <UnitConversionsSection t={t} conversions={unitConversions ?? []} />
        ) : (
          <div className="empty-state">
            {t(
              "items.conversions.availableAfterCreate",
              "Guarda el ítem primero para agregar conversiones de unidad.",
            )}
          </div>
        )}
      </ZHFormSection>

      {isEditMode && itemId && (
        <ZHFormSection
          title="Trazabilidad"
          description="Historial completo de movimientos de inventario de este producto."
        >
          <button
            type="button"
            className="pf-btn pf-btn--primary"
            onClick={() => navigate(`/inventory/kardex?productId=${itemId}`)}
          >
            <span className="material-symbols-outlined pf-btn__icon">
              history
            </span>
            Ver Kardex de este producto
          </button>
        </ZHFormSection>
      )}
    </>
  );
}
