import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  EmptyState,
  NoAccessPage,
  PageShell,
  PageToolbar,
  TableCard,
} from "../../../components/PageShell";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { payablesService, type PayableListItemDto } from "../api/payablesService";
import { PayableOriginBadge } from "../components/PayableOriginBadge";
import { PayableStatusBadge } from "../components/PayableStatusBadge";
import { PayablesFilters, type PayablesFiltersValue } from "../components/PayablesFilters";
import "../styles/payables.css";

const PERMISSIONS = { view: "payables.view" } as const;

const EMPTY_FILTERS: PayablesFiltersValue = {
  search: "",
  originType: "",
  status: "",
  dueDateFrom: "",
  dueDateTo: "",
  supplierId: null,
};

export function PayablesPage() {
  const { has } = usePermissionsUi();
  const canView = has(PERMISSIONS.view);
  const navigate = useNavigate();
  const decimals = getDecimalConfig().totalAmount;

  const [rows, setRows] = useState<PayableListItemDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<PayablesFiltersValue>(EMPTY_FILTERS);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleFiltersChange = (patch: Partial<PayablesFiltersValue>) => {
    setFilters((current) => ({ ...current, ...patch }));
    setPage(1);
  };

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await payablesService.list(filters, page, 25);
      setRows(response.items);
      setTotal(response.total);
    } catch (err) {
      const msg = formatApiRequestError(err, {
        generic: "No se pudo cargar las cuentas por pagar.",
      });
      setError(msg);
      message.error(msg);
    } finally {
      setLoading(false);
    }
  }, [filters, page]);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  const columns = useMemo<ZHDataTableColumn<PayableListItemDto>[]>(
    () => [
      {
        key: "supplier",
        header: "Proveedor",
        render: (row) => (
          <div className="pay-list-cell">
            <strong>{row.supplierName || "—"}</strong>
          </div>
        ),
      },
      {
        key: "origin",
        header: "Origen",
        render: (row) => <PayableOriginBadge originType={row.originType} />,
      },
      {
        key: "document",
        header: "Documento",
        render: (row) => (
          <div className="pay-list-cell">
            <strong>{row.documentNumber}</strong>
            <span>{row.documentType}</span>
          </div>
        ),
      },
      { key: "issueDate", header: "Emision", render: (row) => formatDate(row.issueDate) },
      { key: "dueDate", header: "Vence", render: (row) => formatDate(row.dueDate) },
      {
        key: "total",
        header: "Total",
        align: "right",
        render: (row) => <ZHMoneyValue value={row.totalAmount} decimals={decimals} />,
      },
      {
        key: "paid",
        header: "Pagado",
        align: "right",
        render: (row) => <ZHMoneyValue value={row.paidAmount} decimals={decimals} />,
      },
      {
        key: "outstanding",
        header: "Saldo",
        align: "right",
        render: (row) => <ZHMoneyValue value={row.outstandingAmount} decimals={decimals} />,
      },
      {
        key: "status",
        header: "Estado",
        render: (row) => <PayableStatusBadge status={row.status} />,
      },
    ],
    [decimals],
  );

  if (!canView) return <NoAccessPage title="Cuentas por pagar" />;

  return (
    <PageShell
      kicker="Finanzas"
      title="Cuentas por pagar"
      subtitle="Consulta de la deuda viva con proveedores, generada desde Compras y Gastos."
    >
      <TableCard>
        <PageToolbar>
          <PayablesFilters value={filters} onChange={handleFiltersChange} />
        </PageToolbar>

        {error ? (
          <EmptyState message={error} />
        ) : (
          <ZHDataTable
            rows={rows}
            columns={columns}
            rowKey={(row) => row.id}
            loading={loading}
            emptyMessage="No hay cuentas por pagar registradas para estos filtros."
            page={page}
            pageSize={25}
            total={total}
            onPageChange={setPage}
            onRowClick={(row) => navigate(`/payables/${row.id}`)}
          />
        )}
      </TableCard>
    </PageShell>
  );
}

export default PayablesPage;
