import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import type { useItemCreationCatalogs } from "../../../components/items/ItemEditorModal/useItemCreationCatalogs";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { useI18n } from "../../../i18n/i18n";
import { formatMoney } from "../../../lib/sanitizers";
import type { PendingProductsFormValues } from "../schemas/pendingProductsSchema";
import {
  netUnitCost,
  rowSalePrice,
  type PendingProductSource,
} from "../utils/pendingProductsResolution";

/** Catálogos de creación de Items compartidos por el modal, la barra masiva y cada fila. */
export type PendingProductsCatalogs = ReturnType<typeof useItemCreationCatalogs>;

/** Datos del XML del proveedor — solo lectura: no son decisiones del Item. */
export function XmlFacts({
  source,
  factor,
  catalogs,
  purchaseVatError,
}: {
  source: PendingProductSource;
  factor: number | null;
  catalogs: PendingProductsCatalogs;
  purchaseVatError: string | undefined;
}) {
  const { t } = useI18n();
  const quantityDecimals = usePrecisionDecimals("quantity");
  const percentageDecimals = usePrecisionDecimals("percentage");
  const vat = catalogs.vatRateByCode.get(source.vatCode);
  const ice = source.iceCode
    ? catalogs.iceRateOptions.find((r) => r.code === source.iceCode)
    : undefined;
  return (
    <dl className="zh-description-grid">
      <dt>{t("purchases.pendingProducts.xml.quantityCost", "Cantidad × costo")}</dt>
      <dd>
        {formatMoney(source.quantity, quantityDecimals)} ×{" "}
        <ZHMoneyValue value={source.unitPrice} precision="purchaseUnitPrice" currencySymbol="" />
        {source.discountPct > 0 && (
          <span className="prl-muted">
            {" "}
            (−{formatMoney(source.discountPct, percentageDecimals)}%)
          </span>
        )}
      </dd>
      <dt>{t("purchases.pendingProducts.xml.baseCost", "Costo neto por unidad base")}</dt>
      <dd>
        {factor && factor > 0 ? (
          <ZHMoneyValue value={netUnitCost(source, factor)} precision="purchaseUnitPrice" currencySymbol="" />
        ) : (
          "—"
        )}
      </dd>
      <dt>{t("purchases.pendingProducts.field.purchaseVat", "IVA compra")}</dt>
      <dd>
        {vat?.name ?? (source.vatCode || "—")}
        {purchaseVatError && <div className="prl-row__error">{purchaseVatError}</div>}
      </dd>
      {source.iceCode && (
        <>
          <dt>ICE</dt>
          <dd>{ice?.name ?? source.iceCode}</dd>
        </>
      )}
    </dl>
  );
}

/** Desglose del PVP final desde el precio sin IVA realmente persistible. */
export function SalePriceSummary({
  row,
  breakdown,
  priceDecimals,
}: {
  row: PendingProductsFormValues["rows"][number];
  breakdown: ReturnType<typeof rowSalePrice>;
  priceDecimals: number;
}) {
  const { t } = useI18n();
  const percentageDecimals = usePrecisionDecimals("percentage");
  if (!breakdown) return null;
  const percent = (value: number | null) =>
    value == null ? "—" : `${formatMoney(value, percentageDecimals)}%`;
  return (
    <dl className="zh-description-grid">
      <dt>{t("purchases.pendingProducts.price.net", "Sin IVA (se guarda)")}</dt>
      <dd>
        <ZHMoneyValue value={breakdown.net} precision="salesUnitPrice" currencySymbol="" />
      </dd>
      {breakdown.vatAmount !== 0 && (
        <>
          <dt>{t("purchases.pendingProducts.price.effective", "PVP efectivo con IVA")}</dt>
          <dd>
            <ZHMoneyValue value={breakdown.effectiveGross} precision="salesUnitPrice" currencySymbol="" />
          </dd>
        </>
      )}
      <dt>{t("purchases.pendingProducts.price.profit", "Utilidad")}</dt>
      <dd className={breakdown.profit != null && breakdown.profit < 0 ? "prl-price--loss" : undefined}>
        {breakdown.profit == null ? (
          "—"
        ) : (
          <ZHMoneyValue value={breakdown.profit} precision="salesUnitPrice" currencySymbol="" />
        )}
      </dd>
      <dt>{t("purchases.pendingProducts.price.marginMarkup", "Margen / recargo")}</dt>
      <dd>
        {percent(breakdown.marginPct)} / {percent(breakdown.markupPct)}
      </dd>
      {breakdown.differs && (
        <dd className="zh-description-grid__full-row prl-row__warning" role="alert">
          {t("purchases.pendingProducts.price.differs", {
            entered: formatMoney(row.salePriceGross ?? 0, priceDecimals),
            effective: formatMoney(breakdown.effectiveGross, priceDecimals),
            decimals: priceDecimals,
          })}
        </dd>
      )}
    </dl>
  );
}
