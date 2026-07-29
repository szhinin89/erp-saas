import { useCallback, useEffect, useState } from "react";
import { ZhDecimalInput } from "../../../../components/zh/inputs/ZhDecimalInput";
import { apiGet } from "../../../lib/apiEnvelope";
import { useI18n } from "../../../../i18n/i18n";
import { formatMoney } from "../../../../lib/sanitizers";
import { getDecimalConfig } from "../../../../lib/config/decimal.config";
import { companyProfileService } from "../../../configuracion/empresa/api/companyProfileService";
import { itemService } from "../../api/itemService";
import type {
  ItemProfitabilityDto,
  PriceSimulationDto,
  MarginStatusCode,
} from "../../api/itemService";

interface Props {
  itemId: string;
  disabled?: boolean;
}

// Mapeo puramente presentacional token→variable CSS. El código/label/existencia de cada
// estado de margen viene del catálogo `item-margin-statuses` (backend), no se hardcodea aquí.
const COLOR_TOKEN_VAR: Record<string, string> = {
  success: "var(--color-success)",
  warning: "var(--color-warning)",
  error: "var(--color-error)",
  neutral: "var(--color-text-secondary)",
};

type MarginStatusOption = {
  code: MarginStatusCode;
  label: string;
  colorToken: string;
};

export function ProfitabilitySection({ itemId, disabled = false }: Props) {
  const { t } = useI18n();
  const [data, setData] = useState<ItemProfitabilityDto | null>(null);
  const [sim, setSim] = useState<PriceSimulationDto | null>(null);
  const [newPvp, setNewPvp] = useState("");
  const [loading, setLoading] = useState(false);
  const [simLoading, setSimLoading] = useState(false);
  const [marginStatuses, setMarginStatuses] = useState<MarginStatusOption[]>(
    [],
  );
  const [companyCurrency, setCompanyCurrency] = useState<string | null>(null);
  const dc = getDecimalConfig();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await itemService.getProfitability(itemId));
    } catch {
      /* */
    }
    setLoading(false);
  }, [itemId]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    apiGet<MarginStatusOption[]>("/api/v1/catalog/item-margin-statuses")
      .then(setMarginStatuses)
      .catch(() => {});
    companyProfileService
      .getProfile()
      .then((p) => setCompanyCurrency(p?.currencyCode ?? null))
      .catch(() => {});
  }, []);

  // Moneda real de la lista de precios usada para el PVP; si el ítem aún no tiene
  // precio en ninguna lista (currencyCode null), se usa la del perfil de empresa.
  const currencyCode =
    data?.currencyCode ?? sim?.currencyCode ?? companyCurrency ?? "";

  const simulate = async () => {
    const pvp = parseFloat(newPvp);
    if (isNaN(pvp) || pvp < 0) return;
    setSimLoading(true);
    try {
      setSim(await itemService.simulatePrice(itemId, pvp));
    } catch {
      /* */
    }
    setSimLoading(false);
  };

  const statusColor = (s: MarginStatusCode) =>
    COLOR_TOKEN_VAR[
      marginStatuses.find((m) => m.code === s)?.colorToken ?? ""
    ] ?? "var(--color-text-secondary)";

  const statusLabel = (s: MarginStatusCode) =>
    marginStatuses.find((m) => m.code === s)?.label ?? s;

  if (loading)
    return (
      <div className="items-profitability__loading">
        {t("items.profitability.loading", "Cargando rentabilidad...")}
      </div>
    );
  if (!data) return null;

  return (
    <div className="items-profitability">
      {/* ── Indicadores principales ── */}
      <div className="items-profitability__grid">
        <MetricCard
          label={t("items.profitability.averageCost", "Costo Promedio")}
          value={`${currencyCode} ${formatMoney(data.averageCost, dc.purchaseUnitPrice)}`}
          sublabel={`${t("items.profitability.stockLabel", "Stock")}: ${formatMoney(data.totalStockQuantity, dc.quantity)} ${t("items.profitability.units", "uds")}`}
        />
        <MetricCard
          label={t("items.profitability.currentPvp", "PVP Actual")}
          value={
            data.currentSalePrice > 0
              ? `${currencyCode} ${formatMoney(data.currentSalePrice, dc.salesUnitPrice)}`
              : "—"
          }
          sublabel={
            data.priceListName ?? t("items.profitability.noList", "Sin lista")
          }
        />
        <MetricCard
          label={t("items.profitability.grossMargin", "Margen Bruto")}
          value={`${currencyCode} ${formatMoney(data.marginAmount, dc.totalAmount)}`}
          sublabel={`${formatMoney(data.marginPercent, dc.percentage)}%`}
          valueColor={statusColor(data.marginStatus)}
        />
        <MetricCard
          label={t("items.profitability.status", "Estado")}
          value={statusLabel(data.marginStatus)}
          valueColor={statusColor(data.marginStatus)}
        />
      </div>

      {/* ── Simulador de PVP ── */}
      <div className="items-profitability__simulator">
        <div className="items-profitability__simulator-title">
          {t("items.profitability.simulatorTitle", "Simulador de Precio")}
        </div>
        <div className="items-profitability__simulator-row">
          <div>
            <label className="items-profitability__simulator-label">{`${t("items.profitability.newPvpLabel", "Nuevo PVP")} (${currencyCode})`}</label>
            <ZhDecimalInput
              decimals={dc.salesUnitPrice}
              positiveOnly
              value={newPvp}
              onChange={(e) => {
                setNewPvp(e.target.value);
                setSim(null);
              }}
              placeholder={
                data.currentSalePrice > 0
                  ? formatMoney(data.currentSalePrice, dc.salesUnitPrice)
                  : formatMoney(0, dc.salesUnitPrice)
              }
              className="items-profitability__simulator-input"
              disabled={disabled}
            />
          </div>
          <button
            onClick={simulate}
            disabled={disabled || simLoading || !newPvp}
            className="items-profitability__simulate-btn"
          >
            {simLoading
              ? t("items.profitability.simulating", "Calculando...")
              : t("items.profitability.simulate", "Simular")}
          </button>
        </div>

        {sim && (
          <div className="items-profitability__sim-grid">
            <MetricCard
              label={t("items.profitability.currentMargin", "Margen Actual")}
              value={`${currencyCode} ${formatMoney(sim.currentMarginAmount, dc.totalAmount)}`}
              sublabel={`${formatMoney(sim.currentMarginPercent, dc.percentage)}%`}
              small
            />
            <MetricCard
              label={t(
                "items.profitability.simulatedMargin",
                "Margen Simulado",
              )}
              value={`${currencyCode} ${formatMoney(sim.simulatedMarginAmount, dc.totalAmount)}`}
              sublabel={`${formatMoney(sim.simulatedMarginPercent, dc.percentage)}%`}
              valueColor={statusColor(sim.simulatedMarginStatus)}
              small
            />
            <MetricCard
              label={t("items.profitability.difference", "Diferencia")}
              value={`${sim.marginDifference >= 0 ? "+" : ""}${currencyCode} ${formatMoney(sim.marginDifference, dc.totalAmount)}`}
              valueColor={
                sim.marginDifference >= 0
                  ? "var(--color-success)"
                  : "var(--color-error)"
              }
              small
            />
            <MetricCard
              label={t("items.profitability.status", "Estado")}
              value={statusLabel(sim.simulatedMarginStatus)}
              valueColor={statusColor(sim.simulatedMarginStatus)}
              small
            />
          </div>
        )}
      </div>
    </div>
  );
}

function MetricCard({
  label,
  value,
  sublabel,
  valueColor,
  small,
}: {
  label: string;
  value: string;
  sublabel?: string;
  valueColor?: string;
  small?: boolean;
}) {
  return (
    <div
      className={`items-metric-card${small ? " items-metric-card--small" : ""}`}
    >
      <div className="items-metric-card__label">{label}</div>
      <div
        className="items-metric-card__value"
        style={valueColor ? { color: valueColor } : undefined}
      >
        {value}
      </div>
      {sublabel && (
        <div className="items-metric-card__sublabel">{sublabel}</div>
      )}
    </div>
  );
}
