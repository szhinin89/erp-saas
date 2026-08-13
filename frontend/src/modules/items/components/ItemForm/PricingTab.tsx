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
  return value == null
    ? "—"
    : `${currencyCode} ${formatMoney(value, decimals)}`;
}

function formatOptionalPercent(value: number | null, decimals: number) {
  return value == null ? "—" : `${formatMoney(value * 100, decimals)}%`;
}

function formatVatRate(
  rate: { name: string; percentage: number } | null,
  decimals: number,
) {
  if (!rate) return "—";
  return `${rate.name} (${formatMoney(rate.percentage, decimals)}%)`;
}

function calculateNetPrice(
  inputPriceValue: number | null,
  inputPriceMode: PriceInputMode,
  vatDecimal: number | null,
) {
  if (inputPriceValue == null) return null;
  if (inputPriceMode === "net") return inputPriceValue;
  if (vatDecimal == null) return null;
  if (vatDecimal === 0) return inputPriceValue;
  return inputPriceValue / (1 + vatDecimal);
}

function calculateGrossPrice(
  inputPriceValue: number | null,
  inputPriceMode: PriceInputMode,
  vatDecimal: number | null,
) {
  if (inputPriceValue == null) return null;
  if (inputPriceMode === "gross") return inputPriceValue;
  if (vatDecimal == null) return null;
  return inputPriceValue * (1 + vatDecimal);
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
      <strong
        className={`items-metric-card__value items-metric-card__value--${tone}`}
      >
        {" "}
        {value}
      </strong>
    </div>
  );
}

export function PricingTab({ t, disabled, itemId, vatRateOptions }: Props) {
  const {
    watch,
    setValue,
    formState: { errors },
  } = useFormContext<CreateItemFormValues>();
  const fe = (msg?: string) => (msg ? t(msg, msg) : null);
  const dc = getDecimalConfig();
  const [inputPriceMode, setInputPriceMode] =
    useState<PriceInputMode>("net");
  const [inputPriceValue, setInputPriceValue] = useState("");
  const syncedBasePrice = useRef<number | null>(null);
  const skipNextDerivedPersist = useRef(true);

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

  const parsedInputPriceValue = toNullableNumber(inputPriceValue);
  const computedNetPrice = calculateNetPrice(
    parsedInputPriceValue,
    inputPriceMode,
    vatDecimal,
  );
  const computedGrossPrice = calculateGrossPrice(
    parsedInputPriceValue,
    inputPriceMode,
    vatDecimal,
  );
  const computedTaxAmount =
    computedNetPrice != null && computedGrossPrice != null
      ? computedGrossPrice - computedNetPrice
      : null;
  const utilidad =
    hasCost && computedNetPrice != null ? computedNetPrice - averageCost : null;
  const margen =
    utilidad != null && computedNetPrice != null && computedNetPrice > 0
      ? utilidad / computedNetPrice
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

  const valueFromBasePrice = (persistedBasePrice: number | null) => {
    if (persistedBasePrice == null) return "";
    return formatInputValue(persistedBasePrice, dc.salesUnitPrice);
  };

  useEffect(() => {
    if (syncedBasePrice.current === basePrice) return;
    syncedBasePrice.current = basePrice;
    setInputPriceMode("net");
    setInputPriceValue(valueFromBasePrice(basePrice));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [basePrice]);

  const persistNetPrice = (netPrice: number | null) => {
    const next = netPrice == null ? null : roundTo(netPrice, dc.salesUnitPrice);
    syncedBasePrice.current = next;
    setValue("baseSalePrice", next, {
      shouldDirty: true,
      shouldValidate: true,
    });
  };

  useEffect(() => {
    if (skipNextDerivedPersist.current) {
      skipNextDerivedPersist.current = false;
      return;
    }
    persistNetPrice(computedNetPrice);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [inputPriceMode, vatDecimal]);

  const handlePriceChange = (nextValue: string) => {
    setInputPriceValue(nextValue);
    const parsed = toNullableNumber(nextValue);
    persistNetPrice(calculateNetPrice(parsed, inputPriceMode, vatDecimal));
  };

  const handleModeChange = (nextMode: PriceInputMode) => {
    setInputPriceMode(nextMode);
  };

  return (
    <ZHFormSection
      title={t("items.pricing.title", "Precio de venta, costo y rentabilidad")}
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
            <span className="items-currency-input__prefix">{currencyCode}</span>
            <ZhDecimalInput
              decimals={dc.salesUnitPrice}
              positiveOnly
              placeholder={t("items.pricing.pvpPlaceholder", "0.00")}
              value={inputPriceValue}
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
            value={inputPriceMode}
            onChange={(event) =>
              handleModeChange(event.target.value as PriceInputMode)
            }
            disabled={disabled}
          >
            <option value="net">{t("items.pricing.net", "Sin IVA")}</option>
            <option value="gross">{t("items.pricing.gross", "Con IVA")}</option>
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
          label={t("items.pricing.saleVatRate", "Tarifa IVA venta")}
          value={formatVatRate(selectedVatRate, dc.percentage)}
        />
        <Metric
          label={t("items.pricing.netToSave", "Precio sin IVA que se guardará")}
          value={formatOptionalMoney(
            computedNetPrice,
            currencyCode,
            dc.salesUnitPrice,
          )}
        />
        <Metric
          label={t("items.pricing.vatCalculated", "IVA calculado")}
          value={formatOptionalMoney(
            computedTaxAmount,
            currencyCode,
            dc.salesUnitPrice,
          )}
        />
        <Metric
          label={t("items.pricing.grossFinal", "Precio final con IVA")}
          value={formatOptionalMoney(
            computedGrossPrice,
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
          value={formatOptionalMoney(utilidad, currencyCode, dc.salesUnitPrice)}
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
      </div>
    </ZHFormSection>
  );
}
