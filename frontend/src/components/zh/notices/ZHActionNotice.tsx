import type { NoticeVM } from "../../../notices";
import { ZHBtn } from "../ZHForm";
import { ZHNoticeBadge } from "./ZHNoticeBadge";
import { ZHNoticePopover } from "./ZHNoticePopover";
import { useNoticeDisclosure } from "./useNoticeDisclosure";

interface ZHActionNoticeProps {
  notice: NoticeVM;
  className?: string;
}

/** Igual que ZHCompactNotice, pero para intent "actionRequired"/"confirmation": el popover
 * incluye el botón de acción (`actionLabel`/`onAction`) cuando vienen definidos. Preparado para
 * uso futuro — el primer caso de uso (Compras) no pasa `onAction` todavía. */
export function ZHActionNotice({ notice, className }: ZHActionNoticeProps) {
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
        {notice.actionLabel && notice.onAction && (
          <ZHBtn
            type="button"
            variant="primary"
            size="sm"
            className="zh-notice-popover__action"
            onClick={() => {
              notice.onAction?.();
              close();
            }}
          >
            {notice.actionLabel}
          </ZHBtn>
        )}
      </ZHNoticePopover>
    </span>
  );
}
