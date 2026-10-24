import { useEffect, useMemo, useState, type ReactNode } from "react";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { formatMoney } from "../../../lib/sanitizers";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseCostDistributionType } from "../api/purchaseService";
import {
  simulateCostDistribution,
  type DistributeCostSourceLine,
} from "../utils/purchaseCalc";

type DistributionPreviewRow = ReturnType<typeof simulateCostDistribution>[number];

interface Props {
  open: boolean;
  error?: string;
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
  error,
  lines,
  totalFreight,
  totalOtherCosts,
  grandTotal,
  onCancel,
  onApply,
}: Props) {
  const { t } = useI18n();
  // Totales/valores agregados de la simulación (freight, valor a distribuir, totales de factura)
  // → precision="money"; costos por unidad (actual/nuevo, asignado por unidad) → "unitCost" —
  // misma semántica que purchaseLinePresentation.ts. Textos compuestos (06): cantidad y % de
  // participación con la escala semántica del hook.
  const quantityDecimals = usePrecisionDecimals("quantity");
  const percentageDecimals = usePrecisionDecimals("percentage");

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
  // The empty amount is the starting state: an instruction on its own field, not a warning.
  const amountHint =
    amount <= 0
      ? t(
          "purchases.distributeCost.errors.amountRequired",
          "El valor a distribuir debe ser mayor a cero.",
        )
      : null;
  const linesMessage =
    includedCount === 0
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

  const canCalculate = amountHint === null && linesMessage === null;
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

  // Una definición por columna: encabezado de escritorio y data-label del apilado móvil.
  const itemHeader = t("purchases.distributeCost.col.item", "Ítem");
  const includeLabel = t("purchases.distributeCost.col.include", "Incluir");
  const calculated = (value: number) => (hasCalculated ? value : null);
  const numericColumns: {
    key: string;
    label: string;
    render: (p: DistributionPreviewRow) => ReactNode;
  }[] = [
    {
      key: "quantity",
      label: t("purchases.distributeCost.col.quantity", "Cantidad"),
      render: (p) => formatMoney(p.quantity, quantityDecimals),
    },
    {
      key: "currentCost",
      label: t("purchases.distributeCost.col.currentCost", "Costo unit. actual"),
      render: (p) => <ZHMoneyValue value={p.currentUnitCost} precision="unitCost" currencySymbol="" />,
    },
    {
      key: "subtotalBase",
      label: t("purchases.distributeCost.col.subtotalBase", "Subtotal base"),
      render: (p) => <ZHMoneyValue value={p.subtotalBase} precision="money" currencySymbol="" />,
    },
    {
      key: "participation",
      label: t("purchases.distributeCost.col.participation", "% particip."),
      render: (p) => (hasCalculated ? `${formatMoney(p.participationPct, percentageDecimals)}%` : "—"),
    },
    {
      key: "allocated",
      label: t("purchases.distributeCost.col.allocated", "Valor asignado"),
      render: (p) => <ZHMoneyValue value={calculated(p.allocatedAmount)} precision="money" currencySymbol="" />,
    },
    {
      key: "allocatedPerUnit",
      label: t("purchases.distributeCost.col.allocatedPerUnit", "Asignado/unidad"),
      render: (p) => <ZHMoneyValue value={calculated(p.allocatedPerUnit)} precision="unitCost" currencySymbol="" />,
    },
    {
      key: "newCost",
      label: t("purchases.distributeCost.col.newCost", "Nuevo costo unit."),
      render: (p) => <ZHMoneyValue value={calculated(p.newUnitCost)} precision="unitCost" currencySymbol="" />,
    },
    {
      key: "newTotal",
      label: t("purchases.distributeCost.col.newTotal", "Nuevo total línea"),
      render: (p) => <ZHMoneyValue value={calculated(p.newLineTotal)} precision="money" currencySymbol="" />,
    },
  ];

  // Mismos ocho conceptos de siempre, agrupados: primero el resultado de cada grupo.
  const summaryGroups: {
    key: string;
    title: string;
    rows: { label: string; value: number | null; primary?: boolean }[];
  }[] = [
    {
      key: "distribution",
      title: t("purchases.distributeCost.totals.groupDistribution", "Distribución"),
      rows: [
        { label: t("purchases.distributeCost.totals.distributed", "Total distribuido"), value: calculated(totalDistributed), primary: true },
        { label: t("purchases.distributeCost.totals.roundingDiff", "Diferencia de redondeo / por cuadrar"), value: calculated(roundingDiff) },
        { label: t("purchases.distributeCost.totals.baseIncluded", "Subtotal base ítems incluidos"), value: subtotalBaseIncluded },
        { label: t("purchases.distributeCost.totals.baseAll", "Subtotal base todos los ítems"), value: subtotalBaseAll },
      ],
    },
    {
      key: "invoice",
      title: t("purchases.distributeCost.totals.groupInvoice", "Factura"),
      rows: [
        { label: t("purchases.distributeCost.totals.invoiceAfter", "Total factura después / CxP esperada"), value: calculated(totalFacturaDespues), primary: true },
        { label: t("purchases.distributeCost.totals.invoiceBefore", "Total factura antes"), value: grandTotal },
        { label: t("purchases.distributeCost.totals.currentFreight", "Flete actual del documento"), value: totalFreight },
        { label: t("purchases.distributeCost.totals.currentOtherCosts", "Otros gastos actuales del documento"), value: totalOtherCosts },
      ],
    },
  ];

  return (
    <ZHModal
      open={open}
      onClose={onCancel}
      size="2xl"
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
      {error && <ZHPageNotice variant="error" message={error} />}
      <div className="pdc-toolbar">
        <ZHField
          density="compact"
          label={t("purchases.distributeCost.amount", "Valor a distribuir")}
          hint={amountHint}
          hintType="muted"
        >
          <ZhDecimalInput
            precision="money"
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

      {linesMessage && (
        <ZHPageNotice variant="warning" message={linesMessage} />
      )}

      {/* Preview técnico calculado en vivo (excepción documentada de ZHDataTable, frontend.md):
          checkbox por fila y data-label por celda para el apilado móvil de .responsive-table.
          El scroll horizontal vive solo en .table-scroll; la columna Ítem queda fija. */}
      <div className="table-scroll pdc-table-scroll">
        <table className="table table--compact table--sticky-column responsive-table pdc-table">
          <thead>
            <tr>
              <th className="table__sticky-cell pdc-table__item">{itemHeader}</th>
              {numericColumns.map((col) => (
                <th key={col.key} className="zh-text-align-right">
                  {col.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {preview.map((p) => (
              <tr key={p.lineId} className={p.included ? undefined : "pdc-table__row--excluded"}>
                <td className="table__sticky-cell pdc-table__item">
                  <label className="pdc-table__include">
                    <input
                      type="checkbox"
                      checked={p.included}
                      onChange={() => toggleLine(p.lineId)}
                      aria-label={`${includeLabel}: ${p.description}`}
                    />
                    <span className="pdc-table__desc" title={p.description}>
                      {p.description}
                    </span>
                  </label>
                </td>
                {numericColumns.map((col) => (
                  <td key={col.key} className="zh-table-cell--num" data-label={col.label}>
                    {col.render(p)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="pdc-summary">
        {summaryGroups.map((group) => (
          <section key={group.key} className="pdc-summary__group" aria-label={group.title}>
            <h4 className="zh-section-title pdc-summary__title">{group.title}</h4>
            <div className="zh-summary-grid">
              {group.rows.map((row) => (
                <div
                  key={row.label}
                  className={`pdc-totals__row${row.primary ? " pdc-totals__row--primary" : ""}`}
                >
                  <span className="zh-summary-grid__label">{row.label}</span>
                  <span className="zh-summary-grid__value">
                    <ZHMoneyValue
                      value={row.value}
                      precision="money"
                      currencySymbol=""
                      emphasis="strong"
                    />
                  </span>
                </div>
              ))}
            </div>
          </section>
        ))}
      </div>
    </ZHModal>
  );
}
