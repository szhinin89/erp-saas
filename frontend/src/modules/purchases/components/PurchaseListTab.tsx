import { useNavigate } from "react-router-dom";
import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import type { PurchaseListItemDto } from "../api/purchaseService";
import { useI18n } from "../../../i18n/i18n";
import { purchaseStatusBadge } from "../utils/purchaseDocumentState";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";

type PurchasesCtx = ReturnType<typeof usePurchasesPage>;

export function PurchaseListTab({ ctx }: { ctx: PurchasesCtx }) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const invoiceActionLabel = (inv: PurchaseListItemDto) =>
    t(inv.status === "Draft" ? "purchases.list.editInvoiceAria" : "purchases.list.viewInvoiceAria", { number: inv.invoiceNumber });
  const purchaseListColumns: ZHDataTableColumn<PurchaseListItemDto>[] = [
    {
      key: "invoiceNumber",
      header: t("purchases.list.invoiceNumber", "Nro. factura"),
      render: (inv) => <span className="pf-invoice-number zh-code-value">{inv.invoiceNumber}</span>,
    },
    // FASE 5 PURCHASE-UX — proveedor (snapshot de la compra) y total del documento.
    {
      key: "supplier",
      header: t("purchases.list.supplier", "Proveedor"),
      render: (inv) => (
        <span className="pf-list-supplier">
          <span className="pf-list-supplier__name">{inv.supplierName}</span>
          {inv.supplierTaxId && (
            <ZHDataValue variant="code">{inv.supplierTaxId}</ZHDataValue>
          )}
        </span>
      ),
    },
    { key: "date", header: t("purchases.list.date", "Fecha"), render: (inv) => formatDate(inv.issueDate) },
    { key: "lines", header: t("purchases.list.lines", "Líneas"), align: "center", render: (inv) => inv.lineCount },
    {
      key: "total",
      header: t("purchases.list.total", "Total"),
      align: "right",
      render: (inv) => <ZHMoneyValue value={inv.grandTotal} precision="money" />,
    },
    {
      key: "status",
      header: t("purchases.list.status", "Estado"),
      // El estado persistido distingue Discarded de Cancelled.
      render: (inv) => {
        const badge = purchaseStatusBadge(inv.status);
        return <Badge variant={badge.variant} label={t(badge.key, badge.fallback)} />;
      },
    },
    {
      key: "actions",
      header: t("purchases.list.actions", "Acciones"),
      align: "center",
      render: (inv) => (
        <>
          <ZHIconButton
            icon={inv.status === "Draft" ? "edit" : "visibility"}
            variant="ghost"
            title={invoiceActionLabel(inv)}
            ariaLabel={invoiceActionLabel(inv)}
            onClick={() => void ctx.loadForEdit(inv.id)}
          />
          {inv.status === "Confirmed" && (
            <ZHIconButton
              icon="history"
              variant="ghost"
              title={t("purchases.actions.viewInventoryMovement", "Ver movimiento de inventario")}
              ariaLabel={t("purchases.list.viewMovementInvoiceAria", { number: inv.invoiceNumber })}
              onClick={() => navigate(`/inventory/kardex?docId=${inv.id}&docType=PurchaseInvoice`)}
            />
          )}
        </>
      ),
    },
  ];
  return (
        <div className="prd-section">
          <div className="pg-table-controls zh-mb-16">
            <ZhTextInput
              placeholder={t(
                "purchases.list.searchPlaceholder",
                "Buscar por número, proveedor o RUC...",
              )}
              value={ctx.listSearchInput}
              onChange={(e) => ctx.setListSearchInput(e.target.value)}
            />
            <ZhSelect
              value={ctx.listStatus}
              onChange={(e) => ctx.setListStatus(e.target.value)}
            >
              <option value="">{t("purchases.list.allStatuses", "Todos los estados")}</option>
              <option value="Draft">{t("purchases.status.draft", "Borrador")}</option>
              <option value="Confirmed">{t("purchases.status.confirmed", "Confirmada")}</option>
              <option value="Cancelled">
                {t("purchases.status.cancelled", "Anulada")}
              </option>
              <option value="Discarded">{t("purchases.status.discarded", "Descartado")}</option>
            </ZhSelect>
            <ZHBtn
              variant="secondary"
              onClick={ctx.fetchList}
              disabled={ctx.listLoading}
            >
              <span
                className="material-symbols-outlined zh-icon-lg"
              >
                refresh
              </span>
            </ZHBtn>
          </div>
          <ZHDataTable
            columns={purchaseListColumns}
            rows={ctx.listItems}
            rowKey={(inv) => inv.id}
            loading={ctx.listLoading}
            showRowNumber
            rowNumberOffset={(ctx.listPage - 1) * ctx.listPageSize}
            tableClassName="table--compact table--neutral"
            emptyMessage={t("purchases.list.empty", "Sin compras registradas.")}
          />
          {!ctx.listLoading && ctx.listTotal > ctx.listPageSize && (
            <div className="pf-pagination">
              <span className="pf-pagination__summary">
                {t("purchases.pagination.page", "Página")} {ctx.listPage} {t("purchases.pagination.of", "de")}{" "}
                {Math.max(1, Math.ceil(ctx.listTotal / ctx.listPageSize))} —{" "}
                {t("purchases.pagination.purchaseCount", {
                  count: ctx.listTotal,
                })}
              </span>
              <div className="pf-pagination__actions">
                <ZHBtn
                  type="button"
                  variant="secondary"
                  disabled={ctx.listPage <= 1}
                  onClick={() => ctx.setListPage((p) => p - 1)}
                >
                  {t("common.previous", "Anterior")}
                </ZHBtn>
                <ZHBtn
                  type="button"
                  variant="secondary"
                  disabled={
                    ctx.listPage >= Math.ceil(ctx.listTotal / ctx.listPageSize)
                  }
                  onClick={() => ctx.setListPage((p) => p + 1)}
                >
                  {t("common.next", "Siguiente")}
                </ZHBtn>
              </div>
            </div>
          )}
        </div>

  );
}
