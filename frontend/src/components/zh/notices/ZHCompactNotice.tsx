import type { NoticeVM } from "../../../notices";
import { ZHNoticeBadge } from "./ZHNoticeBadge";
import { ZHNoticePopover } from "./ZHNoticePopover";
import { useNoticeDisclosure } from "./useNoticeDisclosure";

interface ZHCompactNoticeProps {
  notice: NoticeVM;
  className?: string;
}

/** Aviso compacto de línea/campo: badge + popover con el detalle completo. Reemplaza texto largo
 * repetido (p. ej. `.pdl-cost-alert` por línea) sin ocultar el motivo — el popover siempre
 * muestra `detail` íntegro cuando existe. */
export function ZHCompactNotice({ notice, className }: ZHCompactNoticeProps) {
  const { open, pinned, triggerRef, titleId, openByHover, toggleByClick, close } =
    useNoticeDisclosure(notice.intent);

  return (
    <span className={["zh-notice-compact", className].filter(Boolean).join(" ")}>
      <ZHNoticeBadge
        label={notice.label}
        severity={notice.severity}
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
        title={notice.label}
        severity={notice.severity}
        closeOnMouseLeave={!pinned}
      >
        {notice.detail && <p className="zh-notice-popover__detail">{notice.detail}</p>}
      </ZHNoticePopover>
    </span>
  );
}
