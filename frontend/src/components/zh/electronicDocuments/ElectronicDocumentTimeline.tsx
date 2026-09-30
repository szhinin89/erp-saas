import { useI18n } from "../../../i18n/i18n";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import type { ElectronicDocumentTimelineEventDto } from "./electronicDocumentDiagnosticTypes";
import "./electronic-documents.css";

type Props = { timeline: ElectronicDocumentTimelineEventDto[] };

/** Timeline cronológico de transiciones — reconstruido desde auditoría real, nunca inventado. */
export function ElectronicDocumentTimeline({ timeline }: Props) {
  const { t } = useI18n();

  if (timeline.length === 0) {
    return (
      <p className="zh-edoc-hint-sm">
        {t("electronicDocuments.monitor.detail.timelineEmpty")}
      </p>
    );
  }

  return (
    <ul className="zh-edoc-timeline">
      {timeline.map((ev, idx) => (
        <li key={idx} className="zh-edoc-timeline-item">
          <span className="zh-edoc-timeline-dot" />
          <div className="zh-edoc-timeline-body">
            <div className="zh-edoc-detail-item-value">
              {t(`electronicDocuments.monitor.timelineAction.${ev.action}`)}
              {ev.fromState && (
                <>
                  {" "}
                  — {t(
                    `electronicDocuments.monitor.state.${ev.fromState}`,
                  )} → {t(`electronicDocuments.monitor.state.${ev.toState}`)}
                </>
              )}
            </div>
            <div className="zh-edoc-timeline-meta">
              {formatDateTime(ev.occurredAtUtc)} · {ev.userName}
              {ev.durationSinceLastMinutes !== null && (
                <>
                  {" "}
                  · {t("electronicDocuments.monitor.detail.duration")}:{" "}
                  {Math.round(ev.durationSinceLastMinutes)} min
                </>
              )}
            </div>
          </div>
        </li>
      ))}
    </ul>
  );
}
