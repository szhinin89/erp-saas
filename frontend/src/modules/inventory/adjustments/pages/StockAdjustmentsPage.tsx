import { useNavigate } from "react-router-dom";
import { useI18n } from "../../../../i18n/i18n";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../../components/zh/inputs/ZhSelect";
import { ZhDateInput } from "../../../../components/zh/inputs/ZhDateInput";
import {
  Badge,
  EmptyState,
  LoadingState,
  NoAccessPage,
} from "../../../../components/PageShell";
import { formatDate } from "../../../../lib/formatters/dateFormatters";
import { formatMoney } from "../../../../lib/sanitizers";
import { useStockAdjustmentsPage } from "../hooks/useStockAdjustmentsPage";
import { AdjustmentLifecycleModals } from "../components/AdjustmentLifecycleModals";
import {
  adjustmentStatusBadge,
  movementTypeBadge,
} from "../utils/adjustmentStatusBadge";
import type { StockAdjustmentDto } from "../types";
import "./StockAdjustmentsPage.css";

/**
 * INVENTORY-ADJUSTMENTS-03 — Pantalla 1: lista de ajustes de inventario.
 *
 * Documento distinto de una Transferencia entre bodegas (ver el doc comment de
 * `StockTransferPage`): un ajuste corrige el saldo de UNA bodega contra un motivo administrable,
 * no mueve stock entre dos. Se reutiliza el mismo vocabulario visual (ErpPageTemplate, ZHCard,
 * ZHGrid, Badge, tabla `.table`) sin compartir código de dominio.
 *
 * "Costo total" solo tiene significado una vez Ejecutado: `TotalCost` lo resuelve el backend al
 * ejecutar (Egreso desde el costo promedio móvil), así que en Borrador se muestra "—" en vez de
 * un 0 que el usuario leería como "costo cero".
 */
export function StockAdjustmentsPage() {
  const { t } = useI18n();
  const ctx = useStockAdjustmentsPage();
  const navigate = useNavigate();

  if (!ctx.canView) {
    return (
      <NoAccessPage
        title={t("inventory.adjustments.title", "Ajustes de inventario")}
      />
    );
  }

  const totalCost = (row: StockAdjustmentDto) =>
    row.lines.reduce((sum, l) => sum + (l.totalCost ?? 0), 0);

  return (
    <ErpPageTemplate
      kicker={t("inventory.adjustments.kicker", "Inventario")}
      title={t("inventory.adjustments.title", "Ajustes de inventario")}
      subtitle={t(
        "inventory.adjustments.description",
        "Corrige el saldo de una bodega contra un motivo de ajuste, con trazabilidad en Kardex.",
      )}
      action={
        ctx.canCreate ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            onClick={() => navigate("/inventory/adjustments/new")}
          >
            <span className="material-symbols-outlined">add</span>
            {t("inventory.adjustments.actions.new", "Nuevo ajuste")}
          </ZHBtn>
        ) : null
      }
    >
      {ctx.error && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix", "Error:")}
          detail={ctx.error}
        />
      )}
      {ctx.lifecycle.actionError && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix", "Error:")}
          detail={ctx.lifecycle.actionError}
        />
      )}

      <ZHCard title={t("inventory.adjustments.sections.filters", "Filtros")}>
        <div className="adj-filters">
          <ZHField label={t("inventory.adjustments.filters.startDate", "Desde")}>
            <ZhDateInput
              value={ctx.filters.startDate}
              onChange={(e) => ctx.setFilter("startDate", e.target.value)}
            />
          </ZHField>
          <ZHField label={t("inventory.adjustments.filters.endDate", "Hasta")}>
            <ZhDateInput
              value={ctx.filters.endDate}
              onChange={(e) => ctx.setFilter("endDate", e.target.value)}
            />
          </ZHField>
          <ZHField
            label={t("inventory.adjustments.filters.movementType", "Tipo")}
          >
            <ZhSelect
              value={ctx.filters.movementType}
              onChange={(e) => ctx.setFilter("movementType", e.target.value)}
              aria-label={t("inventory.adjustments.filters.movementType", "Tipo")}
            >
              <option value="">{t("common.all", "Todos")}</option>
              <option value="Ingreso">
                {t("inventory.adjustments.movementType.ingreso", "Ingreso")}
              </option>
              <option value="Egreso">
                {t("inventory.adjustments.movementType.egreso", "Egreso")}
              </option>
            </ZhSelect>
          </ZHField>
          <ZHField label={t("inventory.adjustments.filters.status", "Estado")}>
            <ZhSelect
              value={ctx.filters.status}
              onChange={(e) => ctx.setFilter("status", e.target.value)}
              aria-label={t("inventory.adjustments.filters.status", "Estado")}
            >
              <option value="">{t("common.all", "Todos")}</option>
              <option value="Draft">
                {t("inventory.adjustments.status.draft", "Borrador")}
              </option>
              <option value="Executed">
                {t("inventory.adjustments.status.executed", "Ejecutado")}
              </option>
              <option value="Cancelled">
                {t("inventory.adjustments.status.cancelled", "Anulado")}
              </option>
            </ZhSelect>
          </ZHField>
          <ZHField
            label={t("inventory.adjustments.filters.warehouse", "Bodega")}
          >
            <ZhSelect
              value={ctx.filters.warehouseId}
              onChange={(e) => ctx.setFilter("warehouseId", e.target.value)}
              aria-label={t("inventory.adjustments.filters.warehouse", "Bodega")}
            >
              <option value="">{t("common.all", "Todas")}</option>
              {ctx.warehouses.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label={t("inventory.adjustments.filters.reason", "Motivo")}>
            <ZhSelect
              value={ctx.filters.reasonId}
              onChange={(e) => ctx.setFilter("reasonId", e.target.value)}
              aria-label={t("inventory.adjustments.filters.reason", "Motivo")}
            >
              <option value="">{t("common.all", "Todos")}</option>
              {ctx.reasons.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
        </div>
        {ctx.hasFilters && (
          <div className="adj-filters__actions">
            <ZHBtn
              variant="ghost"
              size="sm"
              type="button"
              onClick={ctx.clearFilters}
            >
              {t("inventory.adjustments.actions.clearFilters", "Limpiar filtros")}
            </ZHBtn>
          </div>
        )}
      </ZHCard>

      <ZHCard
        title={t("inventory.adjustments.sections.list", "Ajustes registrados")}
      >
        {ctx.loading ? (
          <LoadingState />
        ) : ctx.rows.length === 0 ? (
          <EmptyState
            message={t(
              "inventory.adjustments.messages.empty",
              "No hay ajustes de inventario para los filtros seleccionados.",
            )}
          />
        ) : (
          <>
            <div className="table-scroll">
              <table className="table">
                <thead>
                  <tr>
                    <th>{t("inventory.adjustments.table.number", "N.º")}</th>
                    <th>{t("inventory.adjustments.table.date", "Fecha")}</th>
                    <th>{t("inventory.adjustments.table.movementType", "Tipo")}</th>
                    <th>{t("inventory.adjustments.table.warehouse", "Bodega")}</th>
                    <th>{t("inventory.adjustments.table.reason", "Motivo")}</th>
                    <th>{t("inventory.adjustments.table.status", "Estado")}</th>
                    <th>{t("inventory.adjustments.table.lines", "Total líneas")}</th>
                    <th>{t("inventory.adjustments.table.totalCost", "Costo total")}</th>
                    <th className="pg-th-right">
                      {t("inventory.adjustments.table.actions", "Acciones")}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {ctx.rows.map((row) => {
                    const status = adjustmentStatusBadge(row.status, t);
                    const movement = movementTypeBadge(row.movementType, t);
                    const isDraft = row.status === "Draft";
                    const isExecuted = row.status === "Executed";
                    return (
                      <tr key={row.id}>
                        <td>
                          <Badge
                            label={row.adjustmentNumber}
                            variant="neutral"
                            size="md"
                            code
                          />
                        </td>
                        <td>{formatDate(row.adjustmentDate)}</td>
                        <td>
                          <Badge
                            label={movement.label}
                            variant={movement.variant}
                            size="md"
                          />
                        </td>
                        <td>{row.warehouseName}</td>
                        <td>{row.reasonName ?? "—"}</td>
                        <td>
                          <Badge
                            label={status.label}
                            variant={status.variant}
                            size="md"
                          />
                        </td>
                        <td className="mono">{row.lines.length}</td>
                        <td className="mono">
                          {isExecuted ? formatMoney(totalCost(row), 2) : "—"}
                        </td>
                        <td className="pg-th-right">
                          <div className="adj-row-actions">
                            <ZHBtn
                              variant="ghost"
                              size="sm"
                              type="button"
                              onClick={() =>
                                navigate(`/inventory/adjustments/${row.id}`)
                              }
                            >
                              {t("common.view", "Ver")}
                            </ZHBtn>
                            {isDraft && ctx.canUpdate && (
                              <ZHBtn
                                variant="ghost"
                                size="sm"
                                type="button"
                                onClick={() =>
                                  navigate(`/inventory/adjustments/${row.id}`)
                                }
                              >
                                {t("common.edit", "Editar")}
                              </ZHBtn>
                            )}
                            {isDraft && ctx.canExecute && (
                              <ZHBtn
                                variant="primary"
                                size="sm"
                                type="button"
                                disabled={ctx.lifecycle.busy}
                                onClick={() =>
                                  ctx.lifecycle.setExecuteTarget({
                                    id: row.id,
                                    adjustmentNumber: row.adjustmentNumber,
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
                                    id: row.id,
                                    adjustmentNumber: row.adjustmentNumber,
                                  })
                                }
                              >
                                {t("inventory.adjustments.actions.cancel", "Anular")}
                              </ZHBtn>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            <div className="adj-pager">
              <span className="subtle">
                {t("inventory.adjustments.table.page", "Página")}{" "}
                {ctx.pageNumber} / {ctx.totalPages} — {ctx.totalCount}{" "}
                {t("inventory.adjustments.table.results", "ajustes")}
              </span>
              <ZHBtn
                variant="ghost"
                size="sm"
                type="button"
                disabled={ctx.pageNumber <= 1}
                onClick={() => ctx.setPageNumber(ctx.pageNumber - 1)}
              >
                {t("common.previous", "Anterior")}
              </ZHBtn>
              <ZHBtn
                variant="ghost"
                size="sm"
                type="button"
                disabled={ctx.pageNumber >= ctx.totalPages}
                onClick={() => ctx.setPageNumber(ctx.pageNumber + 1)}
              >
                {t("common.next", "Siguiente")}
              </ZHBtn>
            </div>
          </>
        )}
      </ZHCard>

      <AdjustmentLifecycleModals lifecycle={ctx.lifecycle} />
    </ErpPageTemplate>
  );
}
