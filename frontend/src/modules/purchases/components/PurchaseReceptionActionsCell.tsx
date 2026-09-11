import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";

/** Recepción conserva el XML fiscal y abre el flujo operativo propio de cada tipo. */
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
    return <div className="pur-actions-cell">
      <Badge variant="success" label="Gasto ya ingresado al sistema" />
      {viewXmlButton}
    </div>;
  }

  if (row.purchaseExists) {
    return (
      <div className="pur-actions-cell">
        <Badge
          variant="success"
          label={t(
            "purchases.reception.actions.purchaseAlreadyEntered",
            "Compra ya ingresada al sistema",
          )}
        />
        {row.purchaseId && (
          <ZHBtn
            variant="secondary"
            size="xs"
            type="button"
            onClick={() =>
              window.open(
                `/purchases?invoiceId=${row.purchaseId}`,
                "_blank",
                "noopener,noreferrer",
              )
            }
          >
            {t(
              "purchases.duplicate.viewExisting",
              "Ver compra existente",
            )}
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
          <Badge
            variant="success"
            label={t(
              "purchases.creditNote.actions.ncAlreadyProcessed",
              "NC ya procesada",
            )}
          />
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
            {t("purchases.creditNote.actions.viewExistingNc", "Ver NC existente")}
          </ZHBtn>
          {consultXmlButton}
          {viewXmlButton}
        </div>
      );
    }

    if (row.affectedPurchaseExists && row.affectedPurchaseId) {
      // PURCHASE-RECEPTION-CREDIT-NOTE-CANCELLED-REPROCESS-01 — la única NC previa de esta
      // recepción está Cancelled (nunca bloquea, ver creditNoteExists arriba): se ofrece
      // "Procesar nuevamente" (idéntico flujo de creación, crea una NC/devolución nueva y
      // limpia — nunca reutiliza la anulada) junto con "Ver NC anulada" para conservar el
      // historial, en vez del botón "Procesar NC" de primera vez.
      const isReprocessing = Boolean(row.cancelledCreditNoteId);
      return (
        <div className="pur-actions-cell">
          {isReprocessing && (
            <Badge
              variant="warning"
              label={t("purchases.creditNote.actions.ncCancelled", "NC anulada")}
            />
          )}
          <ZHBtn
            variant="primary"
            size="xs"
            type="button"
            title={t("purchases.creditNote.actions.processCreditNote", "Procesar nota de crédito")}
            disabled={resolvingCreditNoteId === row.documentId}
            onClick={() => onProcessCreditNote(row)}
          >
            {isReprocessing
              ? t("purchases.creditNote.actions.reprocessNc", "Procesar nuevamente")
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
              AccessKey está Cancelled (historial, purchaseExists=false), se ofrece "Procesar
              nuevamente" + "Ver compra anulada" en vez de "Crear compra" de primera vez, mismo
              patrón ya cerrado para NC. */}
          {row.cancelledPurchaseId && (
            <Badge
              variant="warning"
              label={t("purchases.reception.actions.purchaseCancelled", "Compra anulada")}
            />
          )}
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
            {row.cancelledPurchaseId
              ? t("purchases.reception.actions.reprocessPurchase", "Procesar nuevamente")
              : t("purchases.reception.actions.createPurchase", "Crear compra")}
          </ZHBtn>
          {row.cancelledPurchaseId && (
            <ZHBtn
              variant="secondary"
              size="xs"
              type="button"
              onClick={() =>
                window.open(
                  `/purchases?invoiceId=${row.cancelledPurchaseId}`,
                  "_blank",
                  "noopener,noreferrer",
                )
              }
            >
              {t("purchases.reception.actions.viewCancelledPurchase", "Ver compra anulada")}
            </ZHBtn>
          )}
          {row.sourceDocType === "INVOICE" && (
            <>
              {row.cancelledExpenseId && (
                <Badge
                  variant="warning"
                  label={t("purchases.reception.actions.expenseCancelled", "Gasto anulado")}
                />
              )}
              <ZHBtn variant="secondary" size="xs" type="button"
                disabled={!row.supplierExists || row.supplierIsActive === false}
                title={!row.supplierExists ? "Cree primero el proveedor" : undefined}
                onClick={() => window.open(
                  `/expenses/documents/new?fromReceptionId=${row.documentId}`,
                  "_blank", "noopener,noreferrer",
                )}>
                {row.cancelledExpenseId
                  ? t("purchases.reception.actions.reprocessExpense", "Procesar nuevamente")
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
      {row.documentStatus === "PROCESSED" && (
        <Badge
          variant="success"
          label={t("purchases.reception.actions.purchaseCreated", "Compra creada")}
        />
      )}
    </div>
  );
}
