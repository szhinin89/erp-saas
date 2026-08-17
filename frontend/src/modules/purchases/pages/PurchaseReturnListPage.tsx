import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { PageShell, Badge } from "../../../components/PageShell";
import { useI18n } from "../../../i18n/i18n";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhSelect } from "../../../components/zh/inputs";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { purchaseReturnService, type PurchaseReturnDto } from "../api/purchaseReturnService";
import {
  getPurchaseReturnStatusLabel,
  PURCHASE_RETURN_STATUS_BADGE as STATUS_BADGE,
} from "../utils/purchaseReturnStatus";
import "../../sales/styles/sales-return.css";

const PAGE_SIZE = 25;

/**
 * Listado de devoluciones de compra — consume exclusivamente
 * `GET /api/v1/purchases/returns`. Mismo patrón de lista que
 * `SalesReturnListPage.tsx` (PageShell + ZHCard + ZHDataTable con paginación
 * integrada). Reutiliza `sales-return.css` (clases genéricas `sr-*`, sin
 * acoplamiento a dominio de Ventas) en vez de duplicar hojas de estilo — sin
 * filtro de texto libre: `GetPurchaseReturnListQuery` solo admite `status`.
 */
export function PurchaseReturnListPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [items, setItems] = useState<PurchaseReturnDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("");
  const [loading, setLoading] = useState(false);

  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const r = await purchaseReturnService.list(status || undefined, page, PAGE_SIZE);
      setItems(r.items);
      setTotal(r.total);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, {
          generic: "No se pudo cargar el listado de devoluciones.",
        }),
      );
    } finally {
      setLoading(false);
    }
  }, [status, page]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const columns: ZHDataTableColumn<PurchaseReturnDto>[] = [
    {
      key: "returnNumber",
      header: "N.º Devolución",
      render: (row) => row.returnNumber ?? "—",
    },
    {
      key: "status",
      header: "Estado",
      render: (row) => (
        <Badge
          label={getPurchaseReturnStatusLabel(row.status, t)}
          variant={STATUS_BADGE[row.status] ?? "gray"}
        />
      ),
    },
    {
      key: "lineCount",
      header: "Líneas",
      align: "center",
      render: (row) => row.lines.length,
    },
    {
      key: "grandTotal",
      header: "Total",
      align: "right",
      render: (row) => (
        <ZHMoneyValue
          value={row.authorizedGrandTotal ?? 0}
          decimals={getDecimalConfig().totalAmount}
          currencySymbol=""
        />
      ),
    },
    {
      key: "createdAt",
      header: "Creada",
      render: (row) => formatDate(row.createdAt),
    },
    {
      key: "actions",
      header: "",
      align: "right",
      render: (row) => (
        <ZHBtn
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => navigate(`/purchases/returns/${row.id}`)}
        >
          {row.status === "Draft" ? "Editar" : "Ver"}
        </ZHBtn>
      ),
    },
  ];

  return (
    <PageShell
      title="Devoluciones de Compra"
      subtitle="Devoluciones sobre facturas de compra confirmadas"
    >
      <ZHCard
        title="Listado"
        actions={
          <ZHBtn
            variant="ghost"
            size="sm"
            type="button"
            onClick={() => void fetchList()}
            disabled={loading}
          >
            Actualizar
          </ZHBtn>
        }
      >
        <div className="sr-list-filters">
          <ZHField label="Estado" density="compact">
            <ZhSelect
              className="zh-input"
              value={status}
              onChange={(e) => {
                setPage(1);
                setStatus(e.target.value);
              }}
            >
              <option value="">Todos</option>
              <option value="Draft">Borrador</option>
              <option value="Authorized">Autorizada</option>
              <option value="Cancelled">Cancelada</option>
            </ZhSelect>
          </ZHField>
        </div>

        <ZHDataTable
          columns={columns}
          rows={items}
          rowKey={(row) => row.id}
          loading={loading}
          emptyMessage="No hay devoluciones registradas."
          page={page}
          pageSize={PAGE_SIZE}
          onPageChange={setPage}
          total={total}
        />
      </ZHCard>
    </PageShell>
  );
}
