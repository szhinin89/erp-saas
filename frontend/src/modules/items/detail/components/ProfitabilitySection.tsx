import { useCallback, useEffect, useState } from "react";
import { ZhDecimalInput } from "../../../../components/zh/inputs/ZhDecimalInput";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { apiGet } from "../../../lib/apiEnvelope";
import { useI18n } from "../../../../i18n/i18n";
import { formatMoney } from "../../../../lib/sanitizers";
import { getDecimalConfig } from "../../../../lib/config/decimal.config";
import { companyProfileLookupFacade } from "../../../configuracion/empresa/facades/companyProfileLookupFacade";
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

// Tonos soportados por las clases `items-metric-card__value--*` (ver items-catalog.css).
// El código/label/existencia de cada estado de margen viene del catálogo
// `item-margin-statuses` (backend); aquí solo se valida que el colorToken recibido
// coincida con un tono conocido, cayendo a "neutral" si no.
const VALID_TONES = new Set(["success", "warning", "error", "neutral"]);

function toneClass(colorToken: string | undefined): string {
  return colorToken && VALID_TONES.has(colorToken) ? colorToken : "neutral";
}

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
    companyProfileLookupFacade
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

  const statusTone = (s: MarginStatusCode) =>
    toneClass(marginStatuses.find((m) => m.code === s)?.colorToken);

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
          valueTone={statusTone(data.marginStatus)}
        />
        <MetricCard
          label={t("items.profitability.status", "Estado")}
          value={statusLabel(data.marginStatus)}
          valueTone={statusTone(data.marginStatus)}
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
          <ZHBtn
            variant="primary"
            size="sm"
            onClick={simulate}
            disabled={disabled || simLoading || !newPvp}
          >
            {simLoading
              ? t("items.profitability.simulating", "Calculando...")
              : t("items.profitability.simulate", "Simular")}
          </ZHBtn>
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
              valueTone={statusTone(sim.simulatedMarginStatus)}
              small
            />
            <MetricCard
              label={t("items.profitability.difference", "Diferencia")}
              value={`${sim.marginDifference >= 0 ? "+" : ""}${currencyCode} ${formatMoney(sim.marginDifference, dc.totalAmount)}`}
              valueTone={sim.marginDifference >= 0 ? "success" : "error"}
              small
            />
            <MetricCard
              label={t("items.profitability.status", "Estado")}
              value={statusLabel(sim.simulatedMarginStatus)}
              valueTone={statusTone(sim.simulatedMarginStatus)}
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
  valueTone,
  small,
}: {
  label: string;
  value: string;
  sublabel?: string;
  valueTone?: string;
  small?: boolean;
}) {
  return (
    <div
      className={`items-metric-card${small ? " items-metric-card--small" : ""}`}
    >
      <div className="items-metric-card__label">{label}</div>
      <div
        className={`items-metric-card__value${valueTone ? ` items-metric-card__value--${valueTone}` : ""}`}
      >
        {value}
      </div>
      {sublabel && (
        <div className="items-metric-card__sublabel">{sublabel}</div>
      )}
    </div>
  );
}
