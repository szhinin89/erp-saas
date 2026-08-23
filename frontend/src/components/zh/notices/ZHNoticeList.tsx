import type { NoticeVM } from "../../../notices";

const SEVERITY_ICON: Record<NoticeVM["severity"], string> = {
  info: "info",
  success: "check_circle",
  warning: "warning",
  danger: "error",
};

interface ZHNoticeListProps {
  notices: NoticeVM[];
  className?: string;
}

/** Lista vertical de avisos (severidad + label + detalle) — sin disclosure propia; se usa dentro
 * de un ZHNoticePopover de rollup (ZHLineNoticeSummary) o de forma standalone. */
export function ZHNoticeList({ notices, className }: ZHNoticeListProps) {
  if (notices.length === 0) return null;
  return (
    <ul className={["zh-notice-list", className].filter(Boolean).join(" ")}>
      {notices.map((notice, i) => (
        <li key={i} className={`zh-notice-list__item zh-notice-list__item--${notice.severity}`}>
          <span
            className={`material-symbols-outlined zh-notice-list__icon zh-notice-list__icon--${notice.severity}`}
            aria-hidden="true"
          >
            {SEVERITY_ICON[notice.severity]}
          </span>
          <span className="zh-notice-list__text">
            <span className="zh-notice-list__label">{notice.label}</span>
            {notice.detail && (
              <span className="zh-notice-list__detail">{notice.detail}</span>
            )}
          </span>
        </li>
      ))}
    </ul>
  );
}
