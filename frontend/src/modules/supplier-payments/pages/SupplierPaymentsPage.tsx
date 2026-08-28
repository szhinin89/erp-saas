import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { EmptyState, NoAccessPage, PageShell, TableCard } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import {
  supplierPaymentService,
  type SupplierPaymentListItemDto,
} from "../api/supplierPaymentService";
import { SupplierPaymentStatusBadge } from "../components/SupplierPaymentStatusBadge";
import "../styles/supplier-payments.css";

const PERMISSIONS = { view: "supplier-payments.view", create: "supplier-payments.create" } as const;

export function SupplierPaymentsPage() {
  const { has } = usePermissionsUi();
  const canView = has(PERMISSIONS.view);
  const canCreate = has(PERMISSIONS.create);
  const navigate = useNavigate();
  const decimals = getDecimalConfig().totalAmount;

  const [rows, setRows] = useState<SupplierPaymentListItemDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await supplierPaymentService.list({}, page, 25);
      setRows(response.items);
      setTotal(response.total);
    } catch (err) {
      const msg = formatApiRequestError(err, {
        generic: "No se pudo cargar los pagos a proveedores.",
      });
      setError(msg);
      message.error(msg);
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  const columns = useMemo<ZHDataTableColumn<SupplierPaymentListItemDto>[]>(
    () => [
      {
        key: "number",
        header: "Número",
        render: (row) => (
          <div className="pay-list-cell">
            <strong>{row.displayNumber}</strong>
            {row.receiptNumber && <span>Sistema: {row.systemNumber}</span>}
          </div>
        ),
      },
      {
        key: "supplier",
        header: "Proveedor",
        render: (row) => row.supplierName || "—",
      },
      { key: "date", header: "Fecha", render: (row) => formatDate(row.paymentDate) },
      {
        key: "total",
        header: "Total",
        align: "right",
        render: (row) => <ZHMoneyValue value={row.totalAmount} decimals={decimals} />,
      },
      {
        key: "status",
        header: "Estado",
        render: (row) => <SupplierPaymentStatusBadge status={row.status} />,
      },
    ],
    [decimals],
  );

  if (!canView) return <NoAccessPage title="Pagos a proveedores" />;

  return (
    <PageShell
      kicker="Finanzas"
      title="Pagos a proveedores"
      subtitle="Registro de pagos confirmados contra Cuentas por Pagar — sin borrador, sin edición posterior."
      action={
        canCreate ? (
          <ZHBtn type="button" variant="primary" onClick={() => navigate("/supplier-payments/new")}>
            + Registrar pago
          </ZHBtn>
        ) : undefined
      }
    >
      <TableCard>
        {error ? (
          <EmptyState message={error} />
        ) : (
          <ZHDataTable
            rows={rows}
            columns={columns}
            rowKey={(row) => row.id}
            loading={loading}
            emptyMessage="No hay pagos a proveedores registrados."
            page={page}
            pageSize={25}
            total={total}
            onPageChange={setPage}
            onRowClick={(row) => navigate(`/supplier-payments/${row.id}`)}
          />
        )}
      </TableCard>
    </PageShell>
  );
}

export default SupplierPaymentsPage;
