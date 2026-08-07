import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZhTextInput, ZhSelect } from "../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { formatMoney } from "../../../lib/sanitizers";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import {
  salesReturnService,
  type SalesReturnListItemDto,
} from "../api/salesReturnService";
import {
  SALES_RETURN_STATUS_LABEL as STATUS_LABEL,
  SALES_RETURN_STATUS_BADGE as STATUS_BADGE,
} from "../utils/salesReturnStatus";
import "../styles/sales-return.css";

const PAGE_SIZE = 25;

/**
 * Listado de devoluciones de venta — consume exclusivamente
 * `GET /api/v1/sales/returns`. Mismo patrón de lista que
 * `CompanyManagementListPage`/`PurchaseReceptionPage` (PageShell + ZHCard +
 * ZHDataTable con paginación integrada), no el layout POS de `SalesPage`.
 */
export function SalesReturnListPage() {
  const navigate = useNavigate();
  const [items, setItems] = useState<SalesReturnListItemDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [loading, setLoading] = useState(false);

  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const r = await salesReturnService.list(
        search || undefined,
        status || undefined,
        page,
        PAGE_SIZE,
      );
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
  }, [search, status, page]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const columns: ZHDataTableColumn<SalesReturnListItemDto>[] = [
    {
      key: "returnNumber",
      header: "N.º Devolución",
      render: (row) => row.returnNumber,
    },
    {
      key: "customerId",
      header: "Cliente",
      render: (row) => row.customerId,
    },
    {
      key: "status",
      header: "Estado",
      render: (row) => (
        <Badge
          label={STATUS_LABEL[row.status] ?? row.status}
          variant={STATUS_BADGE[row.status] ?? "gray"}
        />
      ),
    },
    {
      key: "lineCount",
      header: "Líneas",
      align: "center",
      render: (row) => row.lineCount,
    },
    {
      key: "grandTotal",
      header: "Total",
      align: "right",
      render: (row) => formatMoney(row.grandTotal),
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
          onClick={() => navigate(`/sales/returns/${row.id}`)}
        >
          {row.status === "Draft" ? "Editar" : "Ver"}
        </ZHBtn>
      ),
    },
  ];

  return (
    <PageShell
      title="Devoluciones de Venta"
      subtitle="Notas de crédito y devoluciones sobre facturas autorizadas"
      action={
        <ZHBtn
          type="button"
          variant="primary"
          onClick={() => navigate("/sales/returns/new")}
        >
          + Nueva devolución
        </ZHBtn>
      }
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
          <ZHField label="Buscar" density="compact">
            <ZhTextInput
              value={search}
              onChange={(e) => {
                setPage(1);
                setSearch(e.target.value);
              }}
              placeholder="N.º de devolución, cliente..."
            />
          </ZHField>
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
