import { useEffect, useMemo, useState } from "react";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseCostDistributionType } from "../api/purchaseService";
import {
  simulateCostDistribution,
  type DistributeCostSourceLine,
} from "../utils/purchaseCalc";

interface Props {
  open: boolean;
  lines: DistributeCostSourceLine[];
  totalFreight: number;
  totalOtherCosts: number;
  grandTotal: number;
  onCancel: () => void;
  onApply: (
    costType: PurchaseCostDistributionType,
    amount: number,
    includedLineIds: string[],
  ) => Promise<boolean> | boolean;
}

/**
 * PURCHASE-FREIGHT-DISTRIBUTION-MODAL-01 — el usuario elimina manualmente la línea de
 * transporte/flete/gasto (si existe) en la grilla principal ANTES de abrir este modal; aquí solo
 * digita el valor a distribuir y revisa el prorrateo entre los ítems restantes. Sin detección
 * automática de "transporte" — la selección de líneas incluidas es siempre manual.
 */
export function DistributeCostModal({
  open,
  lines,
  totalFreight,
  totalOtherCosts,
  grandTotal,
  onCancel,
  onApply,
}: Props) {
  const { t } = useI18n();
  const totalAmountDecimals = getDecimalConfig().totalAmount;

  const [costType, setCostType] =
    useState<PurchaseCostDistributionType>("Freight");
  const [amount, setAmount] = useState(0);
  const [includedIds, setIncludedIds] = useState<Set<string>>(new Set());
  const [lastCalculatedSignature, setLastCalculatedSignature] = useState<
    string | null
  >(null);
  const [applying, setApplying] = useState(false);

  // Al abrir, reset: todas las líneas de producto incluidas por defecto (regla F-6).
  useEffect(() => {
    if (open) {
      setIncludedIds(new Set(lines.map((l) => l.id)));
      setCostType("Freight");
      setAmount(0);
      setLastCalculatedSignature(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const signature = useMemo(
    () =>
      `${costType}|${amount}|${[...includedIds].sort().join(",")}`,
    [costType, amount, includedIds],
  );

  const preview = useMemo(
    () => simulateCostDistribution(lines, includedIds, amount),
    [lines, includedIds, amount],
  );

  const includedCount = includedIds.size;
  const includedPreview = preview.filter((p) => p.included);
  const subtotalBaseIncluded = includedPreview.reduce(
    (s, p) => s + p.subtotalBase,
    0,
  );
  const subtotalBaseAll = preview.reduce((s, p) => s + p.subtotalBase, 0);
  const totalDistributed = preview.reduce((s, p) => s + p.allocatedAmount, 0);
  const roundingDiff = Math.round((amount - totalDistributed) * 100) / 100;
  const totalFacturaDespues = grandTotal + totalDistributed;

  const hasInvalidQuantity = includedPreview.some(
    (p) => p.quantityInBaseUom <= 0,
  );
  const validationMessage =
    amount <= 0
      ? t(
          "purchases.distributeCost.errors.amountRequired",
          "El valor a distribuir debe ser mayor a cero.",
        )
      : includedCount === 0
        ? t(
            "purchases.distributeCost.errors.noLines",
            "Debe incluir al menos un ítem.",
          )
        : hasInvalidQuantity
          ? t(
              "purchases.distributeCost.errors.invalidQuantity",
              "Todos los ítems incluidos deben tener cantidad válida.",
            )
          : subtotalBaseIncluded <= 0
            ? t(
                "purchases.distributeCost.errors.baseZero",
                "La base de los ítems incluidos debe ser mayor a cero.",
              )
            : null;

  const canCalculate = validationMessage === null;
  const hasCalculated = lastCalculatedSignature === signature;
  const canApply = canCalculate && hasCalculated && !applying;

  const toggleLine = (id: string) => {
    setIncludedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const handleApplyClick = async () => {
    setApplying(true);
    const ok = await onApply(costType, amount, [...includedIds]);
    setApplying(false);
    if (ok) onCancel();
  };

  return (
    <ZHModal
      open={open}
      onClose={onCancel}
      size="xl"
      title={t("purchases.distributeCost.title", "Distribuir flete/gasto")}
      subtitle={t(
        "purchases.distributeCost.subtitle",
        "Reparte un valor entre los ítems de la compra. Elimine antes cualquier línea de transporte que no deba quedar como inventario.",
      )}
      footer={
        <>
          <ZHBtn variant="ghost" size="md" onClick={onCancel} disabled={applying}>
            {t("common.cancel", "Cancelar")}
          </ZHBtn>
          <ZHBtn
            variant="primary"
            size="md"
            onClick={handleApplyClick}
            disabled={!canApply}
          >
            {t("purchases.distributeCost.apply", "Aplicar")}
          </ZHBtn>
        </>
      }
    >
      <div className="pdc-toolbar">
        <ZHField
          density="compact"
          label={t("purchases.distributeCost.amount", "Valor a distribuir")}
        >
          <ZhDecimalInput
            decimals={totalAmountDecimals}
            positiveOnly
            defaultValue={amount}
            onBlur={(e) => setAmount(Number(e.target.value) || 0)}
          />
        </ZHField>
        <ZHField
          density="compact"
          label={t("purchases.distributeCost.type", "Tipo")}
        >
          <ZhSelect
            density="compact"
            value={costType}
            onChange={(e) =>
              setCostType(e.target.value as PurchaseCostDistributionType)
            }
          >
            <option value="Freight">
              {t("purchases.distributeCost.typeFreight", "Flete")}
            </option>
            <option value="OtherCost">
              {t("purchases.distributeCost.typeOther", "Otro gasto")}
            </option>
          </ZhSelect>
        </ZHField>
        <ZHBtn
          variant="secondary"
          size="md"
          disabled={!canCalculate}
          onClick={() => setLastCalculatedSignature(signature)}
        >
          {t("purchases.distributeCost.calculate", "Calcular")}
        </ZHBtn>
      </div>

      {validationMessage && (
        <ZHPageNotice variant="warning" message={validationMessage} />
      )}

      <table className="pf-table pdc-table">
        <thead>
          <tr>
            <th>{t("purchases.distributeCost.col.include", "Incluir")}</th>
            <th>{t("purchases.distributeCost.col.item", "Ítem")}</th>
            <th className="zh-text-align-right">
              {t("purchases.distributeCost.col.quantity", "Cantidad")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.distributeCost.col.currentCost", "Costo unit. actual")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.distributeCost.col.subtotalBase", "Subtotal base")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.distributeCost.col.participation", "% particip.")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.distributeCost.col.allocated", "Valor asignado")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.distributeCost.col.allocatedPerUnit", "Asignado/unidad")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.distributeCost.col.newCost", "Nuevo costo unit.")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.distributeCost.col.newTotal", "Nuevo total línea")}
            </th>
          </tr>
        </thead>
        <tbody>
          {preview.map((p) => (
            <tr key={p.lineId}>
              <td>
                <input
                  type="checkbox"
                  checked={p.included}
                  onChange={() => toggleLine(p.lineId)}
                />
              </td>
              <td>{p.description}</td>
              <td className="zh-table-cell--num">{p.quantity}</td>
              <td className="zh-table-cell--num">
                <ZHMoneyValue
                  value={p.currentUnitCost}
                  decimals={totalAmountDecimals}
                  currencySymbol=""
                />
              </td>
              <td className="zh-table-cell--num">
                <ZHMoneyValue
                  value={p.subtotalBase}
                  decimals={totalAmountDecimals}
                  currencySymbol=""
                />
              </td>
              <td className="zh-table-cell--num">
                {hasCalculated ? `${p.participationPct.toFixed(2)}%` : "—"}
              </td>
              <td className="zh-table-cell--num">
                <ZHMoneyValue
                  value={hasCalculated ? p.allocatedAmount : null}
                  decimals={totalAmountDecimals}
                  currencySymbol=""
                />
              </td>
              <td className="zh-table-cell--num">
                <ZHMoneyValue
                  value={hasCalculated ? p.allocatedPerUnit : null}
                  decimals={totalAmountDecimals}
                  currencySymbol=""
                />
              </td>
              <td className="zh-table-cell--num">
                <ZHMoneyValue
                  value={hasCalculated ? p.newUnitCost : null}
                  decimals={totalAmountDecimals}
                  currencySymbol=""
                />
              </td>
              <td className="zh-table-cell--num">
                <ZHMoneyValue
                  value={hasCalculated ? p.newLineTotal : null}
                  decimals={totalAmountDecimals}
                  currencySymbol=""
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="pdc-totals">
        <div className="pdc-totals__row">
          <span>
            {t(
              "purchases.distributeCost.totals.baseIncluded",
              "Subtotal base ítems incluidos",
            )}
          </span>
          <ZHMoneyValue
            value={subtotalBaseIncluded}
            decimals={totalAmountDecimals}
            currencySymbol=""
            emphasis="strong"
          />
        </div>
        <div className="pdc-totals__row">
          <span>
            {t(
              "purchases.distributeCost.totals.baseAll",
              "Subtotal base todos los ítems",
            )}
          </span>
          <ZHMoneyValue
            value={subtotalBaseAll}
            decimals={totalAmountDecimals}
            currencySymbol=""
            emphasis="strong"
          />
        </div>
        <div className="pdc-totals__row">
          <span>
            {t("purchases.distributeCost.totals.amount", "Valor a distribuir")}
          </span>
          <ZHMoneyValue
            value={amount}
            decimals={totalAmountDecimals}
            currencySymbol=""
            emphasis="strong"
          />
        </div>
        <div className="pdc-totals__row">
          <span>
            {t("purchases.distributeCost.totals.distributed", "Total distribuido")}
          </span>
          <ZHMoneyValue
            value={hasCalculated ? totalDistributed : null}
            decimals={totalAmountDecimals}
            currencySymbol=""
            emphasis="strong"
          />
        </div>
        <div className="pdc-totals__row">
          <span>
            {t(
              "purchases.distributeCost.totals.roundingDiff",
              "Diferencia de redondeo",
            )}
          </span>
          <ZHMoneyValue
            value={hasCalculated ? roundingDiff : null}
            decimals={totalAmountDecimals}
            currencySymbol=""
            emphasis="strong"
          />
        </div>
        <div className="pdc-totals__row">
          <span>
            {t(
              "purchases.distributeCost.totals.invoiceBefore",
              "Total factura antes",
            )}
          </span>
          <ZHMoneyValue
            value={grandTotal}
            decimals={totalAmountDecimals}
            currencySymbol=""
            emphasis="strong"
          />
        </div>
        <div className="pdc-totals__row">
          <span>
            {t(
              "purchases.distributeCost.totals.invoiceAfter",
              "Total factura después",
            )}
          </span>
          <ZHMoneyValue
            value={hasCalculated ? totalFacturaDespues : null}
            decimals={totalAmountDecimals}
            currencySymbol=""
            emphasis="strong"
          />
        </div>
        <div className="pdc-totals__row">
          <span>
            {t(
              "purchases.distributeCost.totals.payableExpected",
              "Total documento / CxP esperado",
            )}
          </span>
          <ZHMoneyValue
            value={hasCalculated ? totalFacturaDespues : null}
            decimals={totalAmountDecimals}
            currencySymbol=""
            emphasis="strong"
          />
        </div>
        <div className="pdc-totals__row">
          <span>
            {t(
              "purchases.distributeCost.totals.squareDiff",
              "Diferencia para cuadrar factura",
            )}
          </span>
          <ZHMoneyValue
            value={hasCalculated ? roundingDiff : null}
            decimals={totalAmountDecimals}
            currencySymbol=""
            emphasis="strong"
          />
        </div>
        <div className="pdc-totals__row">
          <span>
            {t(
              "purchases.distributeCost.totals.currentFreight",
              "Flete actual del documento",
            )}
          </span>
          <ZHMoneyValue
            value={totalFreight}
            decimals={totalAmountDecimals}
            currencySymbol=""
            emphasis="strong"
          />
        </div>
        <div className="pdc-totals__row">
          <span>
            {t(
              "purchases.distributeCost.totals.currentOtherCosts",
              "Otros gastos actuales del documento",
            )}
          </span>
          <ZHMoneyValue
            value={totalOtherCosts}
            decimals={totalAmountDecimals}
            currencySymbol=""
            emphasis="strong"
          />
        </div>
      </div>
    </ZHModal>
  );
}
