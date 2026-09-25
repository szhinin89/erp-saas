import { useI18n } from "../../../../i18n/i18n";
import { ZHLineCard } from "../../../../components/zh/ZHLineCard";
import { ZHRowDeleteAction } from "../../../../components/zh/ZHRowDeleteAction";
import { ZHFieldLabel } from "../../../../components/zh/ZHFieldLabel";
import { ZHDataValue } from "../../../../components/zh/ZHDataValue";
import { ZhDecimalInput } from "../../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../../components/zh/inputs/ZhSelect";
import { ZhTextarea } from "../../../../components/zh/inputs/ZhTextarea";
import { Badge } from "../../../../components/PageShell";
import { formatDecimalDisplay } from "../../../../lib/sanitizers";
import { usePrecisionDecimals } from "../../../../hooks/usePrecisionPolicy";
import type { AdjustmentMovementType } from "../types";
import type { useStockAdjustmentFormPage } from "../hooks/useStockAdjustmentFormPage";

type LineView = ReturnType<
  typeof useStockAdjustmentFormPage
>["lineViews"][number];

type Props = {
  index: number;
  view: LineView;
  movementType: AdjustmentMovementType;
  formLocked: boolean;
  onPatch: (
    key: number,
    patch: Partial<LineView["line"]>,
  ) => void;
  onRemove: (key: number) => void;
};

/**
 * INVENTORY-ADJUSTMENTS-03 — una línea del ajuste. Reutiliza `ZHLineCard` + `ZHRowDeleteAction`
 * (mismo armazón de línea documental que Transferencias y Ventas) y los inputs del DS; el CSS
 * local solo define la disposición de las celdas.
 *
 * Costo unitario base: editable y obligatorio en Ingreso; en Egreso NO es editable porque el
 * backend lo deriva del costo promedio móvil e ignora cualquier valor manual — mostrar un input
 * ahí sería prometer un control que no existe.
 */
export function AdjustmentLineCard({
  index,
  view,
  movementType,
  formLocked,
  onPatch,
  onRemove,
}: Props) {
  const { t } = useI18n();
  const { line } = view;
  const baseUnitWord = t("inventory.adjustments.lines.baseUnit", "unidades base");
  // Textos compuestos de solo lectura (opción de <select>, "Equivale a …", "stock UOM"): la
  // semántica se declara y la escala la resuelve el Design System (reactiva a la policy).
  const quantityDecimals = usePrecisionDecimals("quantity");
  const conversionFactorDecimals = usePrecisionDecimals("conversionFactor");
  const unitCostDecimals = usePrecisionDecimals("unitCost");

  return (
    <ZHLineCard
      className="adj-line"
      rail={
        <>
          <span className="adj-line__index zh-text-muted zh-text-xs">
            {String(index + 1).padStart(2, "0")}
          </span>
          {!formLocked && (
            <ZHRowDeleteAction
              compact
              showText={false}
              title={t("inventory.adjustments.actions.removeLine", "Quitar línea")}
              ariaLabel={`${t("inventory.adjustments.actions.removeLine", "Quitar línea")} ${line.itemName}`}
              onClick={() => onRemove(line._key)}
            />
          )}
        </>
      }
    >
      <div className="adj-line__main">
        <div className="adj-line__info">
          {line.sku && <span className="zh-code-value">{line.sku}</span>}
          <div className="zh-row-title" title={line.itemName}>
            {line.itemName}
          </div>
        </div>

        {line.packagingLevels.length > 0 && (
          <div className="adj-line__cell">
            <ZHFieldLabel size="sm">
              {t("inventory.adjustments.lines.presentation", "Presentación")}
            </ZHFieldLabel>
            <ZhSelect
              density="compact"
              value={line.packagingLevelId ?? ""}
              disabled={formLocked}
              aria-label={`${t("inventory.adjustments.lines.presentation", "Presentación")} ${line.itemName}`}
              onChange={(e) =>
                onPatch(line._key, {
                  packagingLevelId: e.target.value || null,
                })
              }
            >
              <option value="">
                {t("inventory.adjustments.lines.baseUnitOption", "Unidad base")} (
                {line.baseUomCode})
              </option>
              {line.packagingLevels.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name} (x{formatDecimalDisplay(p.baseQuantity, conversionFactorDecimals)})
                </option>
              ))}
            </ZhSelect>
          </div>
        )}

        <div className="adj-line__cell">
          <ZHFieldLabel size="sm">
            {t("inventory.adjustments.lines.quantity", "Cantidad")} (
            {view.uomCode})
          </ZHFieldLabel>
          <ZhDecimalInput
            precision="quantity"
            positiveOnly
            density="compact"
            key={`qty-${line._key}-${line.packagingLevelId ?? "base"}`}
            defaultValue={line.quantity}
            disabled={formLocked}
            aria-label={`${t("inventory.adjustments.lines.quantity", "Cantidad")} ${line.itemName}`}
            // 04A1: commit solo tras edición real (no por foco/blur ni por escala oculta).
            onValueCommit={(value) => onPatch(line._key, { quantity: Number(value) || 0 })}
          />
        </div>

        <div className="adj-line__cell">
          <ZHFieldLabel size="sm">
            {t("inventory.adjustments.lines.equivalence", "Equivalencia")}
          </ZHFieldLabel>
          <ZHDataValue variant="numeric">
            {/* ERP-PRECISION-FRONTEND-06B: la equivalencia en unidad base usa quantityDecimals. */}
            {t("inventory.adjustments.lines.equivalentTo", "Equivale a")}{" "}
            {formatDecimalDisplay(view.quantityInBaseUom, quantityDecimals)} {baseUnitWord}
          </ZHDataValue>
        </div>

        <div className="adj-line__cell">
          <ZHFieldLabel size="sm">
            {t("inventory.adjustments.lines.currentStock", "Stock actual")}
          </ZHFieldLabel>
          <ZHDataValue variant="numeric">
            {line.currentStock === null
              ? "—"
              : `${formatDecimalDisplay(line.currentStock, quantityDecimals)} ${line.baseUomCode}`}
          </ZHDataValue>
        </div>

        <div className="adj-line__cell">
          <ZHFieldLabel size="sm">
            {t("inventory.adjustments.lines.unitCostBase", "Costo unitario base")}
          </ZHFieldLabel>
          {movementType === "Ingreso" && !formLocked ? (
            <ZhDecimalInput
              precision="unitCost"
              positiveOnly
              density="compact"
              key={`cost-${line._key}`}
              defaultValue={line.unitCostBase ?? ""}
              aria-label={`${t("inventory.adjustments.lines.unitCostBase", "Costo unitario base")} ${line.itemName}`}
              // 04A1: commit solo tras edición real (no por foco/blur ni por escala oculta).
              onValueCommit={(value) =>
                onPatch(line._key, { unitCostBase: value === "" ? null : Number(value) || 0 })
              }
            />
          ) : (
            <ZHDataValue variant="numeric">
              {line.unitCostBase === null
                ? t(
                    "inventory.adjustments.lines.costFromAverage",
                    "Lo calcula el sistema",
                  )
                : formatDecimalDisplay(line.unitCostBase, unitCostDecimals)}
            </ZHDataValue>
          )}
        </div>

        {view.insufficientStock && (
          <div className="adj-line__warning">
            <Badge
              variant="red"
              size="md"
              label={t(
                "inventory.adjustments.lines.insufficientStock",
                "Stock insuficiente",
              )}
            />
          </div>
        )}

        <div className="adj-line__notes">
          <ZHFieldLabel size="sm">
            {t("inventory.adjustments.lines.notes", "Observación de línea")}
          </ZHFieldLabel>
          <ZhTextarea
            density="compact"
            rows={2}
            key={`notes-${line._key}`}
            defaultValue={line.lineNotes}
            maxLength={500}
            disabled={formLocked}
            aria-label={`${t("inventory.adjustments.lines.notes", "Observación de línea")} ${line.itemName}`}
            onBlur={(e) => onPatch(line._key, { lineNotes: e.target.value })}
          />
        </div>
      </div>
    </ZHLineCard>
  );
}
