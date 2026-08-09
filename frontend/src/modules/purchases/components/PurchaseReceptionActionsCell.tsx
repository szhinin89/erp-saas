import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";

/**
 * Celda "Acciones" de `/purchases/reception`. Notas de crédito (FLOW-READY-02B.1, Fase 1) solo se
 * cargan y muestran — sin Consultar XML, sin Crear compra, sin navegación; el procesamiento real es
 * una fase futura.
 */
export function PurchaseReceptionActionsCell({
  row,
  xmlState,
  onDownloadXml,
}: {
  row: PurchaseReceptionItem;
  xmlState: "loading" | "error" | undefined;
  onDownloadXml: (documentId: string) => void;
}) {
  const { t } = useI18n();

  if (row.sourceDocType === "CREDIT_NOTE") {
    return (
      <div className="pur-actions-cell">
        <Badge
          variant="neutral"
          label={t("purchases.reception.actions.ncPending", "NC pendiente")}
          title={t(
            "purchases.reception.actions.noActionAvailable",
            "Sin acción disponible — procesamiento pendiente",
          )}
        />
        <p className="pur-actions-hint">
          {t(
            "purchases.reception.actions.documentReceived",
            "Documento recibido",
          )}
        </p>
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
            disabled={!row.supplierExists}
            title={
              row.supplierExists
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
                `/purchases?fromReceptionId=${row.documentId}`,
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
