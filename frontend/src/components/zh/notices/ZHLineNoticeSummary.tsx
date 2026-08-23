import type { NoticeVM } from "../../../notices";
import { ZHNoticeBadge } from "./ZHNoticeBadge";
import { ZHNoticePopover } from "./ZHNoticePopover";
import { ZHNoticeList } from "./ZHNoticeList";
import { useNoticeDisclosure } from "./useNoticeDisclosure";

const SEVERITY_RANK: Record<NoticeVM["severity"], number> = {
  danger: 3,
  warning: 2,
  info: 1,
  success: 0,
};

function highestSeverity(notices: NoticeVM[]): NoticeVM["severity"] {
  return notices.reduce<NoticeVM["severity"]>(
    (worst, n) => (SEVERITY_RANK[n.severity] > SEVERITY_RANK[worst] ? n.severity : worst),
    "success",
  );
}

interface ZHLineNoticeSummaryProps {
  notices: NoticeVM[];
  /** Texto del badge, p. ej. (n) => `${n} líneas con advertencias`. */
  label: (count: number) => string;
  title: string;
  className?: string;
}

/** Rollup de varios avisos (p. ej. uno por línea de una tabla) en un solo badge con el conteo y
 * la severidad más alta presente; el popover lista cada aviso individual (ZHNoticeList). */
export function ZHLineNoticeSummary({
  notices,
  label,
  title,
  className,
}: ZHLineNoticeSummaryProps) {
  const severity = highestSeverity(notices);
  const { open, pinned, triggerRef, titleId, openByHover, toggleByClick, close } =
    useNoticeDisclosure("status");

  if (notices.length === 0) return null;

  return (
    <span className={["zh-notice-compact", className].filter(Boolean).join(" ")}>
      <ZHNoticeBadge
        label={label(notices.length)}
        severity={severity}
        expanded={open}
        describedById={titleId}
        triggerRef={triggerRef}
        onClick={toggleByClick}
        onMouseEnter={openByHover}
      />
      <ZHNoticePopover
        open={open}
        onClose={close}
        anchorRef={triggerRef}
        titleId={titleId}
        title={title}
        severity={severity}
        closeOnMouseLeave={!pinned}
      >
        <ZHNoticeList notices={notices} />
      </ZHNoticePopover>
    </span>
  );
}
