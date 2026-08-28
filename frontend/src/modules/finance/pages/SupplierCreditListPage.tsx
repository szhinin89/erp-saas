import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { formatMoney } from "../../../lib/sanitizers";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { supplierCreditService, type SupplierCreditDto } from "../api/supplierCreditService";

const PAGE_SIZE = 25;

/**
 * Listado de créditos de proveedor — consume exclusivamente
 * `GET /api/v1/finance/supplier-credits`. Mismo patrón de lista que
 * `PurchaseReturnListPage.tsx` (P0-02 Fase 12 / P0-03).
 * `AvailableAmount` mostrado es siempre el valor cacheado del servidor (§4.2 del diseño).
 */
export function SupplierCreditListPage() {
  const navigate = useNavigate();
  const [items, setItems] = useState<SupplierCreditDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);

  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const r = await supplierCreditService.list(page, PAGE_SIZE);
      setItems(r.items);
      setTotal(r.total);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, {
          generic: "No se pudo cargar el listado de créditos de proveedor.",
        }),
      );
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const columns: ZHDataTableColumn<SupplierCreditDto>[] = [
    {
      key: "supplierId",
      header: "Proveedor",
      render: (row) => row.supplierId,
    },
    {
      key: "currencyCode",
      header: "Moneda",
      render: (row) => row.currencyCode,
    },
    {
      key: "originalAmount",
      header: "Monto original",
      align: "right",
      render: (row) => formatMoney(row.originalAmount),
    },
    {
      key: "availableAmount",
      header: "Saldo disponible",
      align: "right",
      render: (row) => <strong>{formatMoney(row.availableAmount)}</strong>,
    },
    {
      key: "isOpen",
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
          onClick={() => navigate(`/finance/supplier-credits/${row.id}`)}
        >
          Ver
        </ZHBtn>
      ),
    },
  ];

  return (
    <PageShell
      title="Créditos de Proveedor"
      subtitle="Saldo disponible originado por devoluciones de compra — aplicación y reembolso"
      action={
        <ZHBtn
          type="button"
          variant="ghost"
          onClick={() => navigate("/settings/financial-destinations")}
        >
          Destinos financieros
        </ZHBtn>
      }
    >
      <ZHCard
        title="Listado"
        actions={
          <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void fetchList()} disabled={loading}>
            Actualizar
          </ZHBtn>
        }
      >
        <ZHDataTable
          columns={columns}
          rows={items}
          rowKey={(row) => row.id}
          loading={loading}
          emptyMessage="No hay créditos de proveedor registrados."
          page={page}
          pageSize={PAGE_SIZE}
          onPageChange={setPage}
          total={total}
        />
      </ZHCard>
    </PageShell>
  );
}
