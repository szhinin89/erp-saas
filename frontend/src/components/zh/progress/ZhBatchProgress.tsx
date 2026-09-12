import { useI18n } from "../../../i18n/i18n";
import "./zh-batch-progress.css";

export type ZhBatchProgressStatus = "idle" | "running" | "success" | "warning" | "error";

export type ZhBatchProgressProps = {
  title?: string;
  status: ZhBatchProgressStatus;
  total: number;
  processed: number;
  succeeded?: number;
  failed?: number;
  skipped?: number;
  currentLabel?: string;
  message?: string;
  /** Reduce el layout a una sola fila compacta (sin desglose succeeded/skipped/failed). */
  compact?: boolean;
};

/**
 * Feedback visual reutilizable para procesos por lote (descarga masiva de XML SRI,
 * importaciones, sincronizaciones, generación de reportes, etc.). No conoce el dominio del
 * proceso que reporta su progreso — solo total/processed/succeeded/skipped/failed + estado
 * visual. Cada caller decide cuándo montarlo y con qué cadencia actualiza sus props.
 */
export function ZhBatchProgress(props: ZhBatchProgressProps) {
  const { t } = useI18n();
  const {
    title,
    status,
    total,
    processed,
    succeeded = 0,
    failed = 0,
    skipped = 0,
    currentLabel,
    message,
    compact = false,
  } = props;

  const percent = total > 0 ? Math.min(100, Math.round((processed / total) * 100)) : 0;

  return (
    <div
      className={`zh-batch-progress zh-batch-progress--${status}${compact ? " zh-batch-progress--compact" : ""}`}
      role="status"
      aria-live="polite"
    >
      {title && <p className="zh-batch-progress-title">{title}</p>}

      <div className="zh-batch-progress-bar">
        <div
          className="zh-batch-progress-fill"
          style={{ "--batch-progress": `${percent}%` } as React.CSSProperties}
        />
      </div>

      <div className="zh-batch-progress-meta">
        <span className="zh-batch-progress-count">
          {t("zh.batchProgress.processedOfTotal", { processed, total })}
        </span>
        <span className="zh-batch-progress-percent">{percent}%</span>
      </div>

      {!compact && (
        <div className="zh-batch-progress-tallies">
          <span className="zh-batch-progress-tally zh-batch-progress-tally--success">
            {t("zh.batchProgress.succeeded", "Exitosos")}: {succeeded}
          </span>
          <span className="zh-batch-progress-tally zh-batch-progress-tally--neutral">
            {t("zh.batchProgress.skipped", "Omitidos")}: {skipped}
          </span>
          <span className="zh-batch-progress-tally zh-batch-progress-tally--error">
            {t("zh.batchProgress.failed", "Fallidos")}: {failed}
          </span>
        </div>
      )}

      {currentLabel && status === "running" && (
        <p className="zh-batch-progress-current">{currentLabel}</p>
      )}

      {message && <p className="zh-batch-progress-message">{message}</p>}
    </div>
  );
}
