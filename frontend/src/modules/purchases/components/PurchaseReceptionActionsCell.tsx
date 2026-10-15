import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import { receptionPurchaseView } from "../utils/receptionPurchaseState";

/**
 * Recepción conserva el XML fiscal y abre el flujo operativo propio de cada tipo. FASE 5
 * RECEPTION-UX — solo acciones: el estado del registro ERP (compra/gasto/NC, activo o anulado) se
 * muestra una única vez en la columna "Registro ERP" (receptionErpRecordView), nunca aquí.
 */
export function PurchaseReceptionActionsCell({
  row,
  xmlState,
  onDownloadXml,
  onViewXml,
  onProcessCreditNote,
  resolvingCreditNoteId,
}: {
  row: PurchaseReceptionItem;
  xmlState: "loading" | "error" | undefined;
  onDownloadXml: (documentId: string) => void;
  onViewXml: (documentId: string) => void;
  onProcessCreditNote: (row: PurchaseReceptionItem) => void;
  resolvingCreditNoteId: string | null;
}) {
  const { t } = useI18n();
  // FASE 5 PURCHASE-UX — estado REAL de la compra relacionada (SSOT): textos y acciones.
  const purchaseView = receptionPurchaseView(row.purchaseState);
  const openPurchase = (purchaseId: string) =>
    window.open(`/purchases?invoiceId=${purchaseId}`, "_blank", "noopener,noreferrer");

  // El XML solo existe una vez el documento fue verificado/procesado (AttachSriAuthorization) —
  // en Importado todavía no hay nada que mostrar.
  const canViewXml =
    row.documentStatus === "VERIFIED" || row.documentStatus === "PROCESSED";
  const viewXmlButton = canViewXml && (
    <ZHBtn
      variant="secondary"
      size="xs"
      type="button"
      onClick={() => onViewXml(row.documentId)}
    >
      {t("purchases.reception.actions.viewXml", "Ver XML")}
    </ZHBtn>
  );

  const consultXmlButton = row.documentStatus === "IMPORTED" && (
    <ZHBtn variant="secondary" size="xs" type="button"
      disabled={xmlState === "loading"}
      onClick={() => onDownloadXml(row.documentId)}>
      {t("purchases.reception.actions.consultXml", "Consultar XML")}
    </ZHBtn>
  );

  if (row.sourceDocType === "INVOICE" && row.expenseExists) {
    return <div className="pur-actions-cell">{viewXmlButton}</div>;
  }

  // Compra ACTIVA: borrador → "Continuar borrador"; confirmada → "Ver compra". "Crear compra" y
  // "Crear gasto" quedan bloqueados (no se renderizan) mientras exista.
  if (purchaseView.hasActivePurchase) {
    return (
      <div className="pur-actions-cell">
        {row.purchaseId && (
          <ZHBtn
            variant={purchaseView.action === "continueDraft" ? "primary" : "secondary"}
            size="xs"
            type="button"
            onClick={() => openPurchase(row.purchaseId as string)}
          >
            {t(purchaseView.actionLabel.key, purchaseView.actionLabel.fallback)}
          </ZHBtn>
        )}
        {viewXmlButton}
      </div>
    );
  }

  if (row.supplierExists && row.supplierIsActive === false) {
    return (
      <div className="pur-actions-cell">
        <Badge
          variant="warning"
          label={t(
            "purchases.reception.supplier.inactive",
            "Proveedor inactivo",
          )}
        />
        {row.supplierId && (
          <ZHBtn
            variant="secondary"
            size="xs"
            type="button"
            onClick={() =>
              window.open(
                `/masterdata/business-partners/${row.supplierId}`,
                "_blank",
                "noopener,noreferrer",
              )
            }
          >
            {t("purchases.reception.supplier.viewButton", "Ver proveedor")}
          </ZHBtn>
        )}
        {viewXmlButton}
      </div>
    );
  }

  if (row.sourceDocType === "CREDIT_NOTE") {
    // PURCHASE-CREDIT-NOTE-RECEPTION-IDEMPOTENCY-UI-01 — una recepción ya vinculada a una NC no
    // debe volver a ofrecer "Procesar NC" (el backend la rechazaría por receptionDocumentId
    // único): se prioriza sobre affectedPurchaseExists, que ya dejó de ser relevante una vez
    // procesada.
    if (row.creditNoteExists && row.creditNoteId) {
      return (
        <div className="pur-actions-cell">
          <ZHBtn
            variant="secondary"
            size="xs"
            type="button"
            onClick={() =>
              window.open(
                `/purchases/credit-notes/${row.creditNoteId}`,
                "_blank",
                "noopener,noreferrer",
              )
            }
          >
            {t("purchases.creditNote.actions.viewExistingNc", "Ver NC")}
          </ZHBtn>
          {consultXmlButton}
          {viewXmlButton}
        </div>
      );
    }

    if (row.affectedPurchaseExists && row.affectedPurchaseId) {
      // PURCHASE-RECEPTION-CREDIT-NOTE-CANCELLED-REPROCESS-01 — la única NC previa de esta
      // recepción está Cancelled (nunca bloquea, ver creditNoteExists arriba): se ofrece
      // "Reprocesar NC" (idéntico flujo de creación, crea una NC/devolución nueva y
      // limpia — nunca reutiliza la anulada) junto con "Ver NC anulada" para conservar el
      // historial, en vez del botón "Procesar NC" de primera vez.
      const isReprocessing = Boolean(row.cancelledCreditNoteId);
      return (
        <div className="pur-actions-cell">
          <ZHBtn
            variant="primary"
            size="xs"
            type="button"
            title={t("purchases.creditNote.actions.processCreditNote", "Procesar nota de crédito")}
            disabled={resolvingCreditNoteId === row.documentId}
            onClick={() => onProcessCreditNote(row)}
          >
            {isReprocessing
              ? t("purchases.creditNote.actions.reprocessNc", "Reprocesar NC")
              : t("purchases.creditNote.actions.processNc", "Procesar NC")}
          </ZHBtn>
          {isReprocessing && (
            <ZHBtn
              variant="secondary"
              size="xs"
              type="button"
              onClick={() =>
                window.open(
                  `/purchases/credit-notes/${row.cancelledCreditNoteId}`,
                  "_blank",
                  "noopener,noreferrer",
                )
              }
            >
              {t("purchases.creditNote.actions.viewCancelledNc", "Ver NC anulada")}
            </ZHBtn>
          )}
          {consultXmlButton}
          {viewXmlButton}
        </div>
      );
    }

    return (
      <div className="pur-actions-cell">
        <Badge
          variant="neutral"
          label={t("purchases.reception.actions.ncPending", "NC pendiente")}
          title={t(
            "purchases.creditNote.errors.affectedInvoiceRequired",
            "La factura afectada debe estar ingresada antes de procesar la NC",
          )}
        />
        <p className="pur-actions-hint">
          {t(
            "purchases.reception.document.enterInvoiceFirst",
            "Ingrese primero la factura afectada",
          )}
        </p>
        {consultXmlButton}
          {viewXmlButton}
      </div>
    );
  }

  return (
    <div className="pur-actions-cell">
      {row.documentStatus === "IMPORTED" && (
        <ZHBtn
          variant="secondary"
          size="xs"
          type="button"
          disabled={xmlState === "loading"}
          onClick={() => onDownloadXml(row.documentId)}
        >
          {t("purchases.reception.actions.consultXml", "Consultar XML")}
        </ZHBtn>
      )}
      {viewXmlButton}
      {row.documentStatus === "VERIFIED" && (
        // Un único botón para todos los documentos Verificados, sin excepciones visibles: la
        // reconstrucción del detalle cuando el intento anterior falló (o el rechazo si el XML
        // sigue sin poder interpretarse) ocurre de forma transparente dentro de create-draft —
        // el usuario nunca ve un paso, label ni concepto distinto de "Crear Compra". Se
        // mantiene visible pero deshabilitado sin proveedor ERP (nunca se oculta) — el
        // proveedor es requisito previo real: create-draft resuelve el Supplier del BP y
        // fallaría igual del lado del servidor.
        <>
          {/* RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — si la única compra previa de este
              AccessKey está Cancelled (historial, sin compra activa), se ofrece "Procesar
              nuevamente" + "Ver compra anulada" en vez de "Crear compra" de primera vez, mismo
              patrón ya cerrado para NC. */}
          {/* FASE 5 PURCHASE-UX — sin compra activa: compra anulada (Reprocesar compra) o
              borrador descartado (Crear compra), cada uno con su historial. El badge de estado
              vive en "Registro ERP". */}
          <ZHBtn
            variant="primary"
            size="xs"
            type="button"
            disabled={!row.supplierExists || row.supplierIsActive === false}
            title={
              row.supplierIsActive === false
                ? t(
                    "purchases.reception.actions.createPurchaseDisabledSupplierInactive",
                    "Active primero el proveedor",
                  )
                : row.supplierExists
                ? undefined
                : t(
                    "purchases.reception.actions.createPurchaseDisabledSupplierMissing",
                    "Cree primero el proveedor",
                  )
            }
            onClick={() =>
              // Nueva pestaña, no navigate(): la Recepción funciona como bandeja de
              // documentos recibidos y debe permanecer abierta con su lista cargada mientras
              // la compra se arma en /purchases.
              window.open(
                `/purchases?fromReceptionId=${row.documentId}&accessKey=${encodeURIComponent(row.accessKey)}`,
                "_blank",
                "noopener,noreferrer",
              )
            }
          >
            {t(purchaseView.actionLabel.key, purchaseView.actionLabel.fallback)}
          </ZHBtn>
          {purchaseView.history && row.cancelledPurchaseId && (
            <ZHBtn
              variant="secondary"
              size="xs"
              type="button"
              onClick={() => openPurchase(row.cancelledPurchaseId as string)}
            >
              {t(purchaseView.history.key, purchaseView.history.fallback)}
            </ZHBtn>
          )}
          {row.sourceDocType === "INVOICE" && (
            <>
              <ZHBtn variant="secondary" size="xs" type="button"
                disabled={!row.supplierExists || row.supplierIsActive === false}
                title={
                  row.supplierExists
                    ? undefined
                    : t(
                        "purchases.reception.actions.createPurchaseDisabledSupplierMissing",
                        "Cree primero el proveedor",
                      )
                }
                onClick={() => window.open(
                  `/expenses/documents/new?fromReceptionId=${row.documentId}`,
                  "_blank", "noopener,noreferrer",
                )}>
                {row.cancelledExpenseId
                  ? t("purchases.reception.actions.reprocessExpense", "Reprocesar gasto")
                  : t("purchases.reception.actions.createExpense", "Crear gasto")}
              </ZHBtn>
              {row.cancelledExpenseId && (
                <ZHBtn
                  variant="secondary"
                  size="xs"
                  type="button"
                  onClick={() =>
                    window.open(
                      `/expenses/documents/${row.cancelledExpenseId}`,
                      "_blank",
                      "noopener,noreferrer",
                    )
                  }
                >
                  {t("purchases.reception.actions.viewCancelledExpense", "Ver gasto anulado")}
                </ZHBtn>
              )}
            </>
          )}
          {!row.supplierExists && (
            <p className="pur-actions-hint">
              {t(
                "purchases.reception.actions.createPurchaseDisabledSupplierMissing",
                "Cree primero el proveedor",
              )}
            </p>
          )}
          {row.supplierExists && row.supplierIsActive === false && (
            <p className="pur-actions-hint">
              {t(
                "purchases.reception.actions.createPurchaseDisabledSupplierInactive",
                "Active primero el proveedor",
              )}
            </p>
          )}
        </>
      )}
    </div>
  );
}
