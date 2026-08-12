import { useEffect, useRef, useState } from "react";
import { useFormContext } from "react-hook-form";
import {
  ZHField,
  ZHFormAlert,
  ZHFormSection,
  ZHGrid,
} from "../../../../components/zh/ZHForm";
import { ZhDecimalInput, ZhSelect } from "../../../../components/zh/inputs";
import { getDecimalConfig } from "../../../../lib/config/decimal.config";
import { formatMoney } from "../../../../lib/sanitizers";
import { useAsync } from "../../../../hooks/useAsync";
import { priceListLookupFacade } from "../../../pricing/facades/priceListLookupFacade";
import { companyProfileLookupFacade } from "../../../configuracion/empresa/facades/companyProfileLookupFacade";
import { itemService } from "../../api/itemService";
import type { CreateItemFormValues } from "../../schemas/createItemSchema";

type Props = {
  t: (key: string, fallback?: string) => string;
  disabled: boolean;
  itemId?: string;
  vatRateOptions: { code: string; name: string; percentage: number }[];
};

type PriceInputMode = "net" | "gross";

function toNullableNumber(value: unknown): number | null {
  if (value === "" || value == null) return null;
  const numberValue = Number(value);
  return Number.isFinite(numberValue) ? numberValue : null;
}

function roundTo(value: number, decimals: number) {
  const factor = 10 ** decimals;
  return Math.round(value * factor) / factor;
}

function formatInputValue(value: number | null, decimals: number) {
  if (value == null || !Number.isFinite(value)) return "";
  return String(roundTo(value, decimals));
}

function formatOptionalMoney(
  value: number | null,
  currencyCode: string,
  decimals: number,
) {
  return value == null ? "—" : `${currencyCode} ${formatMoney(value, decimals)}`;
}

function formatOptionalPercent(value: number | null, decimals: number) {
  return value == null ? "—" : `${formatMoney(value * 100, decimals)}%`;
}

function Metric({
  label,
  value,
  tone = "neutral",
}: {
  label: string;
  value: string;
  tone?: "success" | "warning" | "error" | "neutral";
}) {
  return (
    <div className="items-metric-card items-metric-card--small">
      <span className="items-metric-card__label">{label}</span>
      <strong className={`items-metric-card__value items-metric-card__value--${tone}`}>
        {value}
      </strong>
    </div>
  );
}

export function PricingTab({
  t,
  disabled,
  itemId,
  vatRateOptions,
}: Props) {
  const {
    watch,
    setValue,
    formState: { errors },
  } = useFormContext<CreateItemFormValues>();
  const fe = (msg?: string) => (msg ? t(msg, msg) : null);
  const dc = getDecimalConfig();
  const [priceMode, setPriceMode] = useState<PriceInputMode>("net");
  const [enteredPrice, setEnteredPrice] = useState("");
  const syncedBasePrice = useRef<number | null>(null);

  // Moneda real de la lista de precios predeterminada; si aún no existe una lista
  // (ítem nuevo), se usa la moneda configurada en el perfil de la empresa
  // (Company.CurrencyCode) — nunca un código fijo en el código del formulario.
  const priceListsState = useAsync(() =>
    priceListLookupFacade.list(true).catch(() => []),
  );
  const defaultList = (priceListsState.data ?? []).find((pl) => pl.isDefault);

  const companyProfileState = useAsync(() =>
    companyProfileLookupFacade.getProfile().catch(() => null),
  );
  const currencyCode =
    defaultList?.currencyCode ?? companyProfileState.data?.currencyCode ?? "";

  const basePrice = toNullableNumber(watch("baseSalePrice"));
  const saleVatCode = watch("taxConfig.saleVatCode");
  const maxDiscount = watch("saleConfig.maxDiscountPercent");
  const selectedVatRate =
    vatRateOptions.find((rate) => rate.code === saleVatCode) ?? null;
  const vatPercent = selectedVatRate?.percentage ?? null;
  const vatDecimal = vatPercent != null ? vatPercent / 100 : null;

  const profitabilityState = useAsync(
    () =>
      itemId
        ? itemService.getProfitability(itemId).catch(() => null)
        : Promise.resolve(null),
    !!itemId,
    [itemId],
  );
  const averageCost = profitabilityState.data?.averageCost ?? null;
  const hasCost = averageCost != null && averageCost > 0;
  const lastCost: number | null = null;

  const parsedEnteredPrice = toNullableNumber(enteredPrice);
  const priceSinIva =
    parsedEnteredPrice == null
      ? null
      : priceMode === "net"
        ? parsedEnteredPrice
        : vatDecimal == null
          ? null
          : parsedEnteredPrice / (1 + vatDecimal);
  const priceConIva =
    parsedEnteredPrice == null
      ? null
      : priceMode === "gross"
        ? parsedEnteredPrice
        : vatDecimal == null
          ? null
          : parsedEnteredPrice * (1 + vatDecimal);
  const ivaValor =
    priceSinIva != null && priceConIva != null
      ? priceConIva - priceSinIva
      : null;
  const utilidad =
    hasCost && priceSinIva != null ? priceSinIva - averageCost : null;
  const margen =
    utilidad != null && priceSinIva != null && priceSinIva > 0
      ? utilidad / priceSinIva
      : null;
  const markup =
    utilidad != null && averageCost != null && averageCost > 0
      ? utilidad / averageCost
      : null;
  const isLoss = utilidad != null && utilidad < 0;
  const lowMargin = margen != null && margen >= 0 && margen < 0.1;
  const status = !hasCost
    ? {
        label: t("items.pricing.status.noCost", "Sin costo disponible"),
        tone: "neutral" as const,
      }
    : isLoss
      ? {
          label: t("items.pricing.status.loss", "Pérdida"),
          tone: "error" as const,
        }
      : lowMargin
        ? {
            label: t("items.pricing.status.lowMargin", "Margen bajo"),
            tone: "warning" as const,
          }
        : {
            label: t("items.pricing.status.healthy", "Saludable"),
            tone: "success" as const,
          };

  const valueFromBasePrice = (
    persistedBasePrice: number | null,
    mode: PriceInputMode,
  ) => {
    if (persistedBasePrice == null) return "";
    if (mode === "gross" && vatDecimal != null) {
      return formatInputValue(
        persistedBasePrice * (1 + vatDecimal),
        dc.salesUnitPrice,
      );
    }
    return formatInputValue(persistedBasePrice, dc.salesUnitPrice);
  };

  useEffect(() => {
    if (syncedBasePrice.current === basePrice) return;
    syncedBasePrice.current = basePrice;
    setEnteredPrice(valueFromBasePrice(basePrice, priceMode));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [basePrice, priceMode]);

  useEffect(() => {
    if (priceMode !== "gross") return;
    setEnteredPrice(valueFromBasePrice(basePrice, "gross"));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [vatDecimal]);

  const persistNetPrice = (netPrice: number | null) => {
    const next = netPrice == null ? null : roundTo(netPrice, dc.salesUnitPrice);
    syncedBasePrice.current = next;
    setValue("baseSalePrice", next, {
      shouldDirty: true,
      shouldValidate: true,
    });
  };

  const handlePriceChange = (nextValue: string) => {
    setEnteredPrice(nextValue);
    const parsed = toNullableNumber(nextValue);
    if (parsed == null) {
      persistNetPrice(null);
      return;
    }
    if (priceMode === "net") {
      persistNetPrice(parsed);
      return;
    }
    persistNetPrice(vatDecimal == null ? null : parsed / (1 + vatDecimal));
  };

  const handleModeChange = (nextMode: PriceInputMode) => {
    setPriceMode(nextMode);
    setEnteredPrice(valueFromBasePrice(basePrice, nextMode));
  };

  return (
    <ZHFormSection
      title={t(
        "items.pricing.title",
        "Precio de venta, costo y rentabilidad",
      )}
      description={t(
        "items.pricing.sectionDesc",
        "El sistema guarda siempre el precio sin IVA. Si ingresa un precio con IVA, se calculará automáticamente el precio sin IVA.",
      )}
    >
        <ZHGrid cols={2}>
          <ZHField
            label={t("items.pricing.enteredPrice", "Precio ingresado")}
            fieldError={fe(errors.baseSalePrice?.message)}
          >
            <div className="items-currency-input">
              <span className="items-currency-input__prefix">
                {currencyCode}
              </span>
              <ZhDecimalInput
                decimals={dc.salesUnitPrice}
                positiveOnly
                placeholder={t("items.pricing.pvpPlaceholder", "0.00")}
                value={enteredPrice}
                onChange={(event) => handlePriceChange(event.target.value)}
                disabled={disabled}
                className="items-currency-input__field"
              />
            </div>
          </ZHField>

          <ZHField
            label={t("items.pricing.enteredPriceIs", "El precio ingresado es")}
          >
            <ZhSelect
              value={priceMode}
              onChange={(event) =>
                handleModeChange(event.target.value as PriceInputMode)
              }
              disabled={disabled}
            >
              <option value="net">{t("items.pricing.net", "Sin IVA")}</option>
              <option value="gross">
                {t("items.pricing.gross", "Con IVA")}
              </option>
            </ZhSelect>
          </ZHField>
        </ZHGrid>

        {!selectedVatRate && (
          <ZHFormAlert
            type="attention"
            message={t(
              "items.pricing.needsVat",
              "Seleccione el IVA de venta para calcular el precio final con IVA.",
            )}
          />
        )}
        {!hasCost && (
          <ZHFormAlert
            type="neutral"
            message={t(
              "items.pricing.noCost",
              "No hay costo disponible. El margen se calculará cuando exista una compra confirmada.",
            )}
          />
        )}
        {isLoss && (
          <ZHFormAlert
            type="error"
            message={t(
              "items.pricing.loss",
              "El precio está por debajo del costo. Esta venta generaría pérdida.",
            )}
          />
        )}
        {!isLoss && lowMargin && (
          <ZHFormAlert
            type="warning"
            message={t(
              "items.pricing.lowMargin",
              "Margen bajo. Revise si el precio es correcto.",
            )}
          />
        )}

        <div className="items-metric-grid">
          <Metric
            label={t(
              "items.pricing.netToSave",
              "Precio sin IVA que se guardará",
            )}
            value={formatOptionalMoney(
              priceSinIva,
              currencyCode,
              dc.salesUnitPrice,
            )}
          />
          <Metric
            label={t("items.pricing.vatCalculated", "IVA calculado")}
            value={formatOptionalMoney(ivaValor, currencyCode, dc.salesUnitPrice)}
          />
          <Metric
            label={t(
              "items.pricing.grossFinal",
              "Precio final con IVA",
            )}
            value={formatOptionalMoney(
              priceConIva,
              currencyCode,
              dc.salesUnitPrice,
            )}
          />
          <Metric
            label={t("items.pricing.currentBaseCost", "Costo base actual")}
            value={formatOptionalMoney(
              hasCost ? averageCost : null,
              currencyCode,
              dc.purchaseUnitPrice,
            )}
          />
          <Metric
            label={t("items.pricing.lastCost", "Último costo")}
            value={formatOptionalMoney(
              lastCost,
              currencyCode,
              dc.purchaseUnitPrice,
            )}
          />
          <Metric
            label={t("items.pricing.averageCost", "Costo promedio")}
            value={formatOptionalMoney(
              hasCost ? averageCost : null,
              currencyCode,
              dc.purchaseUnitPrice,
            )}
          />
          <Metric
            label={t(
              "items.pricing.estimatedProfit",
              "Utilidad estimada por unidad",
            )}
            value={formatOptionalMoney(
              utilidad,
              currencyCode,
              dc.salesUnitPrice,
            )}
            tone={isLoss ? "error" : lowMargin ? "warning" : "neutral"}
          />
          <Metric
            label={t("items.pricing.salesMargin", "Margen sobre venta")}
            value={formatOptionalPercent(margen, dc.percentage)}
            tone={isLoss ? "error" : lowMargin ? "warning" : "neutral"}
          />
          <Metric
            label={t("items.pricing.costMarkup", "Markup sobre costo")}
            value={formatOptionalPercent(markup, dc.percentage)}
            tone={isLoss ? "error" : lowMargin ? "warning" : "neutral"}
          />
          <Metric
            label={t("items.pricing.status", "Estado")}
            value={status.label}
            tone={status.tone}
          />
          {maxDiscount != null && (
            <Metric
              label={t(
                "items.pricing.maxDiscount",
                "Descuento máximo permitido (%)",
              )}
              value={`${formatMoney(maxDiscount, dc.percentage)}%`}
            />
          )}
        </div>
    </ZHFormSection>
  );
}
