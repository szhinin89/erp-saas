import { useMemo } from "react";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import type { PayableInstallmentDto } from "../api/payablesService";
import { PayableStatusBadge } from "./PayableStatusBadge";

/** Tabla de solo lectura de cuotas — sin paginación (lista fija ya cargada con el detalle). */
export function PayableInstallmentsTable({
  installments,
}: {
  installments: PayableInstallmentDto[];
}) {
  const decimals = getDecimalConfig().totalAmount;

  const columns = useMemo<ZHDataTableColumn<PayableInstallmentDto>[]>(
    () => [
      { key: "number", header: "Cuota", align: "right", render: (row) => row.installmentNumber },
      { key: "dueDate", header: "Vence", render: (row) => formatDate(row.dueDate) },
      {
        key: "amount",
        header: "Valor",
        align: "right",
        render: (row) => <ZHMoneyValue value={row.amount} decimals={decimals} />,
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

  return (
    <ZHDataTable
      rows={installments}
      columns={columns}
      rowKey={(row) => row.installmentId}
      emptyMessage="Esta cuenta por pagar no tiene cuotas registradas."
    />
  );
}
