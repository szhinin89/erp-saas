import { useI18n } from "../../../i18n/i18n";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import type { ElectronicDocumentTimelineEventDto } from "./electronicDocumentDiagnosticTypes";

type Props = { timeline: ElectronicDocumentTimelineEventDto[] };

/** Timeline cronológico de transiciones — reconstruido desde auditoría real, nunca inventado. */
export function ElectronicDocumentTimeline({ timeline }: Props) {
  const { t } = useI18n();

  if (timeline.length === 0) {
    return (
      <p className="edm-hint-sm">
        {t("electronicDocuments.monitor.detail.timelineEmpty")}
      </p>
    );
  }

  return (
    <ul className="edm-timeline">
      {timeline.map((ev, idx) => (
        <li key={idx} className="edm-timeline-item">
          <span className="edm-timeline-dot" />
          <div className="edm-timeline-body">
            <div className="edm-detail-item-value">
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
            <div className="edm-timeline-meta">
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
