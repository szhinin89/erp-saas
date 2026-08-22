import { useI18n } from "../../../../i18n/i18n";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHInfoRow } from "../../../../components/zh/ZHInfoRow";
import { ZHFieldLabel } from "../../../../components/zh/ZHFieldLabel";
import { ZHDataValue } from "../../../../components/zh/ZHDataValue";
import { ZhSelect } from "../../../../components/zh/inputs/ZhSelect";
import { ZhTextarea } from "../../../../components/zh/inputs/ZhTextarea";
import {
  Badge,
  EmptyState,
  LoadingState,
  NoAccessPage,
} from "../../../../components/PageShell";
import { formatMoney } from "../../../../lib/sanitizers";
import { useStockAdjustmentFormPage } from "../hooks/useStockAdjustmentFormPage";
import { AdjustmentProductPicker } from "../components/AdjustmentProductPicker";
import { AdjustmentLineCard } from "../components/AdjustmentLineCard";
import { AdjustmentLifecycleModals } from "../components/AdjustmentLifecycleModals";
import { AdjustmentAuditRows } from "../components/AdjustmentAuditRows";
import { adjustmentStatusBadge } from "../utils/adjustmentStatusBadge";
import "./StockAdjustmentFormPage.css";

/**
 * INVENTORY-ADJUSTMENTS-03 — Pantalla 2: crear / editar borrador / consultar un ajuste.
 * El modo se deriva de la ruta (`/new` vs `/:id`) más el estado del documento: en Executed o
 * Cancelled todo el formulario queda bloqueado (`formLocked`), misma convención que
 * `StockTransferPage`. Ejecutar y Anular pasan siempre por `ZHConfirmModal`.
 */
export function StockAdjustmentFormPage() {
  const { t } = useI18n();
  const ctx = useStockAdjustmentFormPage();

  if (!ctx.canView) {
    return (
      <NoAccessPage
        title={t("inventory.adjustments.form.title", "Ajuste de inventario")}
      />
    );
  }

  const badge = adjustmentStatusBadge(ctx.status, t);
  const isExecuted = ctx.adjustment?.status === "Executed";
  const canSave = ctx.isDraft && (ctx.adjustment ? ctx.canUpdate : ctx.canCreate);

  return (
    <ErpPageTemplate
      kicker={t("inventory.adjustments.kicker", "Inventario")}
      title={
        ctx.adjustment
          ? `${t("inventory.adjustments.form.title", "Ajuste de inventario")} ${ctx.adjustment.adjustmentNumber}`
          : t("inventory.adjustments.form.newTitle", "Nuevo ajuste de inventario")
      }
      subtitle={t(
        "inventory.adjustments.form.subtitle",
        "El borrador no afecta el stock. El movimiento se registra al ejecutar.",
      )}
      action={<Badge label={badge.label} variant={badge.variant} size="md" />}
    >
      <div className="adj-form">
        {ctx.loadError && (
          <ZHPageNotice
            variant="error"
            message={t("common.errorPrefix", "Error:")}
            detail={ctx.loadError}
          />
        )}
        {ctx.saveError && (
          <ZHPageNotice
            variant="error"
            message={t("common.errorPrefix", "Error:")}
            detail={ctx.saveError}
          />
        )}
        {ctx.lifecycle.actionError && (
          <ZHPageNotice
            variant="error"
            message={t("common.errorPrefix", "Error:")}
            detail={ctx.lifecycle.actionError}
          />
        )}
        {ctx.formLocked && (
          <ZHPageNotice
            variant="neutral"
            message={t(
              "inventory.adjustments.messages.readOnly",
              "Este ajuste ya no es un borrador: se muestra en modo consulta.",
            )}
          />
        )}

        {ctx.loading ? (
          <LoadingState />
        ) : (
          <>
            <ZHCard
              title={t("inventory.adjustments.sections.header", "Datos del ajuste")}
            >
              <ZHGrid cols={2}>
                <ZHField
                  label={t(
                    "inventory.adjustments.fields.movementType",
                    "Tipo de movimiento",
                  )}
                  required
                  error={ctx.errors.movementType?.message}
                >
                  <ZhSelect
                    disabled={ctx.formLocked}
                    aria-required="true"
                    aria-label={t(
                      "inventory.adjustments.fields.movementType",
                      "Tipo de movimiento",
                    )}
                    {...ctx.form.register("movementType")}
                  >
                    <option value="Ingreso">
                      {t("inventory.adjustments.movementType.ingreso", "Ingreso")}
                    </option>
                    <option value="Egreso">
                      {t("inventory.adjustments.movementType.egreso", "Egreso")}
                    </option>
                  </ZhSelect>
                </ZHField>

                <ZHField
                  label={t("inventory.adjustments.fields.warehouse", "Bodega")}
                  required
                  error={ctx.errors.warehouseId?.message}
                >
                  <ZhSelect
                    disabled={ctx.formLocked}
                    aria-required="true"
                    aria-label={t(
                      "inventory.adjustments.fields.warehouse",
                      "Bodega",
                    )}
                    {...ctx.form.register("warehouseId")}
                  >
                    <option value="">
                      {t(
                        "inventory.adjustments.placeholders.warehouse",
                        "— seleccione bodega —",
                      )}
                    </option>
                    {ctx.warehouses.map((w) => (
                      <option key={w.id} value={w.id}>
                        {w.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>

                <ZHField
                  label={t("inventory.adjustments.fields.reason", "Motivo")}
                  required
                  error={ctx.errors.reasonId?.message}
                  hint={
                    ctx.selectableReasons.length === 0
                      ? t(
                          "inventory.adjustments.messages.noReasonsForType",
                          "No hay motivos activos para este tipo de movimiento.",
                        )
                      : undefined
                  }
                  hintType="warning"
                >
                  <ZhSelect
                    disabled={ctx.formLocked}
                    aria-required="true"
                    aria-label={t("inventory.adjustments.fields.reason", "Motivo")}
                    {...ctx.form.register("reasonId")}
                  >
                    <option value="">
                      {t(
                        "inventory.adjustments.placeholders.reason",
                        "— seleccione motivo —",
                      )}
                    </option>
                    {ctx.selectableReasons.map((r) => (
                      <option key={r.id} value={r.id}>
                        {r.code} — {r.name}
                      </option>
                    ))}
                    {/* Un ajuste guardado puede apuntar a un motivo desactivado después: se
                        agrega solo para poder mostrarlo, nunca como opción nueva. */}
                    {ctx.selectedReason &&
                      !ctx.selectableReasons.some(
                        (r) => r.id === ctx.selectedReason?.id,
                      ) && (
                        <option value={ctx.selectedReason.id}>
                          {ctx.selectedReason.code} — {ctx.selectedReason.name}
                        </option>
                      )}
                  </ZhSelect>
                </ZHField>

                <ZHField
                  label={t("inventory.adjustments.fields.notes", "Observaciones")}
                  required={ctx.notesRequired}
                  error={ctx.errors.notes?.message}
                >
                  <ZhTextarea
                    rows={3}
                    maxLength={1000}
                    disabled={ctx.formLocked}
                    aria-required={ctx.notesRequired ? "true" : undefined}
                    aria-label={t(
                      "inventory.adjustments.fields.notes",
                      "Observaciones",
                    )}
                    {...ctx.form.register("notes")}
                  />
                </ZHField>
              </ZHGrid>

              {ctx.adjustment && (
                <AdjustmentAuditRows adjustment={ctx.adjustment} />
              )}
            </ZHCard>

            <ZHCard title={t("inventory.adjustments.sections.lines", "Productos")}>
              {!ctx.formLocked && (
                <div className="adj-picker-wrap">
                  <AdjustmentProductPicker
                    onSelect={(p) => void ctx.addLine(p)}
                    disabled={ctx.formLocked}
                  />
                </div>
              )}

              {ctx.lineViews.length === 0 ? (
                <EmptyState
                  message={t(
                    "inventory.adjustments.messages.emptyLines",
                    "Agregue al menos un producto al ajuste.",
                  )}
                />
              ) : (
                <div className="adj-lines">
                  {ctx.lineViews.map((view, index) => (
                    <AdjustmentLineCard
                      key={view.line._key}
                      index={index}
                      view={view}
                      movementType={ctx.movementType}
                      formLocked={ctx.formLocked}
                      onPatch={ctx.updateLine}
                      onRemove={ctx.removeLine}
                    />
                  ))}
                </div>
              )}
            </ZHCard>

            <ZHCard
              title={t("inventory.adjustments.sections.summary", "Resumen")}
              className="adj-summary"
            >
              <ZHInfoRow
                label={
                  <ZHFieldLabel size="sm">
                    {t("inventory.adjustments.summary.lines", "Total líneas")}
                  </ZHFieldLabel>
                }
                value={
                  <ZHDataValue variant="numeric">
                    {ctx.lineViews.length}
                  </ZHDataValue>
                }
              />
              <ZHInfoRow
                label={
                  <ZHFieldLabel size="sm">
                    {isExecuted
                      ? t("inventory.adjustments.summary.totalCost", "Costo total")
                      : t(
                          "inventory.adjustments.summary.estimatedCost",
                          "Costo total estimado",
                        )}
                  </ZHFieldLabel>
                }
                value={
                  <ZHDataValue variant="numeric">
                    {isExecuted
                      ? formatMoney(ctx.executedTotalCost, 2)
                      : ctx.movementType === "Ingreso"
                        ? formatMoney(ctx.estimatedTotalCost, 2)
                        : t(
                            "inventory.adjustments.summary.costResolvedOnExecute",
                            "Se calcula al ejecutar",
                          )}
                  </ZHDataValue>
                }
              />
              {ctx.insufficientStockLines.length > 0 && (
                <ZHPageNotice
                  variant="warning"
                  message={t(
                    "inventory.adjustments.messages.insufficientStockSummary",
                    "Hay líneas con stock insuficiente en la bodega seleccionada.",
                  )}
                  detail={ctx.insufficientStockLines
                    .map((v) => v.line.itemName)
                    .join(", ")}
                />
              )}
            </ZHCard>

            <div className="zh-form-actions-row zh-form-actions-row--end adj-actions">
              <ZHBtn
                variant="ghost"
                size="sm"
                type="button"
                onClick={() => ctx.navigate("/inventory/adjustments")}
              >
                {t("inventory.adjustments.actions.back", "Volver")}
              </ZHBtn>

              {canSave && (
                <ZHBtn
                  variant="primary"
                  size="sm"
                  type="button"
                  disabled={ctx.saving}
                  onClick={() => void ctx.save()}
                >
                  {ctx.saving
                    ? t("common.saving", "Guardando…")
                    : t(
                        "inventory.adjustments.actions.saveDraft",
                        "Guardar borrador",
                      )}
                </ZHBtn>
              )}

              {ctx.adjustment &&
                ctx.adjustment.status === "Draft" &&
                ctx.canExecute && (
                  <ZHBtn
                    variant="primary"
                    size="sm"
                    type="button"
                    disabled={ctx.lifecycle.busy}
                    onClick={() =>
                      ctx.lifecycle.setExecuteTarget({
                        id: ctx.adjustment!.id,
                        adjustmentNumber: ctx.adjustment!.adjustmentNumber,
                      })
                    }
                  >
                    {t("inventory.adjustments.actions.execute", "Ejecutar")}
                  </ZHBtn>
                )}

              {isExecuted && ctx.canCancel && (
                <ZHBtn
                  variant="ghost"
                  size="sm"
                  type="button"
                  disabled={ctx.lifecycle.busy}
                  onClick={() =>
                    ctx.lifecycle.setCancelTarget({
                      id: ctx.adjustment!.id,
                      adjustmentNumber: ctx.adjustment!.adjustmentNumber,
                    })
                  }
                >
                  {t("inventory.adjustments.actions.cancel", "Anular")}
                </ZHBtn>
              )}
            </div>
          </>
        )}

        <AdjustmentLifecycleModals lifecycle={ctx.lifecycle} />
      </div>
    </ErpPageTemplate>
  );
}
