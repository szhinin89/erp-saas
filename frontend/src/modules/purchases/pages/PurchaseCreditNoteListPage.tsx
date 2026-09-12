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
import {
  purchaseCreditNoteService,
  type PurchaseCreditNoteListItemDto,
} from "../api/purchaseCreditNoteService";
import {
  getPurchaseCreditNoteStatusLabel,
  PURCHASE_CREDIT_NOTE_STATUS_BADGE as STATUS_BADGE,
} from "../utils/purchaseCreditNoteStatus";
import "../../sales/styles/sales-return.css";

const PAGE_SIZE = 25;

const APPLICATION_TYPE_LABEL: Record<string, string> = {
  Return: "Devolución",
  Discount: "Descuento / promoción",
};

/**
 * PURCHASE-CREDIT-NOTE-ENTRY-SCREEN-DUAL-MODE-01 — listado de notas de crédito de compra,
 * punto de entrada del menú ("Compras → Notas de Crédito de Compra"); mismo patrón que
 * `PurchaseReturnListPage.tsx` (PageShell + ZHCard + ZHDataTable con paginación integrada,
 * reutiliza `sales-return.css`). El botón "Nueva" navega a `/purchases/credit-notes/new` —
 * la MISMA pantalla (`PurchaseCreditNoteFormPage`) que abre "Procesar NC" desde Recepción, aquí
 * sin parámetros (modo manual). Solo lectura: consume `GET /api/v1/purchases/credit-notes`, ya
 * implementado; sin filtro de texto libre (la query solo admite status/supplierId/invoiceId/fechas
 * — se expone únicamente `status`, igual que el listado de Devoluciones).
 */
export function PurchaseCreditNoteListPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [items, setItems] = useState<PurchaseCreditNoteListItemDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("");
  const [loading, setLoading] = useState(false);

  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const r = await purchaseCreditNoteService.list({
        status: status || undefined,
        page,
        pageSize: PAGE_SIZE,
      });
      setItems(r.items);
      setTotal(r.total);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, {
          generic: "No se pudo cargar el listado de notas de crédito.",
        }),
      );
    } finally {
      setLoading(false);
    }
  }, [status, page]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const columns: ZHDataTableColumn<PurchaseCreditNoteListItemDto>[] = [
    {
      key: "creditNoteNumber",
      header: "N.º NC",
      render: (row) => row.creditNoteNumber,
    },
    {
      key: "applicationType",
      header: "Tipo",
      render: (row) => APPLICATION_TYPE_LABEL[row.applicationType] ?? row.applicationType,
    },
    {
      key: "status",
      header: "Estado",
      render: (row) => (
        <Badge
          label={getPurchaseCreditNoteStatusLabel(row.status, t)}
          variant={STATUS_BADGE[row.status] ?? "gray"}
        />
      ),
    },
    {
      key: "totalAmount",
      header: "Total",
      align: "right",
      render: (row) => (
        <ZHMoneyValue
          value={row.totalAmount}
          decimals={getDecimalConfig().totalAmount}
          currencySymbol=""
        />
      ),
    },
    {
      key: "issueDate",
      header: "Fecha emisión",
      render: (row) => formatDate(row.issueDate),
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
          onClick={() => navigate(`/purchases/credit-notes/${row.id}`)}
        >
          {row.status === "Draft" ? "Editar" : "Ver"}
        </ZHBtn>
      ),
    },
  ];

  return (
    <PageShell
      title="Notas de Crédito de Compra"
      subtitle="Notas de crédito recibidas del proveedor sobre facturas de compra confirmadas"
      action={
        <ZHBtn
          type="button"
          variant="primary"
          onClick={() => navigate("/purchases/credit-notes/new")}
        >
          Nueva
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
          showRowNumber
          rowNumberOffset={(page - 1) * PAGE_SIZE}
          emptyMessage="No hay notas de crédito de compra registradas."
          page={page}
          pageSize={PAGE_SIZE}
          onPageChange={setPage}
          total={total}
        />
      </ZHCard>
    </PageShell>
  );
}
