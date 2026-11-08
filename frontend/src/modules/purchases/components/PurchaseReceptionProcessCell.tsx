import { Badge } from "../../../components/PageShell";
import { ZHCompactNotice } from "../../../components/zh/notices";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import {
  PROCESSING_STATUS_VARIANT,
  buildDocumentStatusLabel,
  buildProcessingStatusLabel,
} from "../pages/purchaseReceptionLabels";
import { receptionXmlState } from "../utils/receptionFilters";

const XML_BADGE_VARIANT = {
  stored: "success",
  pending: "neutral",
  loading: "neutral",
  error: "error",
} as const;

/**
 * Celda "Recepción y XML" de `/purchases/reception` — lista compacta Recepción/Lectura/XML, alineada en
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
  // del TXT, sin "Consultar XML" todavía). "Consultando"/"Error" solo existen en esta sesión: el
  // backend no guarda los intentos fallidos (PURCHASES-RECEPTION-FILTERS-01).
  const xml = receptionXmlState(row, xmlState);
  const hasDetail = xml === "stored";
  const xmlLabel = {
    stored: t("purchases.reception.xml.stored", "Almacenado"),
    pending: t("purchases.reception.xml.pending", "Pendiente"),
    loading: t("purchases.reception.xml.loading", "Consultando..."),
    error: t("purchases.reception.xml.sessionError", "Error en esta sesión"),
  }[xml];
  const xmlTitle =
    xml === "loading" || xml === "error"
      ? t(
          "purchases.reception.xml.sessionHint",
          "Estado de esta sesión: no se guarda; al recargar se verá como Pendiente.",
        )
      : undefined;
  const processingWarningNotice =
    row.processingStatus === "PROCESSED_WITH_WARNINGS" && row.processingNotes
      ? {
          severity: "warning" as const,
          intent: "status" as const,
          source: "domain-status" as const,
          label: t("purchases.reception.processing.warningNotice", "Aviso XML"),
          detail: row.processingNotes,
        }
      : null;

  return (
    <div className="pur-process-cell">
      <span className="pur-process-label">
        {t("purchases.reception.process.document", "Recepción")}
      </span>
      <span className="pur-process-value">
        <Badge
          variant="info"
          upper
          size="md"
          label={documentStatusLabel[row.documentStatus]}
          title={t("purchases.reception.process.documentSaved", "Recepción guardada")}
        />
      </span>

      <span className="pur-process-label">
        {t("purchases.reception.process.detail", "Lectura")}
      </span>
      <span className="pur-process-value">
        {hasDetail ? (
          <>
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
            {processingWarningNotice && (
              <ZHCompactNotice notice={processingWarningNotice} />
            )}
          </>
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
        <Badge variant={XML_BADGE_VARIANT[xml]} label={xmlLabel} title={xmlTitle} />
      </span>
    </div>
  );
}
