import type { FieldArrayWithId, UseFieldArrayAppend, UseFieldArrayRemove } from "react-hook-form";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { formatMoney } from "../../../lib/sanitizers";
import { useI18n } from "../../../i18n/i18n";
import type {
  PurchaseCreditNoteDraftFormValues,
  PurchaseCreditNoteTaxSummaryLineFormValues,
} from "../schemas/purchaseCreditNoteSchema";
import type { PurchaseInvoiceTaxSummaryDto } from "../api/purchaseService";
import "../styles/purchase-credit-note.css";

interface Props {
  /** Resúmenes fiscales reales de la compra afectada (`purchaseService.getTaxSummaries`), con base ya acreditada/disponible calculada por el servidor. */
  taxSummaries: PurchaseInvoiceTaxSummaryDto[];
  selected: FieldArrayWithId<PurchaseCreditNoteDraftFormValues, "taxSummaryLines", "id">[];
  append: UseFieldArrayAppend<PurchaseCreditNoteDraftFormValues, "taxSummaryLines">;
  remove: UseFieldArrayRemove;
  disabled?: boolean;
}

/** Espejo de ERP.Domain.Common.SriTaxCalculator — solo vista previa, el backend recalcula autoritativamente. */
function computeTaxPreview(taxableBase: number, vatRate: number, iceRate: number) {
  const round2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100;
  const ice = iceRate > 0 ? round2((taxableBase * iceRate) / 100) : 0;
  const vatBase = taxableBase + ice;
  const vat = vatRate > 0 ? round2((vatBase * vatRate) / 100) : 0;
  return { ice, vat, total: round2(taxableBase + ice + vat) };
}

/**
 * FLOW-READY-02C-R1.2 — flujo principal de descuento/promoción: una fila por resumen fiscal real
 * de la compra afectada. El usuario solo edita "base descuento a aplicar" — nunca cantidades, IVA
 * ni ICE (heredados/calculados siempre desde la compra original). Mismo patrón que
 * `PurchaseReturnableLinesEditor.tsx` (filas fijas del servidor, `useFieldArray` upsert por fila) —
 * no reutilizable directamente porque las columnas/reglas de negocio son distintas (impuesto vs.
 * cantidad/bodega).
 */
export function PurchaseCreditNoteTaxSummaryLinesEditor({
  taxSummaries,
  selected,
  append,
  remove,
  disabled,
}: Readonly<Props>) {
  const { t } = useI18n();

  if (taxSummaries.length === 0) {
    return (
      <p className="pcn-lines-empty">
        {t(
          "purchases.creditNote.taxSummaryLines.noSummaries",
          "Esta compra no tiene resumen fiscal persistido. Confirme que fue creada después de la actualización o ejecute backfill controlado.",
        )}
      </p>
    );
  }

  const indexOf = (sourceId: string) =>
    selected.findIndex((l) => l.sourcePurchaseInvoiceTaxSummaryId === sourceId);

  const handleBaseChange = (summary: PurchaseInvoiceTaxSummaryDto, raw: string) => {
    const base = Number(raw);
    const idx = indexOf(summary.id);
    if (!raw || base <= 0) {
      if (idx >= 0) remove(idx);
      return;
    }
    const entry: PurchaseCreditNoteTaxSummaryLineFormValues = {
      sourcePurchaseInvoiceTaxSummaryId: summary.id,
      taxableBase: base,
    };
    if (idx >= 0) remove(idx);
    append(entry);
  };

  return (
    <div className="table-scroll">
      <table className="pf-table pcn-lines-table">
        <thead>
          <tr>
            <th>{t("purchases.creditNote.taxSummaryLines.tax", "Impuesto")}</th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.taxSummaryLines.purchaseBase", "Base compra")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.taxSummaryLines.credited", "Ya acreditado")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.taxSummaryLines.available", "Disponible")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.taxSummaryLines.discountBase", "Base descuento a aplicar")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.taxSummaryLines.iceCredit", "ICE crédito")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.taxSummaryLines.vatCredit", "IVA crédito")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.taxSummaryLines.totalCredit", "Total NC")}
            </th>
          </tr>
        </thead>
        <tbody>
          {taxSummaries.map((summary) => {
            const idx = indexOf(summary.id);
            const baseInput = idx >= 0 ? String(selected[idx].taxableBase) : "";
            const exceeds = Number(baseInput || 0) > summary.availableTaxableBase;
            const preview = computeTaxPreview(
              Number(baseInput || 0),
              summary.vatRate,
              summary.iceRate,
            );
            return (
              <tr key={summary.id}>
                <td>
                  <div className="pcn-lines-table__desc">
                    {summary.vatName ?? summary.vatCode}
                    {summary.iceCode && ` + ${summary.iceName ?? summary.iceCode}`}
                  </div>
                  <div className="pcn-lines-table__meta">
                    {t("purchases.creditNote.taxSummaryLines.vatCode", "IVA")} {summary.vatCode}
                    {summary.iceCode &&
                      ` · ${t("purchases.creditNote.taxSummaryLines.iceCode", "ICE")} ${summary.iceCode}`}
                  </div>
                </td>
                <td className="zh-table-cell--num">{formatMoney(summary.taxableBase)}</td>
                <td className="zh-table-cell--num">{formatMoney(summary.creditedTaxableBase)}</td>
                <td className="zh-table-cell--num">{formatMoney(summary.availableTaxableBase)}</td>
                <td className="zh-text-align-right">
                  <ZhDecimalInput
                    decimals={2}
                    positiveOnly
                    disabled={disabled || summary.availableTaxableBase <= 0}
                    value={baseInput}
                    onChange={(e) => handleBaseChange(summary, e.target.value)}
                    className={exceeds ? "pcn-qty-input--error" : undefined}
                  />
                  {exceeds && (
                    <div className="pcn-lines-table__error">
                      {t(
                        "purchases.creditNote.taxSummaryLines.exceedsAvailable",
                        "Excede la base disponible",
                      )}{" "}
                      ({formatMoney(summary.availableTaxableBase)}).
                    </div>
                  )}
                </td>
                <td className="zh-table-cell--num">{formatMoney(preview.ice)}</td>
                <td className="zh-table-cell--num">{formatMoney(preview.vat)}</td>
                <td className="zh-table-cell--num">{formatMoney(preview.total)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
