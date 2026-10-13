import type { ReactNode } from "react";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { useI18n } from "../../../i18n/i18n";

/**
 * FASE 5 PURCHASE-UX — acciones del lote de Recepción en una fila compacta: "Acciones del lote · N
 * comprobantes" a la izquierda y los botones a la derecha (wrap en móvil). Componente local del
 * módulo (no del Design System): solo ZHBtn + CSS local de PurchaseReception. Sin importación no se
 * muestra; el progreso solo ocupa espacio cuando existe.
 */
export function PurchaseReceptionBatchActions({
  importedCount,
  uploading,
  downloadingXml,
  refreshing,
  onRefreshStatuses,
  onDownloadPendingXml,
  progress,
}: {
  /** Comprobantes importados; null = todavía no hay importación (la barra no se muestra). */
  importedCount: number | null;
  uploading: boolean;
  downloadingXml: boolean;
  refreshing: boolean;
  onRefreshStatuses: () => void;
  onDownloadPendingXml: () => void;
  /** Progreso de la descarga de XML; null/undefined = no se renderiza nada debajo de la fila. */
  progress?: ReactNode;
}) {
  const { t } = useI18n();
  if (importedCount === null) return null;
  // Ambas acciones se bloquean mientras se importa, se descarga XML o se actualizan estados.
  const busy = uploading || downloadingXml || refreshing;

  return (
    <div className="pg-section pur-reception-batch">
      <div className="pur-reception-batch__bar">
        <span className="pur-reception-batch__title">
          {t("purchases.reception.batchActions.title", "Acciones del lote")}
          <span className="pur-reception-batch__count">
            {" · "}
            {t("purchases.reception.batchActions.count", { count: importedCount })}
          </span>
        </span>
        <div className="pur-reception-batch__actions">
          <ZHBtn
            variant="secondary"
            size="sm"
            type="button"
            disabled={busy}
            onClick={onRefreshStatuses}
          >
            <span className="material-symbols-outlined zh-icon-md">refresh</span>
            {refreshing
              ? t("purchases.reception.actions.refreshingStatuses", "Actualizando…")
              : t("purchases.reception.actions.refreshStatuses", "Actualizar estados")}
          </ZHBtn>
          <ZHBtn
            variant="secondary"
            size="sm"
            type="button"
            disabled={busy}
            onClick={onDownloadPendingXml}
          >
            <span className="material-symbols-outlined zh-icon-md">download</span>
            {t("purchases.reception.actions.downloadPendingXml", "Descargar XML pendientes")}
          </ZHBtn>
        </div>
      </div>
      {progress ?? null}
    </div>
  );
}
