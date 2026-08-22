import { useI18n } from "../../../../i18n/i18n";
import { ZHLineCard } from "../../../../components/zh/ZHLineCard";
import { ZHRowDeleteAction } from "../../../../components/zh/ZHRowDeleteAction";
import { ZHFieldLabel } from "../../../../components/zh/ZHFieldLabel";
import { ZHDataValue } from "../../../../components/zh/ZHDataValue";
import { ZhDecimalInput } from "../../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../../components/zh/inputs/ZhSelect";
import { ZhTextarea } from "../../../../components/zh/inputs/ZhTextarea";
import { Badge } from "../../../../components/PageShell";
import { formatMoney } from "../../../../lib/sanitizers";
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
                  {p.name} (x{formatMoney(p.baseQuantity, 2)})
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
            decimals={2}
            positiveOnly
            density="compact"
            key={`qty-${line._key}-${line.packagingLevelId ?? "base"}`}
            defaultValue={line.quantity}
            disabled={formLocked}
            aria-label={`${t("inventory.adjustments.lines.quantity", "Cantidad")} ${line.itemName}`}
            onBlur={(e) =>
              onPatch(line._key, { quantity: Number(e.target.value) || 0 })
            }
          />
        </div>

        <div className="adj-line__cell">
          <ZHFieldLabel size="sm">
            {t("inventory.adjustments.lines.equivalence", "Equivalencia")}
          </ZHFieldLabel>
          <ZHDataValue variant="numeric">
            {t("inventory.adjustments.lines.equivalentTo", "Equivale a")}{" "}
            {formatMoney(view.quantityInBaseUom, 2)} {baseUnitWord}
          </ZHDataValue>
        </div>

        <div className="adj-line__cell">
          <ZHFieldLabel size="sm">
            {t("inventory.adjustments.lines.currentStock", "Stock actual")}
          </ZHFieldLabel>
          <ZHDataValue variant="numeric">
            {line.currentStock === null
              ? "—"
              : `${formatMoney(line.currentStock, 2)} ${line.baseUomCode}`}
          </ZHDataValue>
        </div>

        <div className="adj-line__cell">
          <ZHFieldLabel size="sm">
            {t("inventory.adjustments.lines.unitCostBase", "Costo unitario base")}
          </ZHFieldLabel>
          {movementType === "Ingreso" && !formLocked ? (
            <ZhDecimalInput
              decimals={4}
              positiveOnly
              density="compact"
              key={`cost-${line._key}`}
              defaultValue={line.unitCostBase ?? ""}
              aria-label={`${t("inventory.adjustments.lines.unitCostBase", "Costo unitario base")} ${line.itemName}`}
              onBlur={(e) =>
                onPatch(line._key, {
                  unitCostBase:
                    e.target.value === "" ? null : Number(e.target.value) || 0,
                })
              }
            />
          ) : (
            <ZHDataValue variant="numeric">
              {line.unitCostBase === null
                ? t(
                    "inventory.adjustments.lines.costFromAverage",
                    "Lo calcula el sistema",
                  )
                : formatMoney(line.unitCostBase, 4)}
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
