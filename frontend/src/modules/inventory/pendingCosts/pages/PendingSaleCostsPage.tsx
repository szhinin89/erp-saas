import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useI18n } from "../../../../i18n/i18n";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { Badge, EmptyState, LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { formatDate } from "../../../../lib/formatters/dateFormatters";
import { formatDecimalDisplay, formatMoney } from "../../../../lib/sanitizers";
import { usePrecisionDecimals } from "../../../../hooks/usePrecisionPolicy";
import { readApiErrorMessage } from "../../../lib/apiError";
import { stockService, type PendingSaleCostDto } from "../../stock/api/stockService";

/**
 * ACCOUNTING-12B — Inventario → Ventas con costo pendiente (`SALE_COST_PENDING`).
 *
 * Auditoría de reutilización: revisadas `StockAdjustmentsPage` (ErpPageTemplate + ZHCard +
 * ZHDataTable + Badge + EmptyState/LoadingState) y `KardexPage`. Permiso propio
 * `inventory.pending-costs.view` (`inventory.stock.view` es Kardex; el menú identifica ítems por permiso).
 * Sin componentes ni CSS nuevos. Solo lectura y sin acción "Contabilizar": la cobertura del costo
 * es automática en backend al registrar una compra, un ajuste positivo con costo o la carga inicial;
 * los enlaces llevan a esas pantallas, nunca resuelven aquí.
 */
export function PendingSaleCostsPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("inventory.pending-costs.view");
  const quantityDecimals = usePrecisionDecimals("quantity");
  const unitCostDecimals = usePrecisionDecimals("unitCost");
  const [rows, setRows] = useState<PendingSaleCostDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!canView) return;
    let alive = true;
    stockService
      .getPendingCosts()
      .then((data) => alive && setRows(data))
      .catch((err) => alive && setError(readApiErrorMessage(err) ?? String(err)))
      .finally(() => alive && setLoading(false));
    return () => {
      alive = false;
    };
  }, [canView]);

  if (!canView) {
    return <NoAccessPage title={t("inventory.pendingCosts.title", "Ventas con costo pendiente")} />;
  }

  const columns: ZHDataTableColumn<PendingSaleCostDto>[] = [
    {
      key: "document",
      header: t("inventory.pendingCosts.table.document", "Documento"),
      render: (row) => <Badge label={row.invoiceNumber ?? "—"} variant="neutral" size="md" code />,
    },
    {
      key: "date",
      header: t("inventory.pendingCosts.table.date", "Fecha"),
      render: (row) => (row.issueDate ? formatDate(row.issueDate) : "—"),
    },
    {
      key: "item",
      header: t("inventory.pendingCosts.table.item", "Ítem"),
      render: (row) => (
        <>
          <span className="mono">{row.itemSku ?? "—"}</span> {row.itemName ?? ""}
        </>
      ),
    },
    {
      key: "warehouse",
      header: t("inventory.pendingCosts.table.warehouse", "Bodega"),
      render: (row) => row.warehouseName ?? "—",
    },
    {
      key: "quantity",
      header: t("inventory.pendingCosts.table.quantity", "Cantidad"),
      align: "right",
      render: (row) => (
        <span className="mono">{formatDecimalDisplay(row.pendingQuantity, quantityDecimals)}</span>
      ),
    },
    {
      key: "status",
      header: t("inventory.pendingCosts.table.status", "Estado / costo pendiente"),
      render: (row) =>
        row.provisionalUnitCost != null ? (
          <Badge
            label={t("inventory.pendingCosts.status.provisional", {
              cost: formatMoney(row.provisionalUnitCost, unitCostDecimals),
            })}
            variant="warning"
            size="md"
          />
        ) : (
          <Badge label={t("inventory.pendingCosts.status.pending", "Sin costo")} variant="error" size="md" />
        ),
    },
    {
      key: "actions",
      header: t("inventory.pendingCosts.table.actions", "Regularizar"),
      align: "right",
      render: (row) => (
        <div className="zh-form-actions-row zh-form-actions-row--end">
          {canShow("purchases.create") && (
            <Link to="/purchases">{t("inventory.pendingCosts.actions.purchase", "Registrar compra")}</Link>
          )}
          {canShow("inventory.adjustments.create") && (
            <Link to="/inventory/adjustments/new">
              {t("inventory.pendingCosts.actions.adjustment", "Ajuste positivo con costo")}
            </Link>
          )}
          <Link to={`/inventory/kardex?productId=${row.productId}`}>
            {t("inventory.pendingCosts.actions.kardex", "Kardex")}
          </Link>
        </div>
      ),
    },
  ];

  return (
    <ErpPageTemplate
      kicker={t("inventory.adjustments.kicker", "Inventario")}
      title={t("inventory.pendingCosts.title", "Ventas con costo pendiente")}
      subtitle={t(
        "inventory.pendingCosts.description",
        "Ventas de productos que aún no tenían un costo válido en la bodega.",
      )}
    >
      {error && <ZHPageNotice variant="error" message={t("common.errorPrefix", "Error:")} detail={error} />}
      <ZHPageNotice
        variant="info"
        message={t(
          "inventory.pendingCosts.notice",
          "Esta venta está pendiente de costo. Se regularizará automáticamente cuando el producto reciba un costo válido.",
        )}
        detail={t(
          "inventory.pendingCosts.noticeDetail",
          "Registre la compra, un ajuste positivo con costo o la carga inicial del producto; no se requiere contabilizar manualmente.",
        )}
      />
      <ZHCard title={t("inventory.pendingCosts.sections.list", "Pendientes")}>
        {loading ? (
          <LoadingState />
        ) : rows.length === 0 ? (
          <EmptyState
            message={t(
              "inventory.pendingCosts.empty",
              "No hay ventas con costo pendiente. Todas las ventas tienen costo resuelto.",
            )}
          />
        ) : (
          <ZHDataTable columns={columns} rows={rows} rowKey={(row) => row.obligationId} />
        )}
      </ZHCard>
    </ErpPageTemplate>
  );
}
