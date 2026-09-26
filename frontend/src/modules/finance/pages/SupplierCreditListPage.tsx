import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Badge, PageShell, PageToolbar, TableCard } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import {
  supplierCreditService,
  type SupplierCreditListItemDto,
} from "../api/supplierCreditService";
import { SupplierCreditFilters } from "../components/SupplierCreditFilters";
import {
  DEFAULT_SUPPLIER_CREDIT_FILTERS,
  toSupplierCreditListFilters,
  type SupplierCreditFiltersValue,
} from "../utils/supplierCreditFilters";
import { supplierCreditSourceLabel, supplierCreditSourceRoute } from "../utils/supplierCreditOrigin";

const PAGE_SIZE = 25;

/**
 * ZH-SUPPLIER-BALANCES-UX-02D-E — "Saldos a favor de proveedores": dinero que el proveedor mantiene a
 * favor de la empresa (anticipos / pagos mayores y devoluciones de compra). Una sola responsabilidad:
 * consultar y abrir el saldo para gestionarlo. Filtros server-side (02D-D, default: abiertos).
 * `AvailableAmount` es siempre el valor del servidor — nunca se recalcula en el cliente.
 */
export function SupplierCreditListPage() {
  const navigate = useNavigate();
  const [items, setItems] = useState<SupplierCreditListItemDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<SupplierCreditFiltersValue>(DEFAULT_SUPPLIER_CREDIT_FILTERS);
  const [loading, setLoading] = useState(false);

  const handleFiltersChange = (patch: Partial<SupplierCreditFiltersValue>) => {
    setFilters((current) => ({ ...current, ...patch }));
    setPage(1);
  };

  const handleReset = () => {
    setFilters(DEFAULT_SUPPLIER_CREDIT_FILTERS);
    setPage(1);
  };

  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const r = await supplierCreditService.list(page, PAGE_SIZE, toSupplierCreditListFilters(filters));
      setItems(r.items);
      setTotal(r.total);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, {
          generic: "No se pudo cargar el listado de saldos a favor de proveedores.",
        }),
      );
    } finally {
      setLoading(false);
    }
  }, [page, filters]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const columns = useMemo<ZHDataTableColumn<SupplierCreditListItemDto>[]>(
    () => [
      {
        key: "supplier",
        header: "Proveedor",
        render: (row) => <strong>{row.supplierName ?? "—"}</strong>,
      },
      {
        key: "origin",
        header: "Origen",
        render: (row) => supplierCreditSourceLabel(row.sourceType),
      },
      {
        key: "document",
        header: "Documento",
        render: (row) => {
          const route = supplierCreditSourceRoute(row);
          const label = row.sourceDocumentNumber ?? "—";
          return route && row.sourceDocumentNumber ? (
            <Link to={route} className="zh-link">
              {label}
            </Link>
          ) : (
            label
          );
        },
      },
      { key: "date", header: "Fecha", render: (row) => formatDate(row.sourceDate) },
      {
        key: "originalAmount",
        header: "Monto original",
        align: "right",
        render: (row) => <ZHMoneyValue value={row.originalAmount} precision="money" />,
      },
      {
        key: "availableAmount",
        header: "Saldo disponible",
        align: "right",
        render: (row) => <ZHMoneyValue value={row.availableAmount} precision="money" emphasis="strong" />,
      },
      {
        key: "status",
        header: "Estado",
        render: (row) => (
          <Badge label={row.isOpen ? "Abierto" : "Cerrado"} variant={row.isOpen ? "green" : "gray"} />
        ),
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
            onClick={() => navigate(`/suppliers/credits/${row.id}`)}
          >
            Ver
          </ZHBtn>
        ),
      },
    ],
    [navigate],
  );

  return (
    <PageShell
      kicker="Cuentas por pagar"
      title="Saldos a favor de proveedores"
      subtitle="Dinero que los proveedores mantienen a favor de la empresa: anticipos, pagos mayores y devoluciones de compra."
    >
      <TableCard>
        <PageToolbar>
          <SupplierCreditFilters value={filters} onChange={handleFiltersChange} onReset={handleReset} />
        </PageToolbar>
        <ZHDataTable
          columns={columns}
          rows={items}
          rowKey={(row) => row.id}
          loading={loading}
          showRowNumber
          rowNumberOffset={(page - 1) * PAGE_SIZE}
          emptyMessage="No hay saldos a favor de proveedores para estos filtros."
          page={page}
          pageSize={PAGE_SIZE}
          onPageChange={setPage}
          total={total}
        />
      </TableCard>
    </PageShell>
  );
}
