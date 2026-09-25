import { useMemo } from "react";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import type { PayableInstallmentDto } from "../api/payablesService";
import { PayableStatusBadge } from "./PayableStatusBadge";

/** Tabla de solo lectura de cuotas — sin paginación (lista fija ya cargada con el detalle). */
export function PayableInstallmentsTable({
  installments,
}: {
  installments: PayableInstallmentDto[];
}) {

  const columns = useMemo<ZHDataTableColumn<PayableInstallmentDto>[]>(
    () => [
      { key: "number", header: "Cuota", align: "right", render: (row) => row.installmentNumber },
      { key: "dueDate", header: "Vence", render: (row) => formatDate(row.dueDate) },
      {
        key: "amount",
        header: "Valor",
        align: "right",
        render: (row) => <ZHMoneyValue value={row.amount} precision="money" />,
      },
      {
        key: "paid",
        header: "Pagado",
        align: "right",
        render: (row) => <ZHMoneyValue value={row.paidAmount} precision="money" />,
      },
      {
        key: "outstanding",
        header: "Saldo",
        align: "right",
        render: (row) => <ZHMoneyValue value={row.outstandingAmount} precision="money" />,
      },
      {
        key: "status",
        header: "Estado",
        render: (row) => <PayableStatusBadge status={row.status} />,
      },
    ],
    [],
  );

  return (
    <ZHDataTable
      rows={installments}
      columns={columns}
      rowKey={(row) => row.installmentId}
      emptyMessage="Esta cuenta por pagar no tiene cuotas registradas."
    />
  );
}
