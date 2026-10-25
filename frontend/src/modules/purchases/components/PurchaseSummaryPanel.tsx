import type { ReactNode } from "react";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import { calcVatBreakdown } from "../utils/purchaseCalc";

export interface PurchaseSummaryTotals {
  subtotal: number;
  discount: number;
  ice: number;
  vat: number;
  irbpnr: number;
  /** Costos externos distribuidos: suman al total del documento y a la CxP. */
  externalCosts: number;
  /** Transporte del XML redistribuido a productos: ya incluido en las líneas, no suma otra vez. */
  xmlTransportCosts: number;
  total: number;
}

interface Props {
  lines: PurchaseLineFormValues[];
  vatRatesMap: Record<string, number>;
  iceRatesMap: Record<string, number>;
  totals: PurchaseSummaryTotals;
}

/**
 * PURCHASE-INVOICE-TOTALS-SUMMARY-PANEL-01 — panel "Resumen" de solo lectura. El desglose por tarifa IVA
 * se calcula desde el draft editable (nunca desde el snapshot XML); los totales los entrega el hook
 * (persistidos sin cambios locales, del formulario en caso contrario).
 */
export function PurchaseSummaryPanel({ lines, vatRatesMap, iceRatesMap, totals }: Props) {
  const { t } = useI18n();
  const vatBreakdown = calcVatBreakdown(lines, vatRatesMap, iceRatesMap);
  const hasIce = vatBreakdown.some((row) => row.ice !== 0);
  const row = (label: ReactNode, value: ReactNode, valueClass = "") => (
    <div className="pf-totals__row">
      <span className="pf-totals__label">{label}</span>
      <span className={`pf-totals__value${valueClass}`}>{value}</span>
    </div>
  );
  return (
    <div className="pf-totals">
      <div className="pf-totals__header">
        <h4>
          <span className="material-symbols-outlined pf-totals__title-icon--primary">summarize</span>{" "}
          {t("purchases.summary.title", "Resumen")}
        </h4>
      </div>
      {vatBreakdown.length > 0 && (
        <div className="pf-totals__vat-breakdown">
          <table className="table table--compact table--neutral">
            <thead>
              <tr>
                <th>{t("purchases.summary.vatRate", "% IVA")}</th>
                <th className="zh-table-cell--num">{t("purchases.summary.taxableBase", "Base imponible")}</th>
                <th className="zh-table-cell--num">{t("purchases.lines.vatShort", "IVA")}</th>
                {hasIce && <th className="zh-table-cell--num">{t("purchases.lines.iceShort", "ICE")}</th>}
                <th className="zh-table-cell--num">{t("purchases.summary.rowTotal", "Total")}</th>
              </tr>
            </thead>
            <tbody>
              {vatBreakdown.map((r) => (
                <tr key={r.vatCode}>
                  <td>{r.vatPercent}%</td>
                  <td className="zh-table-cell--num"><ZHMoneyValue value={r.taxableBase} precision="money" /></td>
                  <td className="zh-table-cell--num"><ZHMoneyValue value={r.vat} precision="tax" /></td>
                  {hasIce && <td className="zh-table-cell--num"><ZHMoneyValue value={r.ice} precision="tax" /></td>}
                  <td className="zh-table-cell--num"><ZHMoneyValue value={r.total} precision="money" /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="pf-totals__body">
        {row(t("purchases.summary.subtotal", "Subtotal"), <ZHMoneyValue value={totals.subtotal} precision="money" />)}
        {row(t("purchases.summary.discount", "Descuento"),
          <>-<ZHMoneyValue value={totals.discount} precision="money" /></>, " pf-totals__value--warning")}
        {row(t("purchases.lines.iceShort", "ICE"), <ZHMoneyValue value={totals.ice} precision="tax" />)}
        {row(<>{t("purchases.lines.vatShort", "IVA")}<ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_VAT} /></>,
          <ZHMoneyValue value={totals.vat} precision="tax" />)}
        {!!totals.irbpnr && row(
          <>IRBPNR{" "}
            <span className="pf-totals__value--warning"
              title={t("purchases.summary.irbpnrHint", "Valor del comprobante XML — incluido en el Gran Total.")}>(i)</span>
          </>,
          <ZHMoneyValue value={totals.irbpnr} precision="tax" />)}
        {row(t("purchases.summary.externalCosts", "Costos adicionales externos"),
          <ZHMoneyValue value={totals.externalCosts} precision="money" />)}
        {!!totals.xmlTransportCosts && row(
          <span title={t("purchases.summary.xmlTransportHint",
            "Transporte del comprobante XML asignado al costo de los productos — ya incluido en las líneas.")}>
            {t("purchases.summary.xmlTransportCosts", "Transporte XML redistribuido")} (i)
          </span>,
          <ZHMoneyValue value={totals.xmlTransportCosts} precision="money" />)}
        <div className="pf-totals__divider" />
        <div className="pf-totals__row">
          <span className="pf-totals__label pf-totals__label--strong">{t("purchases.summary.lines", "Líneas")}</span>
          <span className="pf-totals__value">{lines.length}</span>
        </div>
      </div>
      <div className="pf-totals__grand">
        <span className="pf-totals__grand-label">{t("purchases.summary.total", "TOTAL")}</span>
        <span className="pf-totals__grand-value"><ZHMoneyValue value={totals.total} precision="money" /></span>
      </div>
    </div>
  );
}
