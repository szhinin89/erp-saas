import { Badge } from "../../../components/PageShell";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import {
  PROCESSING_STATUS_VARIANT,
  buildDocumentStatusLabel,
  buildProcessingStatusLabel,
} from "../pages/purchaseReceptionLabels";

/**
 * Celda "Proceso" de `/purchases/reception` — lista compacta Documento/Detalle/XML, alineada en
 * grid label-valor. No repite el estado del proveedor (ya vive en la columna Proveedor).
 */
export function PurchaseReceptionProcessCell({
  row,
  xmlState,
}: {
  row: PurchaseReceptionItem;
  xmlState: "loading" | "error" | undefined;
}) {
  const { t } = useI18n();
  const documentStatusLabel = buildDocumentStatusLabel(t);
  const processingStatusLabel = buildProcessingStatusLabel(t);
  // El detalle/XML todavía no existe mientras el documento no salga de IMPORTED (recién llegado
  // del TXT, sin "Consultar XML" todavía).
  const hasDetail = row.documentStatus !== "IMPORTED";

  return (
    <div className="pur-process-cell">
      <span className="pur-process-label">
        {t("purchases.reception.process.document", "Documento")}
      </span>
      <span className="pur-process-value">
        <Badge
          variant="info"
          upper
          size="md"
          label={documentStatusLabel[row.documentStatus]}
          title={`Documento persistido — id ${row.documentId}`}
        />
      </span>

      <span className="pur-process-label">
        {t("purchases.reception.process.detail", "Detalle")}
      </span>
      <span className="pur-process-value">
        {hasDetail ? (
          <Badge
            variant={PROCESSING_STATUS_VARIANT[row.processingStatus]}
            label={processingStatusLabel[row.processingStatus]}
            title={
              row.processingNotes ??
              t(
                "purchases.reception.processing.defaultNote",
                "El detalle del XML se interpretó sin advertencias.",
              )
            }
          />
        ) : (
          <span className="pur-process-value--muted">
            {t("purchases.reception.process.notAvailable", "—")}
          </span>
        )}
      </span>

      <span className="pur-process-label">
        {t("purchases.reception.process.xml", "XML")}
      </span>
      <span className="pur-process-value">
        {hasDetail ? (
          <Badge
            variant="success"
            label={t("purchases.reception.xml.received", "Recibido")}
          />
        ) : xmlState === "loading" ? (
          <Badge
            variant="neutral"
            label={t("purchases.reception.xml.loading", "Consultando...")}
          />
        ) : (
          <Badge
            variant={xmlState === "error" ? "error" : "neutral"}
            label={
              xmlState === "error"
                ? t("purchases.reception.xml.error", "Error consulta")
                : t("purchases.reception.xml.pending", "Pendiente")
            }
          />
        )}
      </span>
    </div>
  );
}
