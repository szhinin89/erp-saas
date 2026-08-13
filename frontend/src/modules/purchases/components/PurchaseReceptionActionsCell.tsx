import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";

/**
 * Celda "Acciones" de `/purchases/reception`. Notas de crédito (FLOW-READY-02B.1) solo se cargan y
 * muestran — sin Consultar XML, sin Crear compra (nunca reintroduce matching de productos ni crea
 * proveedor desde esta fila). FLOW-READY-02C.4 agrega la única acción real posible: si la factura
 * afectada ya existe en el ERP, "Procesar NC" abre la pantalla centralizada de Nota de Crédito de
 * Compra (`PurchaseCreditNoteFormPage`, FLOW-READY-02C.3) en una pestaña nueva — mismo criterio que
 * "Crear compra"/"Abrir factura afectada": la Recepción permanece abierta como bandeja. El usuario
 * decide ahí mismo si es Devolución o Descuento/promoción; esta celda nunca infiere el tipo.
 */
export function PurchaseReceptionActionsCell({
  row,
  xmlState,
  onDownloadXml,
  onViewXml,
}: {
  row: PurchaseReceptionItem;
  xmlState: "loading" | "error" | undefined;
  onDownloadXml: (documentId: string) => void;
  onViewXml: (documentId: string) => void;
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
    if (row.affectedPurchaseExists && row.affectedPurchaseId) {
      return (
        <div className="pur-actions-cell">
          <ZHBtn
            variant="primary"
            size="xs"
            type="button"
            title={t("purchases.creditNote.actions.processCreditNote", "Procesar nota de crédito")}
            onClick={() =>
              window.open(
                `/purchases/credit-notes/new?invoiceId=${row.affectedPurchaseId}&receptionDocumentId=${row.documentId}`,
                "_blank",
                "noopener,noreferrer",
              )
            }
          >
            {t("purchases.creditNote.actions.processNc", "Procesar NC")}
          </ZHBtn>
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
            {t("purchases.reception.actions.createPurchase", "Crear compra")}
          </ZHBtn>
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
