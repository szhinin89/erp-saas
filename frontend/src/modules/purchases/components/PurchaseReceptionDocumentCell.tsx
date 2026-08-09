import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";

/**
 * Celda "Documento" de `/purchases/reception`. Para notas de crédito (FLOW-READY-02B.2), "Afecta:
 * <número>" abre la factura afectada en `/purchases?invoiceId=<id>` en nueva pestaña cuando ya está
 * ingresada en el ERP; si no, se muestra como texto plano con la ayuda correspondiente.
 */
export function PurchaseReceptionDocumentCell({ row }: { row: PurchaseReceptionItem }) {
  const { t } = useI18n();

  if (row.sourceDocType !== "CREDIT_NOTE") {
    return (
      <div className="pur-document-cell">
        <Badge
          variant="neutral"
          label={t("purchases.reception.document.invoice", "Factura")}
        />
        <p className="pur-document-number">{row.invoiceNumber}</p>
      </div>
    );
  }

  const affectsLabel = t("purchases.reception.document.affects", {
    number: row.modifiedDocumentNumber ?? "—",
  });

  return (
    <div className="pur-document-cell">
      <Badge
        variant="warning"
        label={t("purchases.reception.document.creditNote", "Nota de crédito")}
      />
      <p className="pur-document-number">{row.invoiceNumber}</p>
      {row.affectedPurchaseExists && row.affectedPurchaseId ? (
        <>
          <ZHBtn
            variant="ghost"
            size="xs"
            type="button"
            className="pur-document-affects-link"
            title={t(
              "purchases.reception.document.openAffectedInvoice",
              "Abrir factura afectada",
            )}
            onClick={() =>
              // Nueva pestaña, mismo criterio que "Crear compra": la Recepción permanece abierta.
              window.open(
                `/purchases?invoiceId=${row.affectedPurchaseId}`,
                "_blank",
                "noopener,noreferrer",
              )
            }
          >
            {affectsLabel}
          </ZHBtn>
          <p className="pur-document-affects-hint">
            {t("purchases.reception.document.invoiceFound", "Factura encontrada")}
          </p>
        </>
      ) : (
        <>
          <p className="pur-document-affects">{affectsLabel}</p>
          <p
            className="pur-document-affects-hint pur-document-affects-hint--warning"
            title={t(
              "purchases.reception.document.enterInvoiceFirst",
              "Ingrese primero la factura afectada",
            )}
          >
            {t(
              "purchases.reception.document.invoiceNotEntered",
              "Factura afectada no ingresada",
            )}
          </p>
        </>
      )}
    </div>
  );
}
