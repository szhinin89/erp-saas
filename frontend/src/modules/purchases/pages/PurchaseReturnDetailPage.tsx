import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError } from "../../lib/apiError";
import { purchaseReturnService, type PurchaseReturnDto, type PurchaseReturnDetailDto } from "../api/purchaseReturnService";
import { getPurchaseReturnStatusLabel, getPurchaseReturnFiscalStatusLabel } from "../utils/purchaseReturnStatus";
import { useI18n } from "../../../i18n/i18n";

/** Historial íntegro, exclusivamente de consulta. */
export function PurchaseReturnDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const [document, setDocument] = useState<PurchaseReturnDto | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    if (!id) return;
    purchaseReturnService.getById(id).then((dto) => { if (active) setDocument(dto); })
      .catch((err: unknown) => { if (active) setError(formatApiRequestError(err, { generic: "No se pudo consultar la devolución." })); });
    return () => { active = false; };
  }, [id]);
  const columns: ZHDataTableColumn<PurchaseReturnDetailDto>[] = [
    { key: "product", header: "Producto", render: (line) => line.itemName ?? line.itemId },
    { key: "quantity", header: "Cantidad", align: "right", render: (line) => line.quantity },
    { key: "warehouse", header: "Bodega", render: (line) => line.warehouseName ?? line.warehouseId },
  ];
  return <PageShell title={`Historial de devolución ${document?.returnNumber ?? ""}`}
    action={<ZHBtn variant="ghost" onClick={() => navigate("/purchases/credit-notes")}>Volver a notas de crédito</ZHBtn>}>
    <ZHPageNotice variant="info" message="Solo consulta. Las operaciones se realizan desde Nota de crédito." />
    {error && <ZHPageNotice variant="error" message={error} />}
    {!document && !error && <p>Cargando...</p>}
    {document && <>
      <ZHCard title="Datos generales">
        <p>Factura origen: {document.purchaseInvoiceNumber ?? document.purchaseInvoiceId}</p>
        <p>Estado: {getPurchaseReturnStatusLabel(document.status, t)}</p>
        <p>Estado fiscal: {getPurchaseReturnFiscalStatusLabel(document.fiscalStatus, t)}</p>
        <p>Creada: {formatDateTime(document.createdAt)}</p>
        <p>{document.reason}</p>
        {document.authorizedGrandTotal !== null && <ZHMoneyValue precision="money" value={document.authorizedGrandTotal} />}
        {document.supplierCreditNoteInvoiceNumber && <p>NC recibida: {document.supplierCreditNoteInvoiceNumber}</p>}
        {document.supplierCreditNoteAccessKey && <p>Clave de acceso: {document.supplierCreditNoteAccessKey}</p>}
        {document.linkedPurchaseCreditNoteId && <ZHBtn variant="ghost" onClick={() => navigate(`/purchases/credit-notes/${document.linkedPurchaseCreditNoteId}`)}>Ver nota de crédito</ZHBtn>}
        {document.supplierCreditId && <ZHBtn variant="ghost" onClick={() => navigate(`/suppliers/credits/${document.supplierCreditId}`)}>Ver saldo a favor</ZHBtn>}
      </ZHCard>
      <ZHCard title="Líneas registradas"><ZHDataTable columns={columns} rows={document.lines} rowKey={(line) => line.id} /></ZHCard>
      {document.cancellationReason && <ZHPageNotice variant="info" message="Devolución cancelada" detail={document.cancellationReason} />}
    </>}
  </PageShell>;
}
